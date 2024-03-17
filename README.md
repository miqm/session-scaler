# Azure Service Bus Session Scaler

[![Build Status](https://dev.azure.com/miqm/github/_apis/build/status/miqm.session-scaler?branchName=main)](https://dev.azure.com/miqm/github/_build/latest?definitionId=10&branchName=main)


## Installation

Add a helm repo:
```console
helm repo add miqm https://miqm.github.io/helm-charts
```

Create a kubernetes secret with connection string to the service bus using a [Shared Access Policies](https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-sas). `Listen` permission is sufficient.
Remember to put connection string in quotes while executing command below!
```console
kubectl create secret generic session-scaler-connections \
    --from-literal=SERVICE_BUS_CONNECTION_STRING="<connection-string-here>"
```

Create a values.yaml file where you specify env file with value from the secret created above:
```yaml
env:
  - name: SERVICE_BUS_CONNECTION_STRING
    valueFrom:
      secretKeyRef:
        name: session-scaler-connections
        key: SERVICE_BUS_CONNECTION_STRING
```

Finally, install helm release with values file:
```console
helm install -f values.yaml -n <namespace> session-scaler miqm/session-scaler
```

The service address will be `session-scaler.<namespace>.svc.cluster.local:8998`

## Usage

Set up the [external trigger](https://keda.sh/docs/latest/scalers/external/) on a KEDA ScaledObject:

```yaml
apiVersion: keda.sh/v1alpha1
kind: ScaledObject
metadata:
  name: {scaled-object-name}
spec:
  scaleTargetRef:
    apiVersion: {api-version-of-target-resource}  # Optional. Default: apps/v1
    kind: {kind-of-target-resource}               # Optional. Default: Deployment
    name: {name-of-target-resource}               # Mandatory. Must be in the same namespace as the ScaledObject
  triggers:
    - type: external
      metricType: Value                                         # Set to `Value` to achieve instance per session behaviour
      metadata:
        scalerAddress: {session-scaler-service-address}:{session-scaler-service-port} # Required. Address of session-scaler service. Does not need to be in this same namespace, but network connectivity must be provided.
        queueName: {queue-name}                                 # Optional. Service Bus Queue to scale on. This or topicName and subscriptionName has to be provided. Takes precedence over topicName and subscriptionName.
        topicName: {topic-name}                                 # Optional. Service Bus Topic to scale on. Cannot be provided if queueName is used.
        subscriptionName: {topic-subscription-name}             # Optional. Service Bus Topic Subscription to scale on. Has to be provided if topicName is provided.
        sessionCount: 1                                         # Optional. Count of sessions to trigger scaling on. Default is 1 session.
        activationSessionCount: 0                               # Optional. Target value for activating the scaler (scaling from 0). Default is 0.
        connectionStringSetting: SERVICE_BUS_CONNECTION_STRING  # Optional. Environment variable name to read connection string with SharedAccessKey from. Default is SERVICE_BUS_CONNECTION_STRING.
                                                                #           Unlike regular KEDA scalers, the environment variable is to be set on the session-scaler, not on the scaled deployment.
                                                                #           use `envFrom` to set environment variable from kubernetes secrets on this release.  Each trigger can use different connection string.
```
## Limitations

The mechanism behind counting sessions is based on `get-message-sessions` AMQP command described in the [Service Bus AMQP docummentation](https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-amqp-request-response#enumerate-sessions). Although it provides a list of sessions on a queue or topic's subscription, it **does not** take into account if there are any **active messages**, just the presence of a session in queue.

Currently there is no mechanism to get count of active messages in a particular session. Using available commands, in a worst-case scenario, we would need to peek all messages in the session to determine if there's an active message besides the scheduled ones. This could generate a huge network traffic and is not very efficient.

You can upvote https://github.com/Azure/azure-service-bus/issues/275 to help proritise native support that can be used in KEDA scalers.
