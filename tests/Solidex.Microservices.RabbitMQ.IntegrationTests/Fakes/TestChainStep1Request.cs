using Solidex.Microservices.RabbitMQ.Attributes;

namespace Solidex.Microservices.RabbitMQ.IntegrationTests.Fakes
{
    [RabbitMessage(
        Exchange = "test-chain-step1",
        ExchangeType = "direct",
        RouteKey = "chain.step1",
        ReplyQueue = "")]
    public class TestChainStep1Request
    {
        public int Value { get; set; }
    }
}
