using Microsoft.JSInterop;
using Soenneker.Atomics.ValueBools;
using Soenneker.Blazor.Utils.JsObjects.Abstract;
using Soenneker.Blazor.Utils.ModuleImport.Abstract;
using Soenneker.Dictionaries.Singletons;
using Soenneker.Extensions.ValueTask;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Blazor.Utils.JsObjects;

/// <inheritdoc cref="IJsObjectRegistry"/>
public sealed class JsObjectRegistry : IJsObjectRegistry
{
    private readonly IModuleImportUtil _moduleImportUtil;

    private readonly SingletonDictionary<IJSObjectReference> _objects;
    private ValueAtomicBool _disposed;

    public JsObjectRegistry(IModuleImportUtil moduleImportUtil)
    {
        _moduleImportUtil = moduleImportUtil;

        _objects = new SingletonDictionary<IJSObjectReference>(async (key, cancellationToken) =>
        {
            (string modulePath, string exportName) = ParseKey(key);

            IJSObjectReference module = await _moduleImportUtil.GetContentModuleReference(modulePath, cancellationToken)
                                                               .NoSync();

            return await module.InvokeAsync<IJSObjectReference>(exportName, cancellationToken)
                               .NoSync();
        });
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<IJSObjectReference> Get(string modulePath, string exportName, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed.Value, this);

        ArgumentException.ThrowIfNullOrWhiteSpace(modulePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(exportName);

        string key = CreateKey(modulePath, exportName);
        return _objects.Get(key, cancellationToken);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string CreateKey(string modulePath, string exportName)
    {
        return string.Create(modulePath.Length + exportName.Length + 1, (modulePath, exportName), static (span, state) =>
        {
            state.modulePath.AsSpan()
                 .CopyTo(span);
            span[state.modulePath.Length] = '|';

            state.exportName.AsSpan()
                 .CopyTo(span[(state.modulePath.Length + 1)..]);
        });
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (string modulePath, string exportName) ParseKey(string key)
    {
        int separatorIndex = key.IndexOf('|');

        string modulePath = key[..separatorIndex];
        string exportName = key[(separatorIndex + 1)..];

        return (modulePath, exportName);
    }

    public async ValueTask<bool> RemoveObject(string modulePath, string exportName)
    {
        ObjectDisposedException.ThrowIf(_disposed.Value, this);

        ArgumentException.ThrowIfNullOrWhiteSpace(modulePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(exportName);

        string key = CreateKey(modulePath, exportName);

        return await _objects.TryRemoveAndDispose(key);
    }

    public async ValueTask<bool> RemoveObjectsForModule(string modulePath, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed.Value, this);

        ArgumentException.ThrowIfNullOrWhiteSpace(modulePath);

        var anyRemoved = false;

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
                await jsObject.DisposeAsync();
            }
            catch
            {
            }
        }

        return anyRemoved;
    }

    public async ValueTask<bool> RemoveModuleAndObjects(string modulePath, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed.Value, this);

        ArgumentException.ThrowIfNullOrWhiteSpace(modulePath);

        bool objectsRemoved = await RemoveObjectsForModule(modulePath, cancellationToken);

        try
        {
            await _moduleImportUtil.DisposeContentModule(modulePath);
        }
        catch
        {
        }

        return objectsRemoved;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool KeyMatchesModule(string key, string modulePath)
    {
        int separatorIndex = key.IndexOf('|');

        if (separatorIndex < 0)
            return key.Equals(modulePath, StringComparison.Ordinal);

        return key.AsSpan(0, separatorIndex)
                  .SequenceEqual(modulePath);
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed.TrySetTrue())
            return;

        await _objects.DisposeAsync();
    }
}