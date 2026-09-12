using Soenneker.Asyncs.Locks;
using Microsoft.JSInterop;
using Soenneker.Atomics.ValueBools;
using Soenneker.Blazor.Utils.JsObjects.Abstract;
using Soenneker.Blazor.Utils.ModuleImport.Abstract;
using Soenneker.Extensions.CancellationTokens;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Blazor.Utils.JsObjects;

public sealed class JsObjectRegistry : IJsObjectRegistry
{

    private readonly IModuleImportUtil _moduleImportUtil;
    private readonly ConcurrentDictionary<string, ModuleObjects> _modules = new(1, 4, StringComparer.Ordinal);
    private readonly AsyncLock _moduleGate = new();
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly CancellationToken _lifetimeToken;
    private ValueAtomicBool _disposed;

    public JsObjectRegistry(IModuleImportUtil moduleImportUtil)
    {
        _lifetimeToken = _lifetimeCancellation.Token;
        _moduleImportUtil = moduleImportUtil ?? throw new ArgumentNullException(nameof(moduleImportUtil));
    }

    public ValueTask<IJSObjectReference> Get(string modulePath, string exportName, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed.Value, this);
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(modulePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(exportName);

        if (_modules.TryGetValue(modulePath, out ModuleObjects? module) && module.Objects.TryGetValue(exportName, out IJSObjectReference? reference))
            return new ValueTask<IJSObjectReference>(reference);

        return GetCore(modulePath, exportName, cancellationToken);
    }

    private async ValueTask<IJSObjectReference> GetCore(string modulePath, string exportName, CancellationToken cancellationToken)
    {
        CancellationToken linked = _lifetimeToken.Link(cancellationToken, out CancellationTokenSource? source);
        using (source)
        {
            while (true)
            {
                ModuleObjects module;
                // Only creation of the per-module gate needs a registry-wide lock.
                // Unrelated modules can initialize independently across interop.
                using (await _moduleGate.Lock().ConfigureAwait(false))
                {
                    ObjectDisposedException.ThrowIf(_disposed.Value, this);
                    module = _modules.GetOrAdd(modulePath, static _ => new ModuleObjects());
                }

                await EnterModule(modulePath, module, linked);
                try
                {
                    ObjectDisposedException.ThrowIf(_disposed.Value, this);
                    // A removal may have retired this generation while we waited.
                    if (module.Retired)
                        continue;
                    if (module.Objects.TryGetValue(exportName, out IJSObjectReference? reference))
                        return reference;

                    IJSObjectReference imported = await _moduleImportUtil.GetContentModuleReference(modulePath, linked);
                    reference = await imported.InvokeAsync<IJSObjectReference>(exportName, linked);
                    module.Objects[exportName] = reference;
                    return reference;
                }
                finally
                {
                    if (module.Objects.IsEmpty)
                        RetireModule(modulePath, module);
                    module.Gate.Release();
                }
            }
        }
    }

    public async ValueTask<bool> RemoveObject(string modulePath, string exportName)
    {
        ObjectDisposedException.ThrowIf(_disposed.Value, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(modulePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(exportName);

        if (!_modules.TryGetValue(modulePath, out ModuleObjects? module))
            return false;

        await module.Gate.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(_disposed.Value, this);
            if (!module.Objects.TryRemove(exportName, out IJSObjectReference? reference))
                return false;

            await DisposeReference(reference);
            return true;
        }
        finally
        {
            if (module.Objects.IsEmpty)
                RetireModule(modulePath, module);
            module.Gate.Release();
        }
    }

    public ValueTask<bool> RemoveObjectsForModule(string modulePath, CancellationToken cancellationToken = default)
    {
        return RemoveModuleCore(modulePath, false, cancellationToken);
    }

    public ValueTask<bool> RemoveModuleAndObjects(string modulePath, CancellationToken cancellationToken = default)
    {
        return RemoveModuleCore(modulePath, true, cancellationToken);
    }

    private async ValueTask<bool> RemoveModuleCore(string modulePath, bool removeModule, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed.Value, this);
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(modulePath);

        ModuleObjects module;
        using (await _moduleGate.Lock().ConfigureAwait(false))
        {
            ObjectDisposedException.ThrowIf(_disposed.Value, this);
            // Concurrent getters share this generation until both object and
            // module disposal have finished.
            if (!_modules.TryGetValue(modulePath, out module!))
            {
                if (!removeModule)
                    return false;
                module = new ModuleObjects();
                _modules[modulePath] = module;
            }
        }

        await EnterModule(modulePath, module, cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed.Value, this);
            if (module.Retired)
                return false;
            bool objectsRemoved = false;
            Exception? objectRemovalException = null;
            try
            {
                objectsRemoved = await RemoveObjectsCore(module);
            }
            catch (Exception exception)
            {
                objectRemovalException = exception;
            }

            bool moduleRemoved = false;
            if (removeModule)
            {
                try
                {
                    moduleRemoved = await _moduleImportUtil.DisposeContentModule(modulePath);
                }
                catch (Exception exception) when (objectRemovalException is not null)
                {
                    throw new AggregateException("JavaScript objects and their module could not be fully disposed.", objectRemovalException, exception);
                }
            }

            if (objectRemovalException is not null)
                ExceptionDispatchInfo.Capture(objectRemovalException).Throw();

            return objectsRemoved || moduleRemoved;
        }
        finally
        {
            if (module.Objects.IsEmpty)
                RetireModule(modulePath, module);
            module.Gate.Release();
        }
    }

    private void RetireModule(string modulePath, ModuleObjects module)
    {
        // Called under this generation's gate. Existing waiters will retry against
        // the current generation; removing by identity cannot remove its replacement.
        module.Retired = true;
        _modules.TryRemove(new KeyValuePair<string, ModuleObjects>(modulePath, module));
    }

    private async ValueTask EnterModule(string modulePath, ModuleObjects module, CancellationToken cancellationToken)
    {
        try
        {
            await module.Gate.WaitAsync(cancellationToken);
        }
        catch
        {
            // Cancellation can win just after an empty generation is registered.
            // If another operation owns its gate, that owner handles retirement.
            if (await module.Gate.WaitAsync(0))
            {
                try
                {
                    if (module.Objects.IsEmpty)
                        RetireModule(modulePath, module);
                }
                finally
                {
                    module.Gate.Release();
                }
            }
            throw;
        }
    }

    private static async ValueTask<bool> RemoveObjectsCore(ModuleObjects module)
    {
        bool anyRemoved = false;
        List<Exception>? exceptions = null;
        foreach (KeyValuePair<string, IJSObjectReference> pair in module.Objects)
        {
            if (!module.Objects.TryRemove(pair.Key, out IJSObjectReference? reference))
                continue;
            anyRemoved = true;
            try
            {
                await DisposeReference(reference);
            }
            catch (Exception exception)
            {
                (exceptions ??= []).Add(exception);
            }
        }
        if (exceptions is not null)
            throw new AggregateException("One or more JavaScript objects could not be disposed.", exceptions);
        return anyRemoved;
    }

    private static async ValueTask DisposeReference(IJSObjectReference reference)
    {
        try
        {
            await reference.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async ValueTask CancelLifetime()
    {
        try
        {
            await _lifetimeCancellation.CancelAsync().ConfigureAwait(false);
        }
        catch
        {
            // A cancellation callback must not prevent reference cleanup.
        }
        finally
        {
            _lifetimeCancellation.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        using (await _moduleGate.Lock().ConfigureAwait(false))
        {
            if (!_disposed.TrySetTrue())
                return;
        }

        await CancelLifetime();
        List<Exception>? exceptions = null;
        foreach (ModuleObjects module in _modules.Values)
        {
            await module.Gate.WaitAsync();
            try
            {
                await RemoveObjectsCore(module);
            }
            catch (Exception exception)
            {
                (exceptions ??= []).Add(exception);
            }
            finally
            {
                // Waiters may still hold this gate; SemaphoreSlim has no native
                // resource unless AvailableWaitHandle is used, so let it be collected.
                module.Gate.Release();
            }
        }
        _modules.Clear();
        if (exceptions is not null)
            throw new AggregateException("One or more JavaScript objects could not be disposed.", exceptions);
    }
}
