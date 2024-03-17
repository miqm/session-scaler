namespace miqm.sbss
{
    public interface IServiceBusSessionCountProviderFactory
    {
        public Task<IServiceBusSessionCountProvider> ConnectAsync(string connectionString, CancellationToken cancellationToken);
    }

    public interface IServiceBusSessionCountProvider: IAsyncDisposable
    {
        public Task<int> GetQueueSessionsCountAsync(string queue,CancellationToken cancellationToken);
        public Task<int> GetTopicSubscriptionSessionsCountAsync(string topic, string subscription, CancellationToken cancellationToken);

    }
}
