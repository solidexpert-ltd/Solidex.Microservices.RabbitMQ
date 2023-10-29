using System.Threading;
using System.Threading.Tasks;

namespace Solidex.Microservices.RabbitMQ{

public interface IBus
{
    public Task ExecuteAsync(CancellationToken stoppingToken);
}
}