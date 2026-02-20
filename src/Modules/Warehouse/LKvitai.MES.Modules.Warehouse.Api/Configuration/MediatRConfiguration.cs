using LKvitai.MES.Modules.Warehouse.Application.Behaviors;
using LKvitai.MES.Modules.Warehouse.Application.Commands;
using MediatR;

namespace LKvitai.MES.Api.Configuration;

/// <summary>
/// MediatR command pipeline configuration per blueprint
/// </summary>
public static class MediatRConfiguration
{
    public static IServiceCollection AddMediatRPipeline(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
            // Also scan Application assembly for command handlers
            cfg.RegisterServicesFromAssemblyContaining<StartPickingCommand>();
            
            // Add pipeline behaviors per blueprint
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(IdempotencyBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        });
        
        return services;
    }
}
