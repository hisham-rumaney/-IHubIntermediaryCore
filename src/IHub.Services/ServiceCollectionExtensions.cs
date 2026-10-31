using IHub.Application.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;

namespace IHub.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddServicesLayer(this IServiceCollection services)
    {
        services.AddScoped<IHubActivityService, HubActivityService>();
        return services;
    }
}