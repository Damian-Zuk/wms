using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Wms.Application.Picking;
using Wms.Application.Putaway;
using Wms.Application.Putaway.Strategies;
using Wms.Application.Common.Events;
using Wms.Application.Common.Behaviors;
using Wms.Application.Common.Messaging;

namespace Wms.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.Scan(scan => scan.FromAssembliesOf(typeof(DependencyInjection))
            .AddClasses(classes => classes.AssignableTo(typeof(IQueryHandler<,>)), publicOnly: false)
                .AsImplementedInterfaces()
                .WithScopedLifetime()
            .AddClasses(classes => classes.AssignableTo(typeof(ICommandHandler<>)), publicOnly: false)
                .AsImplementedInterfaces()
                .WithScopedLifetime()
            .AddClasses(classes => classes.AssignableTo(typeof(ICommandHandler<,>)), publicOnly: false)
                .AsImplementedInterfaces()
                .WithScopedLifetime()
        );

        services.TryDecorate(typeof(ICommandHandler<,>), typeof(ValidationPipelineBehavior.CommandHandler<,>));
        services.TryDecorate(typeof(ICommandHandler<>), typeof(ValidationPipelineBehavior.CommandBaseHandler<>));

        services.TryDecorate(typeof(IQueryHandler<,>), typeof(LoggingPipelineBehavior.QueryHandler<,>));
        services.TryDecorate(typeof(ICommandHandler<,>), typeof(LoggingPipelineBehavior.CommandHandler<,>));
        services.TryDecorate(typeof(ICommandHandler<>), typeof(LoggingPipelineBehavior.CommandBaseHandler<>));

        services.Scan(scan => scan.FromAssembliesOf(typeof(DependencyInjection))
            .AddClasses(classes => classes.AssignableTo(typeof(IDomainEventHandler<>)), publicOnly: false)
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        var assembly = typeof(DependencyInjection).Assembly;
        services.AddValidatorsFromAssembly(assembly);

        // Multi-location putaway planner (registration order = strategy precedence).
        services.AddScoped<IPutawayAllocationStrategy, FixedLocationAllocationStrategy>();
        services.AddScoped<IPutawayAllocationStrategy, ConsolidateSameSkuAllocationStrategy>();
        services.AddScoped<IPutawayAllocationStrategy, NearestEmptyAllocationStrategy>();
        services.AddScoped<IPutawayPlanner, PutawayPlanner>();

        services.AddScoped<IFefoAllocator, FefoAllocator>();

        return services;
    }
}
