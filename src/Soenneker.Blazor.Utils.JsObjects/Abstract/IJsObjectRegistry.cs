using Microsoft.JSInterop;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Blazor.Utils.JsObjects.Abstract;

/// <summary>
/// Centralized registry for loading, caching, and reusing JavaScript object instances via Blazor interop.
/// </summary>
public interface IJsObjectRegistry : IAsyncDisposable
{
    /// <summary>
    /// Gets a cached JavaScript object reference from the specified module using the provided exported getter name.
    /// </summary>
    /// <param name="modulePath">The content module path.</param>
    /// <param name="exportName">The exported JavaScript function name that returns the object instance.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A cached <see cref="IJSObjectReference"/>.</returns>
    ValueTask<IJSObjectReference> Get(string modulePath, string exportName, CancellationToken cancellationToken = default);

    ValueTask<bool> RemoveObject(string modulePath, string exportName);

    ValueTask<bool> RemoveObjectsForModule(string modulePath, CancellationToken cancellationToken = default);
    ValueTask<bool> RemoveModuleAndObjects(string modulePath, CancellationToken cancellationToken = default);
}