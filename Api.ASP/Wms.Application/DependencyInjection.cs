using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Wms.Application.Abstractions.Behaviors;
using Wms.Application.Abstractions.Messaging;
using Wms.Application.Abstractions.DomainEvents;
using Wms.Application.Putaway;
using Wms.Application.Putaway.Strategies;

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

        services.AddScoped<IPutawayStrategy, FixedLocationStrategy>();
        services.AddScoped<IPutawayStrategy, ConsolidateSameSkuStrategy>();
        services.AddScoped<IPutawayStrategy, NearestEmptyStrategy>();
        services.AddScoped<IPutawayService, PutawayService>();

        return services;
    }
}
