using Solidex.Microservices.RabbitMQ.Attributes;

namespace Solidex.Microservices.RabbitMQ.IntegrationTests.Fakes
{
    [RabbitMessage(
        Exchange = "test-chain-step2",
        ExchangeType = "direct",
        RouteKey = "chain.step2",
        ReplyQueue = "")]
    public class TestChainStep2Request
    {
        public int Value { get; set; }
    }
}
