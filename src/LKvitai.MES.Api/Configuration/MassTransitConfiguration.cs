using MassTransit;
using LKvitai.MES.Sagas;

namespace LKvitai.MES.Api.Configuration;

/// <summary>
/// MassTransit configuration per blueprint
/// Saga orchestration and event bus
/// </summary>
public static class MassTransitConfiguration
{
    public static IServiceCollection AddMassTransitConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register ConsumeReservationActivity for DI resolution by the saga
        services.AddScoped<ConsumeReservationActivity>();

        services.AddMassTransit(x =>
        {
            // Register sagas per blueprint with Marten persistence
            // Saga persistence ENABLED per compliance requirement
            x.AddSagaStateMachine<PickStockSaga, PickStockSagaState>()
                .MartenRepository();
            
            x.AddSagaStateMachine<ReceiveGoodsSaga, ReceiveGoodsSagaState>()
                .MartenRepository();
            
            // Additional sagas to be registered:
            // x.AddSagaStateMachine<TransferStockSaga, TransferStockSagaState>()
            //     .MartenRepository();
            
            // Configure transport (in-memory for dev, RabbitMQ for prod)
            if (configuration.GetValue<bool>("UseInMemoryTransport"))
            {
                x.UsingInMemory((context, cfg) =>
                {
                    cfg.ConfigureEndpoints(context);
                });
            }
            else
            {
                x.UsingRabbitMq((context, cfg) =>
                {
                    cfg.Host(configuration["RabbitMQ:Host"] ?? "localhost", h =>
                    {
                        h.Username(configuration["RabbitMQ:Username"] ?? "guest");
                        h.Password(configuration["RabbitMQ:Password"] ?? "guest");
                    });
                    
                    // Use delayed message exchange for durable retry scheduling (prod)
                    cfg.UseDelayedMessageScheduler();
                    cfg.ConfigureEndpoints(context);
                });
            }
        });
        
        return services;
    }
}
