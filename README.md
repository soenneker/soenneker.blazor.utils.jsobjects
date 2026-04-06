[![](https://img.shields.io/nuget/v/soenneker.blazor.utils.jsobjects.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.blazor.utils.jsobjects/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.blazor.utils.jsobjects/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.blazor.utils.jsobjects/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/soenneker.blazor.utils.jsobjects.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.blazor.utils.jsobjects/)
[![](https://img.shields.io/badge/Demo-Live-blueviolet?style=for-the-badge\&logo=github)](https://soenneker.github.io/soenneker.blazor.utils.jsobjects)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.blazor.utils.jsobjects/codeql.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.blazor.utils.jsobjects/actions/workflows/codeql.yml)

# ![](https://user-images.githubusercontent.com/4441470/224455560-91ed3ee7-f510-4041-a8d2-3fc093025112.png) Soenneker.Blazor.Utils.JsObjects

### Centralized registry for loading, caching, and reusing JavaScript object instances via Blazor interop

---

## Installation

```bash
dotnet add package Soenneker.Blazor.Utils.JsObjects
```

---

## What this solves

Blazor interop typically leads to:

* Re-importing modules repeatedly
* Creating duplicate JS instances
* Manual lifecycle/disposal headaches
* Scattered `IJSRuntime` usage everywhere

This library gives you a **central registry** that:

* Caches JS object instances per `(module + export)`
* Ensures **single instance per key**
* Handles async creation safely
* Allows targeted eviction + cleanup
* Keeps your components clean

---

## How it works

Internally, objects are keyed like:

```
[modulePath]|[exportName]
```

And resolved like this:

1. Import module (via `IModuleImportUtil`)
2. Call exported function
3. Cache returned `IJSObjectReference`

Example from implementation: 

---

## JavaScript pattern (IMPORTANT)

Your module should return an object instance:

```js
// myModule.js

export function createInstance() {
    return {
        sayHello() {
            console.log("Hello from JS");
        }
    };
}
```

---

## Basic usage

### Inject

```csharp
public sealed class MyService
{
    private readonly IJsObjectRegistry _registry;

    public MyService(IJsObjectRegistry registry)
    {
        _registry = registry;
    }
}
```

---

### Get (or create) a JS object

```csharp
IJSObjectReference obj = await _registry.Get(
    "js/myModule.js",
    "createInstance",
    cancellationToken
);
```

This will:

* Import module (once)
* Call `createInstance`
* Cache result
* Return same instance on future calls

---

### Call methods on the object

```csharp
await obj.InvokeVoidAsync("sayHello");
```

---

## Example: Real-world wrapper

This is how you *should* use it — wrap JS in a typed service.

```csharp
public sealed class MyJsClient
{
    private const string Module = "js/myModule.js";
    private const string Export = "createInstance";

    private readonly IJsObjectRegistry _registry;

    public MyJsClient(IJsObjectRegistry registry)
    {
        _registry = registry;
    }

    public async ValueTask SayHello(CancellationToken ct = default)
    {
        var obj = await _registry.Get(Module, Export, ct);
        await obj.InvokeVoidAsync("sayHello", ct);
    }
}
```

Now your Blazor components never touch JS directly.

---

## Removing objects

### Remove a single instance

```csharp
await _registry.RemoveObject("js/myModule.js", "createInstance");
```

---

### Remove all objects for a module

```csharp
await _registry.RemoveObjectsForModule("js/myModule.js");
```

---

### Remove module + all objects

```csharp
await _registry.RemoveModuleAndObjects("js/myModule.js");
```

This:

* Disposes all instances
* Disposes imported module

---

## Lifecycle guidance

Use removal when:

* Underlying JS state becomes invalid
* Module is reloaded
* You want to force a fresh instance

Otherwise, **leave it cached**.

---

## Design notes

* Thread-safe via `SingletonDictionary`
* Zero duplicate instance creation
* Optimized key creation (`string.Create`)
* Minimal allocations
* Explicit disposal control

---

## When NOT to use this

Don’t use this for:

* Stateless JS calls
* One-off interop
* Simple `InvokeAsync` usage

This is for **stateful JS objects** only.

---