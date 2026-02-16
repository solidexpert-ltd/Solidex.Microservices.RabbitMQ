# AGENTICS.MD – Agent instructions for Solidex.Microservices.RabbitMQ

This file gives AI agents and code generators the minimal context needed to work with this package correctly.

---

## 1. Package identity and purpose

- **Package:** Solidex.Microservices.RabbitMQ  
- **Purpose:** RabbitMQ integration for .NET: **Publish** (fire-and-forget), **Send** (RPC), **Subscribe** (events), **ChainMQ** (multi-step pipeline with auto-routing MQ/MediatR). Consumer endpoints dispatch to MediatR.
- **Target:** .NET 10.0 (net10.0). Uses RabbitMQ.Client 7.2.0.

---

## 2. Project and namespace layout

| Folder / area | Namespace(s) | Contents |
|---------------|--------------|----------|
| Configuration/ | Solidex.Microservices.RabbitMQ | RabbitMqConfiguration |
| Abstractions/ | Solidex.Microservices.RabbitMQ | IRabbitManager, IBus, IEndpointsConfiguration, IEndpointConfiguration |
| Extensions/ | Solidex.Microservices.RabbitMQ | AddRabbit, AddRabbitMqEndpoints |
| Infrastructure/ | Solidex.Microservices.RabbitMQ.Infrastructure | RabbitManager, RabbitModelPooledObjectPolicy |
| Hosting/ | Solidex.Microservices.RabbitMQ.Hosting | RabbitMqHostedService |
| Endpoints/ | Solidex.Microservices.RabbitMQ (+ .Endpoints for impl) | EndpointsConfiguration, EndpointConfiguration&lt;T&gt;, RabbitMqWrapper&lt;T&gt; |
| Chain/ | Solidex.Microservices.RabbitMQ.Chain | ChainMQ, ThenBy, IRabbitChain, RabbitChainBuilder, RabbitChainExtensions |
| Attributes/ | Solidex.Microservices.RabbitMQ.Attributes | RabbitMessageAttribute |

- **Public API:** Most consumer-facing types live in **Solidex.Microservices.RabbitMQ** (root). Implementation details use **Infrastructure**, **Hosting**, **Endpoints**, **Chain**.
- **ChainMQ:** Entry point is **ChainMQ(request)** (extension on IRabbitManager); options come from **[RabbitMessage]** on the request type. **ThenBy** adds steps; each step routes to MQ (if type has [RabbitMessage]) or MediatR.

---

## 3. Registration (DI)

```csharp
// Required for IRabbitManager (Publish, Send, Subscribe, ChainMQ)
builder.Services.AddRabbit(builder.Configuration);

// Optional: consumer endpoints (MediatR)
builder.Services.AddRabbitMqEndpoints(cfg => {
    cfg.MapEndpoint<TMessage>(queue: "queue-name", durable: true).WithBinding("exchange", "routing-key");
});
```

Configuration key: **"RabbitMQ"** (Hostname, UserName, Password, Port, VHost, QueueName).

---

## 4. Core abstractions

**Four behaviours:** (1) **Publish** — fire-and-forget, uses [RabbitMessage] on T. (2) **Send** — RPC, uses [RabbitMessage] on TRequest, returns TResponse. (3) **Subscribe** — receive all events from a queue. (4) **ChainMQ** — multi-step pipeline with ThenBy; steps route to MQ or MediatR by attribute.

- **IRabbitManager**
  - Task Publish&lt;T&gt;(message, ct) — fire-and-forget; reads [RabbitMessage] from T
  - Task&lt;TResponse&gt; Send&lt;TRequest, TResponse&gt;(message, ct) — RPC; reads [RabbitMessage] from TRequest
  - Task Subscribe&lt;T&gt;(queueName, onMessage, ct) — receive all events until cancelled
  - Task PublishRaw&lt;T&gt;(message, exchange, exchangeType, routeKey, ct)
  - Task PublishWithReplyTo&lt;T&gt;(..., replyToQueue, correlationId, ct)
  - Task&lt;T&gt; WaitForResponseAsync&lt;T&gt;(replyQueueName, correlationId, ct)
  - Task EnsureReplyQueueExists(replyQueueName, ct)
- **IBus** — ExecuteAsync(CancellationToken); used by hosted endpoint consumers.
- **RabbitMqConfiguration** — options bound from config.

---

## 5. ChainMQ (ThenBy + auto-routing)

- **Pattern:** ChainMQ(request).ThenBy&lt;TResponse, TNextRequest&gt;(resp => nextRequest).ExecuteAsync&lt;TResponse&gt;(ct)
- **Entry:** `IRabbitManager.ChainMQ<TRequest>(request)` — initial request must have **[RabbitMessage]**. Returns **IRabbitChain&lt;TRequest&gt;**.
- **ThenBy:** `chain.ThenBy<TResponse, TNextRequest>(response => nextRequest)` — map previous response to next request. If TNextRequest has [RabbitMessage], step runs via RabbitMQ RPC; otherwise via MediatR. Returns **IRabbitChain&lt;TNextRequest&gt;**.
- **Execute:** `chain.ExecuteAsync<TResponse>(ct)` — returns the final response (Task&lt;TResponse&gt;).
- **Semantics:** Run steps sequentially. Each step: if next request type has [RabbitMessage], call Send; else resolve IMediator and Send. No context bag; simple lambda mapping.

---

## 6. Request-response (responder side)

- Consumer must read **ReplyTo** and **CorrelationId** from incoming message **BasicProperties**.
- Publish response to the **ReplyTo** queue (routing key = queue name, exchange = "").
- Set **CorrelationId** on the response message to the same value as the request so the caller can match it.

---

## 7. Attributes and conventions

- **[RabbitMessage(Exchange, ExchangeType, RouteKey, ReplyQueue?)]** — single attribute for Publish, Send, and ChainMQ. Applied on the message/request type. ReplyQueue empty = auto-generated for RPC.
- **MediatR:** Consumer endpoints use **RabbitMqWrapper&lt;T&gt;** which deserializes to T and calls **IMediator.Send(message, ct)**. No attribute required for endpoint message types.

---

## 8. When editing or extending

- Keep **public API** in root namespace **Solidex.Microservices.RabbitMQ** so existing `using Solidex.Microservices.RabbitMQ` stays valid.
- New **chain** types belong in **Solidex.Microservices.RabbitMQ.Chain**.
- **IRabbitManager** is the single entry for publish, send, subscribe, and chain; new capabilities should go through the chain or new extensions on IRabbitManager.
- **RabbitMQ.Client 7.x:** Use **IChannel** (not IModel), **CreateChannelAsync** / **CreateConnectionAsync**, **BasicPublishAsync**, **BasicProperties** (ReplyTo, CorrelationId), **AsyncEventingBasicConsumer**, **BasicConsumeAsync**, **BasicAckAsync**.

---

## 9. Documentation and examples

- **docs/README.md** — full package documentation.
- **docs/RABBIT_CHAIN_USAGE.md** — ChainMQ usage and responder requirements (if present).

Agents should prefer these docs and this AGENTICS.MD when suggesting code or refactors involving this package.
