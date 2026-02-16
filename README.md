# Solidex.Microservices.RabbitMQ

RabbitMQ integration for .NET microservices: **Publish** (fire-and-forget), **Send** (RPC), **Subscribe** (events), and **ChainMQ** (multi-step pipeline with auto-routing). Consumer endpoints dispatch to MediatR.

- **Target:** .NET 10.0  
- **Authors:** Solidexpert LTD  
- **Dependencies:** RabbitMQ.Client 7.2.0, MediatR, Microsoft.Extensions.Hosting, Newtonsoft.Json  

---

## Description

This package provides a unified API over RabbitMQ: (1) **Publish** — fire-and-forget using [RabbitMessage] on T; (2) **Send** — RPC, wait for TResponse using [RabbitMessage] on TRequest; (3) **Subscribe** — receive all events from a queue; (4) **ChainMQ** — multi-step pipeline with ThenBy, auto-routing to MQ or MediatR. Configuration is bound from `"RabbitMQ"` (Hostname, UserName, Password, Port, VHost). Entry point is **IRabbitManager**; optional **AddRabbitMqEndpoints** registers a hosted service that runs consumer queues and dispatches to MediatR.

---

## Feature list

| # | Feature | Description |
|---|---------|-------------|
| 1 | **Publish (fire-and-forget)** | `await Publish<T>(message, ct)` — uses [RabbitMessage(Exchange, RouteKey)] on T. |
| 2 | **Send (RPC)** | `TResponse resp = await Send<TRequest, TResponse>(request, ct)` — uses [RabbitMessage] on TRequest; waits for TResponse. |
| 3 | **Subscribe (events)** | `await Subscribe<T>(queueName, (evt, ct) => { ... }, ct)` — receive all events until cancelled. |
| 4 | **ChainMQ** | `ChainMQ(request).ThenBy<TResp, TNext>(r => next).ExecuteAsync<TFinal>(ct)` — multi-step; steps with [RabbitMessage] go to MQ, else MediatR. |
| 5 | **Low-level** | `PublishWithReplyTo`, `WaitForResponseAsync`, `EnsureReplyQueueExists` for custom request-response. |
| 6 | **Consumer endpoints** | `AddRabbitMqEndpoints(cfg => cfg.MapEndpoint<T>(queue, ...).WithBinding(exchange, routingKey))` — hosted service dispatches to MediatR. |
| 7 | **Attribute** | `[RabbitMessage(Exchange, ExchangeType, RouteKey, ReplyQueue?)]` — used by Publish, Send, and ChainMQ. |

---

For installation, configuration, and usage examples see **docs/README.md**.
