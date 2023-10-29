using System.Collections.Generic;
using Solidex.Telegram.RabbitMQEndpointBinder;

namespace Solidex.Microservices.RabbitMQ{

public class EndpointsConfiguration : IEndpointsConfiguration
{
    public IEndpointConfiguration MapEndpoint<T>(string queue, bool durable = true, bool exclusive = false,
        bool autoDelete = false,
        IDictionary<string, object> arguments = null)
    {
        var cfg = new EndpointConfiguration<T>
        {
            Queue = queue,
            Durable = durable,
            Exclusive = exclusive,
            AutoDelete = autoDelete,
            Arguments = arguments,
        };

        Endpoints.Add(cfg);
        return cfg;
    }

    public List<IEndpointConfiguration> Endpoints { get; set; } = new List<IEndpointConfiguration>();
}
}