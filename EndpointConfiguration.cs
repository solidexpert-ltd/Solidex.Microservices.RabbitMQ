using System;
using System.Collections.Generic;
using Microsoft.Extensions.Options;
using Solidex.Telegram.RabbitMQEndpointBinder;

namespace Solidex.Microservices.RabbitMQ{

public class EndpointConfiguration<T> : IEndpointConfiguration
{
    public string Queue { get; set; }
    public bool Durable { get; set; }
    public bool Exclusive { get; set; }
    public bool AutoDelete { get; set; }
    public IDictionary<string, object> Arguments { get; set; }

    public string Exchange { get; set; }
    public string RoutingKey { get; set; }

    public void WithBinding(string exchange, string routingKey)
    {
        Exchange = exchange;
        RoutingKey = routingKey;
    }


    public IBus BuildWrapper(IServiceProvider services, IOptions<RabbitMqConfiguration> options)
    {
        return new RabbitMqWrapper<T>(options, this, services);
    }
}

}