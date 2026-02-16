# ChainMQ: multi-step pipeline with auto-routing

## What is ChainMQ?

**ChainMQ** provides a fluent multi-step pipeline:

- **ChainMQ(request)** – send the initial request to RabbitMQ (exchange, route, reply queue from **[RabbitMessage]** on the request type).
- **ThenBy&lt;TResponse, TNextRequest&gt;(response => nextRequest)** – map the previous step’s response to the next request. The next step runs via **RabbitMQ** if `TNextRequest` has [RabbitMessage], otherwise via **MediatR**.
- **ExecuteAsync&lt;TResponse&gt;(ct)** – run the chain and return the final response.

No context bag; each step is a simple lambda from previous response to next request.

---

## 1. Attribute on request types (for MQ steps)

```csharp
using Solidex.Microservices.RabbitMQ.Attributes;

[RabbitMessage(
    Exchange = "user-exchange",
    ExchangeType = "direct",
    RouteKey = "user.get",
    ReplyQueue = "")]  // empty = auto-generated per request
public class GetUserRequest
{
    public Guid UserId { get; set; }
}
```

- **Exchange**, **RouteKey** – required for routing.
- **ExchangeType** – optional, default `"direct"`.
- **ReplyQueue** – optional; if empty, a unique reply queue is generated for RPC steps.

---

## 2. Single-step (request → response)

```csharp
using Solidex.Microservices.RabbitMQ.Chain;

var response = await _rabbitManager
    .ChainMQ(new GetUserRequest { UserId = id })
    .ExecuteAsync<UserResponse>(cancellationToken);
```

---

## 3. Multi-step with ThenBy (MQ and/or MediatR)

```csharp
var result = await _rabbitManager
    .ChainMQ(new GetUserRequest { UserId = id })
    .ThenBy<UserResponse, ProcessUserCommand>(r => new ProcessUserCommand { UserId = r.Id })
    .ExecuteAsync<ProcessUserResult>(cancellationToken);
```

- If `ProcessUserCommand` has [RabbitMessage], that step runs via RabbitMQ RPC.
- If it does not (e.g. implements `IRequest<ProcessUserResult>`), that step runs via MediatR (**IMediator** must be registered).

---

## 4. Responder requirements (for MQ steps)

The service that handles the request must:

1. Consume from the request queue (bound to the exchange/route from [RabbitMessage]).
2. Read **ReplyTo** and **CorrelationId** from the incoming message **BasicProperties**.
3. Process the request and build the response.
4. Publish the response to the **ReplyTo** queue with the same **CorrelationId**.

---

## 5. Namespaces

```csharp
using Solidex.Microservices.RabbitMQ;
using Solidex.Microservices.RabbitMQ.Chain;
using Solidex.Microservices.RabbitMQ.Attributes;
```

---

## Summary

| Step | Method | Purpose |
|------|--------|--------|
| 1 | `ChainMQ(request)` | Start chain; initial request must have [RabbitMessage]. |
| 2 | `ThenBy<TResponse, TNextRequest>(response => nextRequest)` | Add step; routes to MQ or MediatR by attribute on TNextRequest. |
| 3 | `ExecuteAsync<TResponse>(ct)` | Run chain and return the final response. |
