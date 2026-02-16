using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Microsoft.Extensions.ObjectPool;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Solidex.Core.Base.Abstraction;
using Solidex.Microservices.RabbitMQ.Attributes;

namespace Solidex.Microservices.RabbitMQ
{
    public class RabbitManager(IPooledObjectPolicy<IChannel> objectPolicy) : IRabbitManager
    {
        private readonly DefaultObjectPool<IChannel> _objectPool = new(objectPolicy, Environment.ProcessorCount * 2);

        public void Publish<T>(T message, string exchangeName, string exchangeType, string routeKey)
            where T : class
        {
            if (message == null)
                return;

            var channel = _objectPool.Get();

            try
            {
                channel.ExchangeDeclareAsync(exchangeName, exchangeType, true, false, null, false, false, CancellationToken.None).GetAwaiter().GetResult();

                var sendBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message));

                var properties = new BasicProperties
                {
                    Persistent = true,
                    Type = message.GetType().Name
                };

                channel.BasicPublishAsync(exchangeName, routeKey, false, properties, sendBytes, CancellationToken.None).GetAwaiter().GetResult();
            }
            finally
            {
                _objectPool.Return(channel);
            }
        }

        public T Subscribe<T>(string queueName, Guid id) where T : IEntity
        {
            var respQueue = new BlockingCollection<T>();

            var channel = _objectPool.Get();
            channel.QueueDeclareAsync(queueName, true, false, false, null, false, false, CancellationToken.None).GetAwaiter().GetResult();

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (ch, ea) =>
            {
                var content = Encoding.UTF8.GetString(ea.Body.Span);
                var message = JsonConvert.DeserializeObject<T>(content);
                await channel.BasicAckAsync(ea.DeliveryTag, false, CancellationToken.None);
                if (message != null && message.Id != id) return;
                if (message != null) respQueue.Add(message);
                respQueue.CompleteAdding();
                await System.Threading.Tasks.Task.CompletedTask;
            };

            channel.BasicConsumeAsync(queueName, false, string.Empty, false, false, null, consumer, CancellationToken.None).GetAwaiter().GetResult();

            return respQueue.Take();
        }

        public void Send<T>(T message) where T : class
        {
            if (message == null)
                return;

            var channel = _objectPool.Get();

            var attribute = typeof(T).GetCustomAttributes(typeof(RabbitQueryAttribute)).FirstOrDefault() as RabbitQueryAttribute;

            if (attribute is null)
                throw new Exception($"The {nameof(RabbitQueryAttribute)} attribute is not exist");

            try
            {
                channel.ExchangeDeclareAsync(attribute.ExchangeName, attribute.ExchangeType, true, false, null, false, false, CancellationToken.None).GetAwaiter().GetResult();

                var sendBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message));

                var properties = new BasicProperties { Persistent = true };

                channel.BasicPublishAsync(attribute.ExchangeName, attribute.RouteKey, false, properties, sendBytes, CancellationToken.None).GetAwaiter().GetResult();
            }
            finally
            {
                _objectPool.Return(channel);
            }
        }

        public void Consume<T, TE>(Func<T, TE> lambda)
        {
            var attribute = typeof(T).GetCustomAttributes(typeof(RabbitQueryAttribute)).FirstOrDefault() as RabbitQueryAttribute;

            if (attribute is null)
                throw new Exception($"The {nameof(RabbitQueryAttribute)} attribute is not exist");

            var channel = _objectPool.Get();
            channel.QueueDeclareAsync(Assembly.GetExecutingAssembly().FullName + nameof(T), true, false, false, null, false, false, CancellationToken.None).GetAwaiter().GetResult();

            var consumer = new AsyncEventingBasicConsumer(channel);

            consumer.ReceivedAsync += async (ch, ea) =>
            {
                var content = Encoding.UTF8.GetString(ea.Body.Span);
                try
                {
                    var message = JsonConvert.DeserializeObject<T>(content);
                    if (message != null) lambda.Invoke(message);
                }
                finally
                {
                    await channel.BasicAckAsync(ea.DeliveryTag, false, CancellationToken.None);
                }
            };

            channel.BasicConsumeAsync(Assembly.GetExecutingAssembly().FullName + nameof(T), false, string.Empty, false, false, null, consumer, CancellationToken.None).GetAwaiter().GetResult();
        }
    }
}