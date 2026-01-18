namespace Common.Applications.NpOn.CommonApplication.Services;

/// <summary>
/// A generic hosted service wrapper to manage the lifecycle of any consumer.
/// This allows consumers to be registered as background services with the DI container,
/// ensuring they are created when the application starts.
/// </summary>
/// <typeparam name="TConsumer">The type of the consumer to host.</typeparam>
public class ConsumerHostedService<TConsumer>(IServiceProvider serviceProvider) : IHostedService where TConsumer : class
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // When the application starts, get the consumer instance from DI.
        // This will trigger its constructor, which in turn starts the RabbitMQ/Kafka listening process.
        serviceProvider.GetRequiredService<TConsumer>();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}