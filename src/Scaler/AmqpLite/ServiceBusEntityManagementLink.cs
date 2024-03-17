using Amqp;
using Amqp.Framing;
using Amqp.Types;

namespace miqm.sbss.AmqpLite;

internal class ServiceBusEntityManagementLink : IAsyncDisposable, IDisposable
{
    private readonly int _commandTimeout;
    private readonly SenderLink _sender;
    private readonly ReceiverLink _receiver;
    private readonly string _clientNode = Guid.NewGuid().ToString();

    public ServiceBusEntityManagementLink(Session session, string entity, int commandTimeout = 30)
    {
        _commandTimeout = commandTimeout;
        var entityManagement = $"{entity}/$management";

        _sender = new(
            session,
            $"mgmt-sender-{_clientNode}",
            new Attach()
            {
                Source = new Source() { Address = _clientNode },
                Target = new Target() { Address = entityManagement }
            }, null);

        _receiver = new(
            session,
            $"mgmt-receiver-{_clientNode}",
            new Attach()
            {
                Source = new Source() { Address = entityManagement },
                Target = new Target() { Address = _clientNode }
            }, null);
    }

    public async Task<ICollection<string>> GetMessageSessionsAsync(int skip, int top = 100, CancellationToken cancellationToken = default)
    {
        await _sender.SendAsync(new()
        {
            Properties = new()
            {
                MessageId = Guid.NewGuid().ToString(),
                ReplyTo = _clientNode
            },
            ApplicationProperties = new()
            {
                ["operation"] = "com.microsoft:get-message-sessions"
            },
            BodySection = new AmqpValue()
            {
                Value = new Map()
                {
                    {"last-updated-time", DateTime.MaxValue},
                    {"skip", skip},
                    {"top", top}
                }
            }
        });

        var response = await _receiver.ReceiveAsync(TimeSpan.FromSeconds(_commandTimeout)) ?? throw new ApplicationException("No response received");

        _receiver.Accept(response);
        cancellationToken.ThrowIfCancellationRequested();
        var resultCode = (int)response.ApplicationProperties.Map["statusCode"];
        return resultCode switch
        {
            204 => [],
            200 => (string[]) ((Map) response.Body)["sessions-ids"],
            _ => throw new ApplicationException($"Unexpected response code {resultCode}")
        };
    } 

    private async Task ReleaseUnmanagedResources()
    {
        var ex = new List<Exception>();
        if (!_sender.IsClosed)
        {
            try
            {
                await _sender.CloseAsync();
            }
            catch (Exception e)
            {
                ex.Add(e);
            }
        }

        if (!_receiver.IsClosed)
        {
            try
            {
                await _receiver.CloseAsync();
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

    public void Dispose()
    {
        ReleaseUnmanagedResources().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        await ReleaseUnmanagedResources();
        GC.SuppressFinalize(this);
    }

    ~ServiceBusEntityManagementLink()
    {
        ReleaseUnmanagedResources().GetAwaiter().GetResult();
    }
}