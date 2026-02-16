# Integration tests (RabbitMQ request/response)

These tests run against a **real RabbitMQ instance** started in Docker via [Testcontainers](https://dotnet.testcontainers.org/modules/rabbitmq).

## Requirements

- **Docker** must be installed and running (Docker Desktop or engine).
- .NET 10 SDK.

## Run tests

### Option A: With Testcontainers (default)

Docker must be running. From the package root:

```bash
dotnet test tests/Solidex.Microservices.RabbitMQ.IntegrationTests/Solidex.Microservices.RabbitMQ.IntegrationTests.csproj
```

If Docker is not running, tests will fail with `DockerUnavailableException`.

### Option B: With docker-compose (no Testcontainers)

Start RabbitMQ with docker-compose, then run tests against it:

```bash
# From the tests folder
docker compose -f docker-compose.rabbitmq.yml up -d

# Run tests using the compose broker (PowerShell)
$env:RABBITMQ_CONNECTION_STRING = "amqp://guest:guest@localhost:5672/"
dotnet test tests/Solidex.Microservices.RabbitMQ.IntegrationTests/Solidex.Microservices.RabbitMQ.IntegrationTests.csproj
```

On Linux/macOS: `export RABBITMQ_CONNECTION_STRING=amqp://guest:guest@localhost:5672/` then `dotnet test ...`.

## What is tested

Integration tests cover each **method and situation**:

| Class | Test | Method / situation |
|-------|------|--------------------|
| **FireAndForgetIntegrationTests** | `Publish_message_reaches_consumer_on_bound_queue` | **Publish** (fire-and-forget): message reaches a consumer on a bound queue. |
| | `Send_with_RabbitQuery_attribute_publishes_to_configured_exchange_and_route` | **Send** with `[RabbitQuery]`: publishes to exchange/route from attribute; consumer receives. |
| **RequestResponseIntegrationTests** | `PublishWithReplyTo_and_WaitForResponseAsync_returns_response_from_responder` | **PublishWithReplyTo** + **WaitForResponseAsync**: request/response with responder. |
| | `SendMQ_ExecuteAsync_returns_response_when_responder_replies` | **SendMQ().ExecuteAsync** (RabbitChain): single response from chain. |
| **SubscribeIntegrationTests** | `SubscribeAsync_receives_all_messages_until_cancelled` | **SubscribeAsync**: receives multiple messages until cancellation. |
| | `SubscribeAsync_invokes_handler_per_message_and_continues_until_cancelled` | **SubscribeAsync**: handler invoked per message; runs until cancelled. |
| **ManagerIntegrationTests** | `EnsureReplyQueueExists_creates_queue_so_messages_can_be_delivered` | **EnsureReplyQueueExists**: queue exists and can receive messages. |
| | `WaitForResponseAsync_returns_only_single_response_when_responder_sends_multiple_with_same_correlation` | **WaitForResponseAsync**: only one response returned when multiple sent with same CorrelationId. |
| **EndpointMappingIntegrationTests** | `MapEndpoint_WithBinding_receives_published_message_and_dispatches_to_MediatR` | **AddRabbitMqEndpoints** + **MapEndpoint** + **WithBinding**: queue bound to exchange/routing key; published message dispatched to MediatR. |
| | `MapEndpoint_with_different_binding_receives_on_correct_routing_key` | **MapEndpoint** with different binding (e.g. `service.user` / `account.deleted`): receives only on the configured routing key. |

## Rabbit endpoint mapping (MapEndpoint + WithBinding)

Example of mapping request types to queues and binding them to exchanges (e.g. notification and user events):

```csharp
services.AddRabbitMqEndpoints(cfg =>
{
    cfg.MapEndpoint<SendMessageRequest>(queue: "service.notification.telegram:send-message", durable: true,
        exclusive: false,
        autoDelete: false,
        arguments: null).WithBinding("service.notification", "telegram");

    cfg.MapEndpoint<Solidex.Telegram.Commands.RabbitMQ.DeleteChatRequest.DeleteChatRequest>(queue: "service.notification.telegram:delete-chat", durable: true,
        exclusive: false,
        autoDelete: false,
        arguments: null).WithBinding("service.user", "account.deleted");
});
```

Integration tests that cover this pattern: **EndpointMappingIntegrationTests** (see table above). They require a running RabbitMQ broker (Testcontainers or `RABBITMQ_CONNECTION_STRING`).

## Running in CI (e.g. GitHub Actions)

Ensure the job has Docker available (e.g. `runs-on: ubuntu-latest` with a Docker service or a runner that has Docker). Testcontainers will start the RabbitMQ container automatically; no extra docker-compose is required for the tests.

