using System.Threading;
using System.Threading.Tasks;

namespace Solidex.Microservices.RabbitMQ
{
    /// <summary>
    /// Represents a message bus endpoint that can execute as a hosted consumer.
    /// </summary>
    public interface IBus
    {
        Task ExecuteAsync(CancellationToken stoppingToken);
    }
}
