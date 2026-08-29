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
    /// Gets or creates the JavaScript object reference returned by a parameterless module export.
    /// </summary>
    /// <param name="modulePath">The content module path.</param>
    /// <param name="exportName">The exported JavaScript function name that returns the object instance.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The reference cached for the exact module path and export name.</returns>
    ValueTask<IJSObjectReference> Get(string modulePath, string exportName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes and disposes one cached JavaScript object.
    /// </summary>
    /// <param name="modulePath">Path of the module to use.</param>
    /// <param name="exportName">Name of the export to target.</param>
    /// <returns><see langword="true"/> when a cached object was removed; otherwise, <see langword="false"/>.</returns>
    ValueTask<bool> RemoveObject(string modulePath, string exportName);

    /// <summary>
    /// Removes and disposes every cached JavaScript object created from a module.
    /// </summary>
    /// <param name="modulePath">Path of the module to use.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns><see langword="true"/> when at least one cached object was removed; otherwise, <see langword="false"/>.</returns>
    ValueTask<bool> RemoveObjectsForModule(string modulePath, CancellationToken cancellationToken = default);
    /// <summary>
    /// Removes and disposes every cached JavaScript object created from a module, then evicts the imported module.
    /// </summary>
    /// <param name="modulePath">Path of the module to use.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns><see langword="true"/> when at least one object or the module cache entry was removed; otherwise, <see langword="false"/>.</returns>
    ValueTask<bool> RemoveModuleAndObjects(string modulePath, CancellationToken cancellationToken = default);
}
