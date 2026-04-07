using Microsoft.Extensions.DependencyInjection;

namespace Wms.Application.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection TryDecorate(
        this IServiceCollection services,
        Type serviceType,
        Type decoratorType)
    {
        if (services.Any(s =>
            s.ServiceType.IsGenericType
                ? s.ServiceType.GetGenericTypeDefinition() == serviceType
                : s.ServiceType == serviceType))
        {
            services.Decorate(serviceType, decoratorType);
        }

        return services;
    }
}
