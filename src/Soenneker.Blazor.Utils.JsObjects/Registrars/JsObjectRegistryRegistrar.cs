using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Soenneker.Blazor.Utils.JsObjects.Abstract;
using Soenneker.Blazor.Utils.ModuleImport.Registrars;

namespace Soenneker.Blazor.Utils.JsObjects.Registrars;

/// <summary>
/// Registration for the interop and utility services.
/// </summary>
public static class JsObjectRegistryRegistrar
{
    public static IServiceCollection AddJsObjectRegistryAsScoped(this IServiceCollection services)
    {
        services.AddModuleImportUtilAsScoped();
        services.TryAddScoped<IJsObjectRegistry, JsObjectRegistry>();

        return services;
    }
}