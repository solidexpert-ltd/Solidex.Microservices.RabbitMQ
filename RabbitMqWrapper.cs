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
using Solidex.Microservices.RabbitMQ;

namespace Solidex.Telegram.RabbitMQEndpointBinder
{
    public class RabbitMqWrapper<T> : IBus
    {
        private readonly IModel _channel;
        private readonly IConnection _connection;
        private readonly string _queue;
        private readonly IServiceProvider _serviceProvider;


        public RabbitMqWrapper(IOptions<RabbitMqConfiguration> options,
            EndpointConfiguration<T> configuration, IServiceProvider services)
        {
            _serviceProvider = services;
            var factory = new ConnectionFactory
            {
                HostName = options.Value.Hostname,
                UserName = options.Value.UserName,
                Password = options.Value.Password,
                Port = AmqpTcpEndpoint.UseDefaultPort,
                VirtualHost = options.Value.VHost
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            _queue = configuration.Queue;
            _channel.QueueDeclare(queue: _queue, durable: configuration.Durable, exclusive: configuration.Exclusive,
                autoDelete: configuration.AutoDelete,
                arguments: configuration.Arguments);
            _channel.QueueBind(configuration.Queue, configuration.Exchange, configuration.RoutingKey);
        }


        public Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += async (_, ea) =>
            {
                var content = Encoding.UTF8.GetString(ea.Body.ToArray());
                try
                {
                    using var scope = _serviceProvider.CreateScope();

                    var mediator =
                        scope.ServiceProvider
                            .GetRequiredService<IMediator>();

                    var message = JsonConvert.DeserializeObject<T>(content);
                    await mediator.Send(message, stoppingToken);
                }
                finally
                {
                    _channel.BasicAck(ea.DeliveryTag, false);
                }
            };

            _channel.BasicConsume(this._queue, false, consumer);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _channel.Close();
            _connection.Close();
        }
    }
}