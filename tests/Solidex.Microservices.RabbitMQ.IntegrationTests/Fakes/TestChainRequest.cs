using Solidex.Microservices.RabbitMQ.Attributes;

namespace Solidex.Microservices.RabbitMQ.IntegrationTests.Fakes
{
    [RabbitMessage(
        Exchange = "test-chain-exchange",
        ExchangeType = "direct",
        RouteKey = "chain.request",
        ReplyQueue = "")]
    public class TestChainRequest
    {
        public int Value { get; set; }
    }
}
