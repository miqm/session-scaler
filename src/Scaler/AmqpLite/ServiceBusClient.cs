using Amqp;

namespace miqm.sbss.AmqpLite
{
    public class ServiceBusClient : IDisposable, IServiceBusSessionCountProvider
    {
        private readonly Connection _connection;
        private readonly Session _session;

        private ServiceBusClient(Address address)
        {
            _connection = new(address);
            _session = new(_connection);
        }

        public static ServiceBusClient CreateFromConnectionString(string connectionString)
        {

            Address address;
            try
            {
                var cDict = connectionString.Split(';').ToDictionary(x => x[..x.IndexOf('=')], x => x[(x.IndexOf('=')+1)..]);
                address = new(new Uri(cDict["Endpoint"]).Host, 5671, cDict["SharedAccessKeyName"], cDict["SharedAccessKey"]);
            }
            catch (Exception e)
            {
                throw new ArgumentException("Invalid connection string", e);
            }

            try
            {
                return new(address);
            }
            catch (Exception e)
            {
                throw new ConnectionException("Failed to create ServiceBusClient", e);
            }

        }

        private async Task ReleaseUnmanagedResourcesAsync()
        {
            var ex = new List<Exception>();
            if (!_session.IsClosed)
            {
                try
                {
                    await _session.CloseAsync();
                }
                catch (Exception e)
                {
                    ex.Add(e);
                }
            }

            if (!_connection.IsClosed)
            {
                try
                {
                    await _connection.CloseAsync();
                }
                catch (Exception e)
                {
                    ex.Add(e);
                }
            }

            if (ex.Count > 0)
            {
                throw new AggregateException(ex);
            }
        }

        private ServiceBusEntityManagementLink OpenEntityManagementLink(string entity, int timeout = 30)
        {
            return new(_session, entity, timeout);
        }


        public void Dispose()
        {
            ReleaseUnmanagedResourcesAsync().GetAwaiter().GetResult();
            GC.SuppressFinalize(this);
        }

        public async ValueTask DisposeAsync()
        {
            await ReleaseUnmanagedResourcesAsync();
            GC.SuppressFinalize(this);
        }

        ~ServiceBusClient()
        {
            ReleaseUnmanagedResourcesAsync().GetAwaiter().GetResult();
        }

        public async Task<int> GetQueueSessionsCountAsync(string queue, CancellationToken cancellationToken)
        {
            await using var link = OpenEntityManagementLink(queue);
            return await CountSessionsAsync(link, cancellationToken);

        }


        public async Task<int> GetTopicSubscriptionSessionsCountAsync(string topic, string subscription, CancellationToken cancellationToken)
        {
            await using var link = OpenEntityManagementLink($"{topic}/subscriptions/{subscription}");
            return await CountSessionsAsync(link, cancellationToken);
        }

        private static async Task<int> CountSessionsAsync(ServiceBusEntityManagementLink link, CancellationToken cancellationToken)
        {
            const int top = 100;
            var skip = 0;
            var count = 0;
            while (!cancellationToken.IsCancellationRequested)
            {
                var listSessions = await link.GetMessageSessionsAsync(skip, top, cancellationToken);
                
                if (listSessions.Count == 0)
                {
                    break;
                }

                count += listSessions.Count;
                skip += top;
            }
            return count;
        }
    }
}
