# Solidex.Microservices.RabbitMQ

RabbitMQ integration for .NET microservices: **Publish** (fire-and-forget), **Send** (RPC), **Subscribe** (events), and **ChainMQ** (multi-step with auto-routing) with optional MediatR.

**Four behaviours:**

| Behaviour | API | Description |
|-----------|-----|-------------|
| **1. Publish** | `await Publish<T>(message, ct)` | Fire-and-forget; uses [RabbitMessage] on T. |
| **2. Send** | `await Send<TRequest, TResponse>(request, ct)` | RPC; uses [RabbitMessage] on TRequest; returns TResponse. |
| **3. Subscribe** | `await Subscribe<T>(queueName, onMessage, ct)` or `AddRabbitMqEndpoints` (MediatR) | Receive all events from a queue until cancelled. |
| **4. ChainMQ** | `ChainMQ(req).ThenBy(...).ExecuteAsync<TResponse>(ct)` | Multi-step pipeline; steps with [RabbitMessage] go to MQ, else MediatR. |

---

## Table of contents

1. [Installation](#installation)
2. [Configuration](#configuration)
3. [Basic publishing and sending](#basic-publishing-and-sending)
4. [Consumer endpoints (MediatR)](#consumer-endpoints-mediatr)
5. [Request-response and ChainMQ](#request-response-and-chainmq)
6. [Responder: replying with CorrelationId](#responder-replying-with-correlationid)
7. [Subscribe: receive all events](#subscribe-receive-all-events)
8. [Integration tests](#integration-tests)
9. [API reference](#api-reference)

---

## Installation

```bash
dotnet add package Solidex.Microservices.RabbitMQ
```

In `Program.cs` (or `Startup.cs`):

```csharp
using Solidex.Microservices.RabbitMQ;

// Core: IRabbitManager, channel pool, configuration
builder.Services.AddRabbit(builder.Configuration);

// Optional: consumer endpoints (hosted service that runs queues and dispatches to MediatR)
builder.Services.AddRabbitMqEndpoints(cfg =>
{
    cfg.MapEndpoint<MyRequest>(queue: "my-queue", durable: true)
        .WithBinding("my-exchange", "my-routing-key");
});
```

---

## Configuration

`appsettings.json`:

```json
{
  "RabbitMQ": {
    "Hostname": "localhost",
    "UserName": "guest",
    "Password": "guest",
    "Port": 5672,
    "VHost": "/",
    "QueueName": ""
  }
}
```

Inject `IRabbitManager` and/or `IOptions<RabbitMqConfiguration>` where needed.

---

## Basic publishing and sending

### Publish (fire-and-forget, attribute-based)

Decorate the message type with `[RabbitMessage]`:

```csharp
using Solidex.Microservices.RabbitMQ.Attributes;

[RabbitMessage(Exchange = "NotificationHub", ExchangeType = "direct", RouteKey = "user.created")]
public class UserCreatedEvent { ... }

await _rabbitManager.Publish(new UserCreatedEvent { ... });
```

### Send (RPC, attribute-based)

Same attribute; response type is the second generic parameter:

```csharp
[RabbitMessage(Exchange = "service.calc", RouteKey = "calculate")]
public class CalculateRequest { ... }

CalculateResponse resp = await _rabbitManager.Send<CalculateRequest, CalculateResponse>(
    new CalculateRequest { X = 5 }, cancellationToken);
```

---

## Consumer endpoints (MediatR)

Register endpoints and map queues to MediatR handlers:

```csharp
builder.Services.AddRabbitMqEndpoints(cfg =>
{
    cfg.MapEndpoint<SendMessageRequest>(
            queue: "service.notification.telegram:send-message",
            durable: true)
        .WithBinding("my-exchange", "my-routing-key");
});
```

Each message received on the queue is deserialized and sent to MediatR:

```csharp
await mediator.Send(message, stoppingToken);
```

Handlers are standard MediatR request handlers (e.g. `IRequestHandler<SendMessageRequest, Unit>`).

---

## Request-response and ChainMQ

Use **ChainMQ** for multi-step pipelines. Each step can route to **RabbitMQ** (if the request type has [RabbitMessage]) or **MediatR** (if not). Simple lambda mapping: `ThenBy<TResponse, TNextRequest>(response => nextRequest)`.

Pattern: **ChainMQ(request).ThenBy(...).ThenBy(...).ExecuteAsync&lt;TResponse&gt;(ct)**

### 1. Attribute on request types (for MQ steps)

```csharp
using Solidex.Microservices.RabbitMQ.Attributes;

[RabbitMessage(Exchange = "req-exchange", RouteKey = "user.get", ReplyQueue = "")]
public class GetUserRequest { public Guid UserId { get; set; } }
```

### 2. Single-step (request → response)

```csharp
using Solidex.Microservices.RabbitMQ.Chain;

var response = await _rabbitManager
    .ChainMQ(myRequest)
    .ExecuteAsync<UserResponse>(cancellationToken);
```

### 3. Multi-step with ThenBy (MQ and/or MediatR)

```csharp
var result = await _rabbitManager
    .ChainMQ(new Step1Request { Id = id })
    .ThenBy<Step1Response, Step2Request>(r => new Step2Request { Value = r.Data })
    .ThenBy<Step2Response, Step3Request>(r => new Step3Request { X = r.Y })
    .ExecuteAsync<Step3Response>(cancellationToken);
```

- If `Step2Request` has [RabbitMessage], that step runs via RabbitMQ RPC.
- If it does not (e.g. implements IRequest&lt;T&gt;), that step runs via MediatR (IMediator must be registered).

---

## Responder: replying with CorrelationId

The service that handles the request must:

1. Read **ReplyTo** and **CorrelationId** from the incoming message properties.
2. Process the request (e.g. with MediatR).
3. Publish the response to the **ReplyTo** queue and set **CorrelationId** on the response message.

Example (pseudo-code):

```csharp
// In your consumer (e.g. RabbitMqWrapper or a custom consumer)
consumer.ReceivedAsync += async (_, ea) =>
{
    var replyTo = ea.BasicProperties.ReplyTo;
    var correlationId = ea.BasicProperties.CorrelationId;
    var request = JsonConvert.DeserializeObject<MyRequest>(ea.Body);

    var response = await mediator.Send(request);

    // Publish response to replyTo with the same correlationId
    channel.BasicPublish(
        exchange: "",
        routingKey: replyTo,  // AMQP: empty exchange = default, route to queue name
        basicProperties: new BasicProperties { CorrelationId = correlationId },
        body: JsonConvert.SerializeObject(response));
};
```

So: **ReplyTo** = reply queue name; **CorrelationId** = same as request so the caller can match the response.

---

## Subscribe: receive all events

To process **every** message from a queue until cancellation (long-running consumer), use `Subscribe`:

```csharp
// Runs until cancellationToken is cancelled; calls onMessage for each message
await _rabbitManager.Subscribe<MyEvent>(
    queueName: "my-events",
    onMessage: async (message, ct) =>
    {
        await ProcessAsync(message, ct);
    },
    cancellationToken: stoppingToken);
```

Messages are acknowledged after `onMessage` completes. For a MediatR-based consumer (one handler per request type), use **AddRabbitMqEndpoints** and map the queue to a MediatR handler instead.

---

## Integration tests

Integration tests for Publish, Send, Subscribe, and ChainMQ run against a real RabbitMQ instance in Docker (Testcontainers). **Docker must be running.**

From the package root:

```bash
dotnet test tests/Solidex.Microservices.RabbitMQ.IntegrationTests/Solidex.Microservices.RabbitMQ.IntegrationTests.csproj
```

See `tests/README.md` for details and CI notes.

---

## API reference

| API | Description |
|-----|-------------|
| `AddRabbit(services, configuration)` | Registers RabbitMQ config, channel pool, `IRabbitManager`. |
| `AddRabbitMqEndpoints(services, configure)` | Registers endpoint config and hosted consumer service. |
| `IRabbitManager.Publish<T>(message, ct)` | Fire-and-forget; uses [RabbitMessage] on T. |
| `IRabbitManager.Send<TRequest, TResponse>(message, ct)` | RPC; uses [RabbitMessage] on TRequest; returns TResponse. |
| `IRabbitManager.Subscribe<T>(queueName, onMessage, ct)` | Receive all events from queue until cancelled. |
| `IRabbitManager.PublishWithReplyTo<T>(..., replyToQueue, correlationId, ct)` | Publish with ReplyTo/CorrelationId. |
| `IRabbitManager.WaitForResponseAsync<T>(replyQueue, correlationId, ct)` | Wait for single response on reply queue. |
| `manager.ChainMQ(request)` | Start a chain (initial request must have [RabbitMessage]). |
| `chain.ThenBy<TResponse, TNextRequest>(response => nextRequest)` | Add step; routes to MQ or MediatR by attribute. |
| `chain.ExecuteAsync<TResponse>(ct)` | Run chain and return the final response. |

---

## Project layout

- **Solidex.Microservices.RabbitMQ** – configuration, abstractions, extensions.
- **Solidex.Microservices.RabbitMQ.Infrastructure** – `RabbitManager`, channel pool.
- **Solidex.Microservices.RabbitMQ.Hosting** – hosted service for endpoints.
- **Solidex.Microservices.RabbitMQ.Endpoints** – endpoint config, `RabbitMqWrapper` (MediatR consumer).
- **Solidex.Microservices.RabbitMQ.Chain** – `ChainMQ`, `ThenBy`, `ExecuteAsync`.
- **Solidex.Microservices.RabbitMQ.Attributes** – `[RabbitMessage]`.
