﻿// Copyright (c) 2018-2019 Sergio Aquilini
// This code is licensed under MIT license (see LICENSE file for details)

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Silverback.Messaging.Broker;
using Silverback.Messaging.ErrorHandling;
using Silverback.Messaging.LargeMessages;
using Silverback.Messaging.Messages;
using Silverback.Messaging.Publishing;

namespace Silverback.Messaging.Connectors
{
    /// <summary>
    /// Subscribes to a message broker and forwards the incoming integration messages to the internal bus.
    /// </summary>
    public class InboundConnector : IInboundConnector
    {
        private readonly IBroker _broker;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger _logger;
        private readonly List<InboundConsumer> _inboundConsumers = new List<InboundConsumer>();

        public InboundConnector(IBroker broker, IServiceProvider serviceProvider, ILogger<InboundConnector> logger)
        {
            _broker = broker ?? throw new ArgumentNullException(nameof(broker));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public virtual IInboundConnector Bind(IEndpoint endpoint, IErrorPolicy errorPolicy = null, InboundConnectorSettings settings = null)
        {
            settings = settings ?? new InboundConnectorSettings();

            for (var i = 0; i < settings.Consumers; i++)
            {
                _inboundConsumers.Add(new InboundConsumer(
                    _broker,
                    endpoint,
                    settings,
                    HandleMessages,
                    Commit,
                    Rollback,
                    errorPolicy,
                    _serviceProvider));
            }

            // TODO: Carefully test with multiple endpoints!
            // TODO: Test if consumer gets properly disposed etc.
            return this;
        }

        protected void HandleMessages(IEnumerable<IInboundMessage> messages, IServiceProvider serviceProvider)
        {
            var deserializedMessages = messages
                .Select(message => HandleChunkedMessage(message, serviceProvider))
                .Where(args => args != null)
                .Select(DeserializeRawMessage)
                .ToList();

            if (!deserializedMessages.Any())
                return;

            RelayMessages(deserializedMessages, serviceProvider);
        }

        private IInboundMessage HandleChunkedMessage(IInboundMessage message, IServiceProvider serviceProvider)
        {
            if (!message.Headers.Contains(MessageHeader.ChunkIdKey))
                return message;

            var completeMessage = serviceProvider.GetRequiredService<ChunkConsumer>().JoinIfComplete(message);

            return completeMessage == null 
                ? null 
                : new InboundMessage(completeMessage, message.Headers, message.Offset, message.Endpoint, message.MustUnwrap);
        }

        private static IInboundMessage DeserializeRawMessage(IInboundMessage message)
        {
            var deserialized =
                message.Content ?? (((InboundMessage) message).Content =
                    message.Endpoint.Serializer.Deserialize(message.RawContent, message.Headers));

            // Create typed message for easier specific subscription
            var typedInboundMessage = (InboundMessage) Activator.CreateInstance(
                typeof(InboundMessage<>).MakeGenericType(deserialized.GetType()),
                message);

            typedInboundMessage.Content = deserialized;

            return typedInboundMessage;
        }

        protected virtual void RelayMessages(IEnumerable<IInboundMessage> messages, IServiceProvider serviceProvider) => 
            serviceProvider.GetRequiredService<IPublisher>().Publish(messages);

        protected virtual void Commit(IServiceProvider serviceProvider) => 
            serviceProvider.GetService<ChunkConsumer>()?.Commit();

        protected virtual void Rollback(IServiceProvider serviceProvider) => 
            serviceProvider.GetService<ChunkConsumer>()?.Rollback();
    }
}