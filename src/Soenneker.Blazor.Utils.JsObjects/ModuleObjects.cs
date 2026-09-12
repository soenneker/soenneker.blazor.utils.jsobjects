using Microsoft.JSInterop;
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Soenneker.Blazor.Utils.JsObjects;

internal sealed class ModuleObjects
{
    internal bool Retired;
    internal readonly SemaphoreSlim Gate = new(1, 1);
    internal readonly ConcurrentDictionary<string, IJSObjectReference> Objects = new(1, 4, StringComparer.Ordinal);
}
