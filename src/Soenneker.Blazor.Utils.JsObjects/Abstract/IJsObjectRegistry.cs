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

    /// <summary>
    /// Removes object.
    /// </summary>
    /// <param name="modulePath">Path of the module to use.</param>
    /// <param name="exportName">Name of the export to target.</param>
    /// <returns>true if removes object; otherwise, false.</returns>
    ValueTask<bool> RemoveObject(string modulePath, string exportName);

    /// <summary>
    /// Removes objects for module.
    /// </summary>
    /// <param name="modulePath">Path of the module to use.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>true if removes objects for module; otherwise, false.</returns>
    ValueTask<bool> RemoveObjectsForModule(string modulePath, CancellationToken cancellationToken = default);
    /// <summary>
    /// Removes module and objects.
    /// </summary>
    /// <param name="modulePath">Path of the module to use.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>true if removes module and objects; otherwise, false.</returns>
    ValueTask<bool> RemoveModuleAndObjects(string modulePath, CancellationToken cancellationToken = default);
}
