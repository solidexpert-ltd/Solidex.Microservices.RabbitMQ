using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Solidex.Microservices.RabbitMQ.Endpoints
{
    /// <summary>
    /// RabbitMQ consumer wrapper that dispatches messages to MediatR.
    /// </summary>
    public class RabbitMqWrapper<T> : IBus
    {
        private readonly IChannel _channel;
        private readonly IConnection _connection;
        private readonly string _queue;
        private readonly IServiceProvider _serviceProvider;

        public RabbitMqWrapper(
            IOptions<RabbitMqConfiguration> options,
            EndpointConfiguration<T> configuration,
            IServiceProvider services)
        {
            _serviceProvider = services;
            var opts = options.Value;
            var factory = new ConnectionFactory
            {
                HostName = opts.Hostname,
                UserName = opts.UserName,
                Password = opts.Password,
                Port = opts.Port > 0 ? opts.Port : AmqpTcpEndpoint.UseDefaultPort,
                VirtualHost = string.IsNullOrEmpty(opts.VHost) ? "/" : opts.VHost
            };

            _connection = factory.CreateConnectionAsync(CancellationToken.None).GetAwaiter().GetResult();
            _channel = _connection.CreateChannelAsync(null, CancellationToken.None).GetAwaiter().GetResult();
            _queue = configuration.Queue;
            _channel.QueueDeclareAsync(_queue, configuration.Durable, configuration.Exclusive,
                    configuration.AutoDelete, configuration.Arguments, false, false, CancellationToken.None)
                .GetAwaiter().GetResult();
            if (!string.IsNullOrEmpty(configuration.Exchange))
            {
                _channel.ExchangeDeclareAsync(configuration.Exchange, "direct", true, false, null, false, false, CancellationToken.None)
                    .GetAwaiter().GetResult();
                _channel.QueueBindAsync(configuration.Queue, configuration.Exchange, configuration.RoutingKey, null, false, CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
        }

        public Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (_, ea) =>
            {
                var content = Encoding.UTF8.GetString(ea.Body.Span);
                try
                {
                    using var scope = _serviceProvider.CreateScope();

                    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                    var message = JsonConvert.DeserializeObject<T>(content);
                    
                    if (message != null)
                        await mediator.Send(message, stoppingToken);
                }
                finally
                {
                    await _channel.BasicAckAsync(ea.DeliveryTag, false, CancellationToken.None);
                }
            };

            _channel.BasicConsumeAsync(_queue, false, string.Empty, false, false, null, consumer, CancellationToken.None)
                .GetAwaiter().GetResult();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _channel.CloseAsync(200, "Connection closed", false, CancellationToken.None).GetAwaiter().GetResult();
            _connection.Dispose();
        }
    }
}
