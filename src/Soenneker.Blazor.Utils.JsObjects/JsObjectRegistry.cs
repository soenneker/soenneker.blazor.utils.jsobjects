using Microsoft.JSInterop;
using Soenneker.Atomics.ValueBools;
using Soenneker.Blazor.Utils.JsObjects.Abstract;
using Soenneker.Blazor.Utils.ModuleImport.Abstract;
using Soenneker.Dictionaries.Singletons;
using Soenneker.Extensions.ValueTask;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Blazor.Utils.JsObjects;

/// <inheritdoc cref="IJsObjectRegistry"/>
public sealed class JsObjectRegistry : IJsObjectRegistry
{
    private readonly IModuleImportUtil _moduleImportUtil;

    private readonly SingletonDictionary<IJSObjectReference> _objects;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ValueAtomicBool _disposed;

    public JsObjectRegistry(IModuleImportUtil moduleImportUtil)
    {
        _moduleImportUtil = moduleImportUtil ?? throw new ArgumentNullException(nameof(moduleImportUtil));

        _objects = new SingletonDictionary<IJSObjectReference>(async (key, cancellationToken) =>
        {
            (string modulePath, string exportName) = ParseKey(key);

            IJSObjectReference module = await _moduleImportUtil.GetContentModuleReference(modulePath, cancellationToken)
                                                               .NoSync();

            return await module.InvokeAsync<IJSObjectReference>(exportName, cancellationToken)
                               .NoSync();
        });
    }

    public async ValueTask<IJSObjectReference> Get(string modulePath, string exportName, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed.Value, this);

        ArgumentException.ThrowIfNullOrWhiteSpace(modulePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(exportName);

        await _gate.WaitAsync(cancellationToken);

        try
        {
            ObjectDisposedException.ThrowIf(_disposed.Value, this);
            string key = CreateKey(modulePath, exportName);
            return await _objects.Get(key, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string CreateKey(string modulePath, string exportName)
    {
        return string.Concat(modulePath.Length.ToString(CultureInfo.InvariantCulture), ":", modulePath, exportName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (string modulePath, string exportName) ParseKey(string key)
    {
        int separatorIndex = key.IndexOf(':');

        if (separatorIndex <= 0 || !int.TryParse(key.AsSpan(0, separatorIndex), NumberStyles.None, CultureInfo.InvariantCulture, out int moduleLength))
            throw new InvalidOperationException("The JavaScript object cache contains an invalid key.");

        int moduleStart = separatorIndex + 1;

        if (moduleLength < 0 || moduleStart + moduleLength > key.Length)
            throw new InvalidOperationException("The JavaScript object cache contains an invalid key.");

        string modulePath = key.Substring(moduleStart, moduleLength);
        string exportName = key[(moduleStart + moduleLength)..];

        return (modulePath, exportName);
    }

    public async ValueTask<bool> RemoveObject(string modulePath, string exportName)
    {
        ObjectDisposedException.ThrowIf(_disposed.Value, this);

        ArgumentException.ThrowIfNullOrWhiteSpace(modulePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(exportName);

        await _gate.WaitAsync();

        try
        {
            ObjectDisposedException.ThrowIf(_disposed.Value, this);
            string key = CreateKey(modulePath, exportName);

            if (!_objects.TryRemove(key, out IJSObjectReference? jsObject) || jsObject is null)
                return false;

            await DisposeReference(jsObject);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask<bool> RemoveObjectsForModule(string modulePath, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed.Value, this);

        ArgumentException.ThrowIfNullOrWhiteSpace(modulePath);

        await _gate.WaitAsync(cancellationToken);

        try
        {
            ObjectDisposedException.ThrowIf(_disposed.Value, this);
            return await RemoveObjectsForModuleCore(modulePath, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async ValueTask<bool> RemoveObjectsForModuleCore(string modulePath, CancellationToken cancellationToken)
    {
        var anyRemoved = false;
        List<Exception>? exceptions = null;
        Dictionary<string, IJSObjectReference> all = await _objects.GetAll(cancellationToken);

        foreach (KeyValuePair<string, IJSObjectReference> pair in all)
        {
            if (!KeyMatchesModule(pair.Key, modulePath))
                continue;

            if (!_objects.TryRemove(pair.Key, out IJSObjectReference? jsObject) || jsObject is null)
                continue;

            anyRemoved = true;

            try
            {
                await DisposeReference(jsObject);
            }
            catch (Exception exception)
            {
                exceptions ??= [];
                exceptions.Add(exception);
            }
        }

        if (exceptions is not null)
            throw new AggregateException("One or more JavaScript objects could not be disposed.", exceptions);

        return anyRemoved;
    }

    public async ValueTask<bool> RemoveModuleAndObjects(string modulePath, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed.Value, this);

        ArgumentException.ThrowIfNullOrWhiteSpace(modulePath);

        await _gate.WaitAsync(cancellationToken);

        try
        {
            ObjectDisposedException.ThrowIf(_disposed.Value, this);
            var objectsRemoved = false;
            Exception? objectRemovalException = null;

            try
            {
                objectsRemoved = await RemoveObjectsForModuleCore(modulePath, cancellationToken);
            }
            catch (Exception exception)
            {
                objectRemovalException = exception;
            }

            bool moduleRemoved;

            try
            {
                moduleRemoved = await _moduleImportUtil.DisposeContentModule(modulePath);
            }
            catch (Exception moduleException) when (objectRemovalException is not null)
            {
                throw new AggregateException("JavaScript objects and their module could not be fully disposed.", objectRemovalException, moduleException);
            }

            if (objectRemovalException is not null)
                ExceptionDispatchInfo.Capture(objectRemovalException).Throw();

            return objectsRemoved || moduleRemoved;
        }
        finally
        {
            _gate.Release();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool KeyMatchesModule(string key, string modulePath)
    {
        (string cachedModulePath, _) = ParseKey(key);
        return cachedModulePath.Equals(modulePath, StringComparison.Ordinal);
    }

    private static async ValueTask DisposeReference(IJSObjectReference jsObject)
    {
        try
        {
            await jsObject.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    /// <summary>
    /// Asynchronously releases resources used by the current instance.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async ValueTask DisposeAsync()
    {
        if (!_disposed.TrySetTrue())
            return;

        await _gate.WaitAsync();

        try
        {
            await _objects.DisposeAsync();
        }
        finally
        {
            _gate.Release();
        }
    }
}
