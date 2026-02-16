using Solidex.Microservices.RabbitMQ.Attributes;

namespace Solidex.Microservices.RabbitMQ.IntegrationTests.Fakes
{
    /// <summary>
    /// DTO for fire-and-forget Publish and Subscribe tests. Has [RabbitMessage] for Publish.
    /// </summary>
    [RabbitMessage(Exchange = "test-events", ExchangeType = "direct", RouteKey = "test.event")]
    public class TestEvent
    {
        public string Id { get; set; } = string.Empty;
        public int Sequence { get; set; }
    }
}
