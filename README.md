[![](https://img.shields.io/nuget/v/soenneker.blazor.utils.jsobjects.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.blazor.utils.jsobjects/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.blazor.utils.jsobjects/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.blazor.utils.jsobjects/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/soenneker.blazor.utils.jsobjects.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.blazor.utils.jsobjects/)
[![](https://img.shields.io/badge/Demo-Live-blueviolet?style=for-the-badge\&logo=github)](https://soenneker.github.io/soenneker.blazor.utils.jsobjects)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.blazor.utils.jsobjects/codeql.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.blazor.utils.jsobjects/actions/workflows/codeql.yml)

# Soenneker.Blazor.Utils.JsObjects

A scoped registry for creating, caching, and disposing stateful JavaScript object references returned by ES module exports.

Use it when an exported factory creates a JavaScript object whose state should be reused across multiple Blazor interop calls. Stateless module functions do not need this registry.

## Installation

```bash
dotnet add package Soenneker.Blazor.Utils.JsObjects
```

```csharp
using Soenneker.Blazor.Utils.JsObjects.Registrars;

builder.Services.AddJsObjectRegistryAsScoped();
```

Inject `IJsObjectRegistry` into the component or service that wraps the JavaScript API.

## JavaScript factory

The export must take no arguments and return an object. The object’s functions become methods on the resulting `IJSObjectReference`:

```javascript
export function createCounter() {
    let value = 0;

    return {
        increment(step = 1) {
            value += step;
            return value;
        },
        reset() {
            value = 0;
        }
    };
}
```

Place the module in the application’s static web assets, for example `wwwroot/js/counter.js`.

## Wrap the object in a typed service

```csharp
using Microsoft.JSInterop;
using Soenneker.Blazor.Utils.JsObjects.Abstract;

public sealed class CounterClient(IJsObjectRegistry objects)
{
    private const string ModulePath = "/js/counter.js";
    private const string Factory = "createCounter";

    public async ValueTask<int> Increment(
        int step,
        CancellationToken cancellationToken = default)
    {
        IJSObjectReference counter =
            await objects.Get(ModulePath, Factory, cancellationToken);

        return await counter.InvokeAsync<int>(
            "increment",
            cancellationToken,
            step);
    }
}
```

Call browser interop after interactive rendering. The same registry scope returns the same object reference for repeated calls with the same exact module path and export name. Different spellings, relative paths, query strings, or export names form different cache entries.

The registry owns returned references. Do not dispose them directly; remove them through the registry so its cache cannot return a disposed handle.

## Reset cached state

Remove one factory result when that object’s JavaScript state is no longer valid:

```csharp
bool removed = await objects.RemoveObject(
    "/js/counter.js",
    "createCounter");
```

The next `Get` for that pair calls the factory again.

Remove every cached object created from a module while leaving the imported module available:

```csharp
await objects.RemoveObjectsForModule("/js/counter.js", cancellationToken);
```

Or remove the objects and evict the imported module:

```csharp
bool anythingRemoved = await objects.RemoveModuleAndObjects(
    "/js/counter.js",
    cancellationToken);
```

The last method returns true when at least one object or the module cache entry was removed. Use module-wide removal only when this registry is the exclusive owner of that module cache entry; disposing a shared imported module can invalidate other consumers.

Creation and removal operations are serialized within the registry so a module eviction cannot race a new cached object creation. Do not invoke a previously returned reference after removing it.

## Lifetime and failures

In Blazor Server, scoped state normally lasts for the circuit. In WebAssembly, a scoped service normally lasts for the application. Remove short-lived objects when their owning widget is destroyed; remaining cached references are disposed with the registry scope.

A cancellation token cancels waiting for creation or removal, but it cannot undo JavaScript work that already completed. Factory import errors, missing exports, non-object return values, and JavaScript exceptions propagate to the caller.

Keep module paths and export names as trusted application constants. Dynamic module import executes code in the page, so never derive either value directly from user input. Returned values and callbacks that originate in JavaScript remain untrusted data and require normal validation.
