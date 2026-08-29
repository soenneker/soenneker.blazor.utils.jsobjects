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
    /// <summary>
    /// Registers JavaScript Object Registry with a scoped lifetime.
    /// </summary>
    /// <param name="services">Service collection that receives the registration.</param>
    /// <returns>The same service collection, so additional registrations can be chained.</returns>
    public static IServiceCollection AddJsObjectRegistryAsScoped(this IServiceCollection services)
    {
        services.AddModuleImportUtilAsScoped();
        services.TryAddScoped<IJsObjectRegistry, JsObjectRegistry>();

        return services;
    }
}
