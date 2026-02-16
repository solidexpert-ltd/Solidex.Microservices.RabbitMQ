using Solidex.Microservices.RabbitMQ.IntegrationTests.Infrastructure;
using Xunit;

namespace Solidex.Microservices.RabbitMQ.IntegrationTests
{
    [CollectionDefinition("RabbitMQ")]
    public class RabbitMqCollection : ICollectionFixture<RabbitMqFixture>
    {
    }
}
