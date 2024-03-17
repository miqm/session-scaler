using Externalscaler;
using Grpc.Core;

namespace miqm.sbss;

public class ServiceBusSessionService(IServiceBusSessionCountProviderFactory sessionCountProviderFactory, ILogger<ServiceBusSessionService> logger)
    : ExternalScaler.ExternalScalerBase
{
    private const string MetricName = "sessionsCount";

    /// <summary>
    /// GetMetricSpec returns the target value for the HPA definition for the scaler.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    public override Task<GetMetricSpecResponse> GetMetricSpec(ScaledObjectRef request, ServerCallContext context)
    {
        var metadata = GetScalerMetadata(request);

        var resp = new GetMetricSpecResponse();

        resp.MetricSpecs.Add(new MetricSpec
        {
            MetricName = MetricName,
            TargetSize = metadata.SessionCount
        });
        return Task.FromResult(resp);
    }

    /// <summary>
    /// GetMetrics returns the value of the metric referred to from GetMetricSpec
    /// </summary>
    /// <param name="request"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    public override async Task<GetMetricsResponse> GetMetrics(GetMetricsRequest request, ServerCallContext context)
    {
        if (request.MetricName != MetricName)
        {
            return new();
        }
        var metadata = GetScalerMetadata(request.ScaledObjectRef);

        var sessionCount = await GetSessionCountAsync(metadata, context.CancellationToken);
        return new()
        {
            MetricValues = {
                new MetricValue()
                {
                    MetricName = MetricName,
                    MetricValue_ = sessionCount
                }
            }
        };
    }

    /// <summary>
    /// the IsActive method in the GRPC interface is called every pollingInterval with a ScaledObjectRef object that contains the scaledObject name, namespace, and scaler metadata.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    public override async Task<IsActiveResponse> IsActive(ScaledObjectRef request, ServerCallContext context)
    {
        var metadata = GetScalerMetadata(request);

        var sessionCount = await GetSessionCountAsync(metadata, context.CancellationToken);

        return new()
        {
            Result = sessionCount > metadata.ActivationSessionCount
        };
    }
    /// <summary>
    /// Unlike IsActive, StreamIsActive is called once when KEDA reconciles the ScaledObject,
    /// and expects the external scaler to maintain a long-lived connection and push IsActiveResponse objects whenever the scaler needs KEDA to activate the deployment.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="responseStream"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    public override async Task StreamIsActive(ScaledObjectRef request, IServerStreamWriter<IsActiveResponse> responseStream, ServerCallContext context)
    {
        var metadata = GetScalerMetadata(request);
        var sleep = Math.Max(5, Math.Min(600, int.TryParse(metadata.ActivationPollInterval, out var interval) ? interval : 600));

        while (!context.CancellationToken.IsCancellationRequested)
        {
            try
            {
                await responseStream.WriteAsync(new()
                {
                    Result = await GetSessionCountAsync(metadata, context.CancellationToken) > metadata.ActivationSessionCount
                });

                await Task.Delay(TimeSpan.FromSeconds(sleep), context.CancellationToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to retrieve session count");
            }
        }
    }

    private static ScalerMetadata GetScalerMetadata(ScaledObjectRef request) =>
        ScalerMetadata.Create(request) ?? throw new RpcException(new(StatusCode.InvalidArgument, "Invalid metadata"));

    private async Task<int> GetSessionCountAsync(ScalerMetadata metadata, CancellationToken cancellationToken)
    {
        var connectionString = Environment.GetEnvironmentVariable(metadata.ConnectionStringSetting) ??
                               throw new RpcException(new(StatusCode.InvalidArgument,
                                   $"Environment variable {metadata.ConnectionStringSetting} is missing"));

        var topicScaling = string.IsNullOrEmpty(metadata.QueueName);
        if (topicScaling && (string.IsNullOrEmpty(metadata.TopicName) || string.IsNullOrEmpty(metadata.SubscriptionName)))
        {
            throw new RpcException(new(StatusCode.InvalidArgument, "queueName or topicName and subscriptionName have to be provided"));
        }

        await using var service = await sessionCountProviderFactory.ConnectAsync(connectionString, cancellationToken);

        int sessionCount;
        try
        {
            if (topicScaling)
            {
                sessionCount = await service.GetTopicSubscriptionSessionsCountAsync(metadata.TopicName!,
                    metadata.SubscriptionName!, cancellationToken);
            }
            else
            {
                sessionCount = await service.GetQueueSessionsCountAsync(metadata.QueueName!, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            throw new RpcException(new(StatusCode.Internal, $"Failed to retrieve session count: {ex.Message}", ex));
        }
        logger.LogDebug("Obtained {sessionCount} sessions for entity {entityName}", sessionCount, metadata.QueueName ?? (metadata.TopicName ?? string.Empty) + "/Subscriptions/" + (metadata.SubscriptionName ?? string.Empty));
        return sessionCount;

    }

}