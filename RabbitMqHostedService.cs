using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;


namespace Solidex.Microservices.RabbitMQ
{
    public abstract class RabbitMqHostedService : BackgroundService
    {
        private readonly IConnection _connection;
        private readonly IModel _channel;
        protected string QueueName;

        public RabbitMqHostedService(IOptions<RabbitMqConfiguration> options)
        {
            var factory = new ConnectionFactory
            {
                HostName = options.Value.Hostname,
                UserName = options.Value.UserName,
                Password = options.Value.Password,
                Port = AmqpTcpEndpoint.UseDefaultPort,
                VirtualHost = options.Value.VHost
                //Port = options.Value.Port
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            //_channel.QueueDeclare(queue: _queueName, durable: true, exclusive: false, autoDelete: false, arguments: null);
        }

        protected abstract override Task ExecuteAsync(CancellationToken stoppingToken);

        public override void Dispose()
        {
            _connection?.Dispose();
            _channel?.Dispose();
            base.Dispose();
        }
    }
}