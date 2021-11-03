using System;
using Solidex.Core.Base.Abstraction;

namespace solidex.microcervices.rabbitMQ
{
    public interface IRabbitManager
    {
        void Publish<T>(T message, string exchangeName, string exchangeType, string routeKey)
            where T : class;

        T Subscribe<T>(string queueName, Guid id) where T : IEntity;
    }
}