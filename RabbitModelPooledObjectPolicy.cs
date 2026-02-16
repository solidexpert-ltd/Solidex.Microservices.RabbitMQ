using System.Threading;
using Microsoft.Extensions.ObjectPool;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Solidex.Microservices.RabbitMQ
{
    public class RabbitModelPooledObjectPolicy : IPooledObjectPolicy<IChannel>
    {
        private readonly RabbitMqConfiguration _options;

        private readonly IConnection _connection;

        public RabbitModelPooledObjectPolicy(IOptions<RabbitMqConfiguration> optionsAccs)
        {
            _options = optionsAccs.Value;
            _connection = GetConnection();
        }

        private IConnection GetConnection()
        {
            var factory = new ConnectionFactory()
            {
                HostName = _options.Hostname,
                UserName = _options.UserName,
                Password = _options.Password,
                Port = AmqpTcpEndpoint.UseDefaultPort,
                VirtualHost = _options.VHost
            };

            return factory.CreateConnectionAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        public IChannel Create()
        {
            return _connection.CreateChannelAsync(null, CancellationToken.None).GetAwaiter().GetResult();
        }

        public bool Return(IChannel obj)
        {
            if (obj.IsOpen)
            {
                return true;
            }
            else
            {
                obj?.Dispose();
                return false;
            }
        }
    }
}