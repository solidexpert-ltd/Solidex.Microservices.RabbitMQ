using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Solidex.Microservices.RabbitMQ.Attributes;
using Solidex.Microservices.RabbitMQ.Infrastructure;

namespace Solidex.Microservices.RabbitMQ.Chain
{
    /// <summary>
    /// Fluent builder for ChainMQ(request).ThenBy(...).ExecuteAsync pattern with auto-routing (MQ vs MediatR).
    /// </summary>
    public class RabbitChainBuilder<TCurrentRequest> : IRabbitChain<TCurrentRequest>
        where TCurrentRequest : class
    {
        private readonly IRabbitManager _manager;
        private readonly object _initialRequest;
        private readonly Type _initialRequestType;
        private readonly List<ChainStep> _steps;

        internal RabbitChainBuilder(IRabbitManager manager, object initialRequest, Type initialRequestType, List<ChainStep> steps)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _initialRequest = initialRequest ?? throw new ArgumentNullException(nameof(initialRequest));
            _initialRequestType = initialRequestType ?? throw new ArgumentNullException(nameof(initialRequestType));
            _steps = steps ?? throw new ArgumentNullException(nameof(steps));
        }

        /// <inheritdoc />
        public IRabbitChain<TNextRequest> ThenBy<TResponse, TNextRequest>(Func<TResponse, TNextRequest> map)
            where TResponse : class
            where TNextRequest : class
        {
            if (map == null)
                throw new ArgumentNullException(nameof(map));

            var newSteps = new List<ChainStep>(_steps)
            {
                new ChainStep
                {
                    ResponseType = typeof(TResponse),
                    NextRequestType = typeof(TNextRequest),
                    Map = o => map((TResponse)o)
                }
            };
            return new RabbitChainBuilder<TNextRequest>(_manager, _initialRequest, _initialRequestType, newSteps);
        }

        /// <inheritdoc />
        public async Task<TResponse> ExecuteAsync<TResponse>(CancellationToken ct = default)
            where TResponse : class
        {
            object currentResponse;
            if (_steps.Count == 0)
            {
                currentResponse = await DispatchAsync(_initialRequest, _initialRequestType, typeof(TResponse), ct);
                return (TResponse)currentResponse;
            }

            currentResponse = await DispatchAsync(_initialRequest, _initialRequestType, _steps[0].ResponseType, ct);

            for (var i = 0; i < _steps.Count; i++)
            {
                var step = _steps[i];
                var nextRequest = step.Map(currentResponse);
                var nextResponseType = i + 1 < _steps.Count ? _steps[i + 1].ResponseType : typeof(TResponse);
                currentResponse = await DispatchAsync(nextRequest, step.NextRequestType, nextResponseType, ct);
            }

            return (TResponse)currentResponse;
        }

        private async Task<object> DispatchAsync(object request, Type requestType, Type responseType, CancellationToken ct)
        {
            var hasRabbitAttr = requestType.GetCustomAttributes(typeof(RabbitMessageAttribute), false)
                .OfType<RabbitMessageAttribute>()
                .Any();

            if (hasRabbitAttr)
            {
                if (_manager is not RabbitManager rm)
                    throw new InvalidOperationException("ChainMQ requires the concrete RabbitManager implementation for MQ steps.");
                return await rm.SendObjectAsync(request, requestType, responseType, ct);
            }

            var sp = _manager is RabbitManager r ? r.ServiceProvider : null;
            if (sp == null)
                throw new InvalidOperationException("ChainMQ requires RabbitManager (with IServiceProvider) for MediatR steps.");
            var mediator = sp.GetService(typeof(IMediator)) as IMediator;
            if (mediator == null)
                throw new InvalidOperationException("IMediator must be registered when using MediatR steps in ChainMQ.");

            var sendMethod = typeof(IMediator).GetMethods()
                .FirstOrDefault(m => m.Name == "Send" && m.IsGenericMethod && m.GetGenericArguments().Length == 1
                    && m.GetParameters().Length == 2 && m.ReturnType.IsGenericType
                    && m.ReturnType.GetGenericTypeDefinition() == typeof(Task<>));
            if (sendMethod == null)
                throw new InvalidOperationException("Could not find IMediator.Send<TResponse> method.");
            var genericSend = sendMethod.MakeGenericMethod(responseType);
            var task = genericSend.Invoke(mediator, new object[] { request, ct });
            if (task == null)
                throw new InvalidOperationException("MediatR Send returned null.");
            await ((Task)task).ConfigureAwait(false);
            return task.GetType().GetProperty("Result")!.GetValue(task)!;
        }
    }
}
