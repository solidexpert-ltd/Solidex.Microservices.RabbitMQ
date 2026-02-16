using Solidex.Microservices.RabbitMQ.Attributes;

namespace Solidex.Microservices.RabbitMQ.IntegrationTests.Fakes
{
    [RabbitMessage(
        Exchange = "test-query-exchange",
        ExchangeType = "direct",
        RouteKey = "test.query")]
    public class TestQueryMessage
    {
        public string Payload { get; set; } = string.Empty;
    }
}
