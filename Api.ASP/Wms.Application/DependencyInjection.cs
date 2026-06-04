using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Wms.Application.Picking;
using Wms.Application.Picking.Strategies;
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
        // Command and query handlers
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

        // Decorate handlers with pipeline behaviors
        services.TryDecorate(typeof(ICommandHandler<,>), typeof(ValidationPipelineBehavior.CommandHandler<,>));
        services.TryDecorate(typeof(ICommandHandler<>), typeof(ValidationPipelineBehavior.CommandBaseHandler<>));

        services.TryDecorate(typeof(IQueryHandler<,>), typeof(LoggingPipelineBehavior.QueryHandler<,>));
        services.TryDecorate(typeof(ICommandHandler<,>), typeof(LoggingPipelineBehavior.CommandHandler<,>));
        services.TryDecorate(typeof(ICommandHandler<>), typeof(LoggingPipelineBehavior.CommandBaseHandler<>));

        // Domain event handlers
        services.Scan(scan => scan.FromAssembliesOf(typeof(DependencyInjection))
            .AddClasses(classes => classes.AssignableTo(typeof(IDomainEventHandler<>)), publicOnly: false)
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        // Validators
        var assembly = typeof(DependencyInjection).Assembly;
        services.AddValidatorsFromAssembly(assembly);

        // Putaway planner
        services.AddScoped<IPutawayAllocationStrategy, PreferredLocationAllocationStrategy>();
        services.AddScoped<IPutawayAllocationStrategy, ConsolidateSameLotAllocationStrategy>();
        services.AddScoped<IPutawayAllocationStrategy, ConsolidateSameSkuAllocationStrategy>();
        services.AddScoped<IPutawayAllocationStrategy, ProximityAllocationStrategy>();
        services.AddScoped<IPutawayAllocationStrategy, NearestEmptyAllocationStrategy>();
        services.AddScoped<IPutawayAllocationStrategy, NearestAvailableAllocationStrategy>();
        services.AddScoped<IPutawayPlanner, PutawayPlanner>();

        // Picking planner
        services.AddScoped<IPickingAllocationStrategy, FefoAllocationStrategy>();
        services.AddScoped<IPickingAllocationStrategy, FifoAllocationStrategy>();
        services.AddScoped<IPickingPlanner, PickingPlanner>();

        return services;
    }
}
