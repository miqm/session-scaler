namespace miqm.sbss.AmqpLite;

public class ServiceBusSessionCountProviderFactory : IServiceBusSessionCountProviderFactory
{
    public Task<IServiceBusSessionCountProvider> ConnectAsync(string connectionString, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult((IServiceBusSessionCountProvider)ServiceBusClient.CreateFromConnectionString(connectionString));
    }
}