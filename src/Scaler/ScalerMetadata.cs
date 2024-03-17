using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Externalscaler;
using static System.Text.Json.JsonSerializer;

namespace miqm.sbss
{
    internal sealed class ScalerMetadata
    {
        /// <summary>
        /// Optional. Environment variable name to read connection string with SharedAccessKey from. Default is SERVICE_BUS_CONNECTION_STRING. Unlike regular KEDA scalers, the environment variable is to be set on the scaler pod, not on the scaled deployment.
        /// </summary>
        public string ConnectionStringSetting { get; set; } = "SERVICE_BUS_CONNECTION_STRING";
        /// <summary>
        /// Optional. Service Bus Queue to scale on. This or TopicName and SubscriptionName has to be provided. Takes precedence over TopicName and SubscriptionName.
        /// </summary>
        public string? QueueName { get; set; }
        /// <summary>
        /// Optional. Service Bus Topic to scale on. Cannot be provided if QueueName is used.
        /// </summary>
        public string? TopicName { get; set; }
        /// <summary>
        /// Optional. Service Bus Topic Subscription to scale on. Has to be provided if TopicName is provided. 
        /// </summary>
        public string? SubscriptionName { get; set; }
        /// <summary>
        /// Optional. Count of sessions to trigger scaling on. Default is 1 session.
        /// </summary>
        public long SessionCount { get; set; } = 1;
        /// <summary>
        /// Optional. Target value for activating scaler (scaling from 0). Default is 0.
        /// </summary>
        public long ActivationSessionCount { get; set; } = 0;
        /// <summary>
        /// Optional. Interval in seconds to check for session count. Maximum value is 600 seconds (10 minutes). Minimum value is 30 seconds. Used only by external-push scaler. Default is 300
        /// </summary>
        public string ActivationPollInterval { get; set; } = "300";

        public static ScalerMetadata? Create(ScaledObjectRef scaledObjectRef)
            => Deserialize(scaledObjectRef.ScalerMetadata.ToString(), AppJsonSerializerContext.Default.ScalerMetadata);
    }
}
