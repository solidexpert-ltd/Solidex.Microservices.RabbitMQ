using System.Collections.Generic;

namespace Solidex.Microservices.RabbitMQ
{
    /// <summary>
    /// Fluent configuration for RabbitMQ consumer endpoints.
    /// </summary>
    public class EndpointsConfiguration : IEndpointsConfiguration
    {
        public List<IEndpointConfiguration> Endpoints { get; set; } = new();

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
                Arguments = arguments
            };

            Endpoints.Add(cfg);
            return cfg;
        }
    }
}
