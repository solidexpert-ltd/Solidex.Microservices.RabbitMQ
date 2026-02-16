using System.Reflection;
using Solidex.Microservices.RabbitMQ.Attributes;
using Solidex.Microservices.RabbitMQ.IntegrationTests.Fakes;
using Xunit;

namespace Solidex.Microservices.RabbitMQ.IntegrationTests.UnitTests
{
    public class RabbitMessageAttributeTests
    {
        [Fact]
        public void Attribute_DefaultValues_Correct()
        {
            var attr = new RabbitMessageAttribute();
            Assert.Equal(string.Empty, attr.Exchange);
            Assert.Equal("direct", attr.ExchangeType);
            Assert.Equal(string.Empty, attr.RouteKey);
            Assert.Equal(string.Empty, attr.ReplyQueue);
        }

        [Fact]
        public void Attribute_AllPropertiesSet_ReadCorrectly()
        {
            var attr = new RabbitMessageAttribute
            {
                Exchange = "my-exchange",
                ExchangeType = "topic",
                RouteKey = "my.route",
                ReplyQueue = "reply-queue"
            };
            Assert.Equal("my-exchange", attr.Exchange);
            Assert.Equal("topic", attr.ExchangeType);
            Assert.Equal("my.route", attr.RouteKey);
            Assert.Equal("reply-queue", attr.ReplyQueue);
        }

        [Fact]
        public void Attribute_MissingOnType_DetectedAtRuntime()
        {
            var hasAttr = typeof(TestRequest).GetCustomAttributes(typeof(RabbitMessageAttribute), false).Length > 0;
            Assert.False(hasAttr);
            var hasAttrOnEvent = typeof(TestEvent).GetCustomAttributes(typeof(RabbitMessageAttribute), false).Length > 0;
            Assert.True(hasAttrOnEvent);
        }
    }
}
