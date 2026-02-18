using System;
using System.Collections.Generic;
using Microsoft.Extensions.Options;

namespace Solidex.Microservices.RabbitMQ
{
    /// <summary>
    /// Configuration for a single typed endpoint (queue + optional exchange binding).
    /// </summary>
    public class EndpointConfiguration<T> : IEndpointConfiguration
    {
        public string Queue { get; set; } = string.Empty;
        public bool Durable { get; set; }
        public bool Exclusive { get; set; }
        public bool AutoDelete { get; set; }
        public IDictionary<string, object> Arguments { get; set; }

        public string Exchange { get; set; } = string.Empty;
        /// <summary>Exchange type: "direct", "topic", "fanout", etc. Default is "direct".</summary>
        public string ExchangeType { get; set; } = "direct";
        public string RoutingKey { get; set; } = string.Empty;

        public void WithBinding(string exchange, string routingKey)
        {
            WithBinding(exchange, routingKey, "direct");
        }

        public void WithBinding(string exchange, string routingKey, string exchangeType)
        {
            Exchange = exchange;
            RoutingKey = routingKey;
            ExchangeType = string.IsNullOrEmpty(exchangeType) ? "direct" : exchangeType;
        }

        public IBus BuildWrapper(IServiceProvider services, IOptions<RabbitMqConfiguration> options)
        {
            return new Endpoints.RabbitMqWrapper<T>(options, this, services);
        }
    }
}
