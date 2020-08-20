﻿// Copyright (c) 2020 Sergio Aquilini
// This code is licensed under MIT license (see LICENSE file for details)

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Silverback.Diagnostics;
using Silverback.Messaging.Broker;
using Silverback.Messaging.Broker.Behaviors;
using Silverback.Messaging.Connectors.Repositories;
using Silverback.Messaging.Messages;
using Silverback.Util;

namespace Silverback.Messaging.Connectors
{
    /// <inheritdoc cref="Producer{TBroker,TEndpoint}" />
    public class OutboundQueueProducer : Producer<OutboundQueueBroker, IProducerEndpoint>
    {
        private readonly IOutboundQueueWriter _queueWriter;

        /// <summary>
        ///     Initializes a new instance of the <see cref="OutboundQueueProducer" /> class.
        /// </summary>
        /// <param name="queueWriter">
        ///     The <see cref="IOutboundQueueWriter"/> to be used to write to the queue.
        /// </param>
        /// <param name="broker">
        ///     The <see cref="IBroker" /> that instantiated this producer.
        /// </param>
        /// <param name="endpoint">
        ///     The endpoint to produce to.
        /// </param>
        /// <param name="behaviors">
        ///     The behaviors to be added to the pipeline.
        /// </param>
        /// <param name="logger">
        ///     The <see cref="ISilverbackIntegrationLogger" />.
        /// </param>
        public OutboundQueueProducer(
            IOutboundQueueWriter queueWriter,
            OutboundQueueBroker broker,
            IProducerEndpoint endpoint,
            IReadOnlyList<IProducerBehavior>? behaviors,
            ISilverbackIntegrationLogger<Producer> logger)
            : base(broker, endpoint, behaviors, logger)
        {
            _queueWriter = queueWriter;
        }

        /// <inheritdoc cref="Producer.ProduceCore" />
        protected override IOffset ProduceCore(IOutboundEnvelope envelope)
        {
            throw new InvalidOperationException("Only asynchronous operations are supported.");
        }

        /// <inheritdoc cref="Producer.ProduceAsyncCore" />
        protected override async Task<IOffset?> ProduceAsyncCore(IOutboundEnvelope envelope)
        {
            Check.NotNull(envelope, nameof(envelope));

            await _queueWriter.Enqueue(envelope).ConfigureAwait(false);

            return null;
        }
    }
}
