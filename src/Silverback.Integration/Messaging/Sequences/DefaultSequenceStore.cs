﻿// Copyright (c) 2020 Sergio Aquilini
// This code is licensed under MIT license (see LICENSE file for details)

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Silverback.Diagnostics;
using Silverback.Messaging.Sequences.Unbounded;
using Silverback.Util;

namespace Silverback.Messaging.Sequences
{
    internal sealed class DefaultSequenceStore : ISequenceStore
    {
        private readonly Guid _id = Guid.NewGuid();

        private readonly Dictionary<string, ISequence> _store = new();

        private readonly ISilverbackIntegrationLogger<DefaultSequenceStore> _logger;

        public DefaultSequenceStore(ISilverbackIntegrationLogger<DefaultSequenceStore> logger)
        {
            _logger = logger;
        }

        public int Count => _store.Count;

        public Task<TSequence?> GetAsync<TSequence>(string sequenceId, bool matchPrefix = false)
            where TSequence : class, ISequence
        {
            ISequence? sequence;

            if (matchPrefix)
            {
                sequence = _store.FirstOrDefault(
                    keyValuePair => keyValuePair.Key.StartsWith(sequenceId, StringComparison.Ordinal)).Value;
            }
            else
            {
                _store.TryGetValue(sequenceId, out sequence);
            }

            if (sequence is ISequenceImplementation sequenceImpl)
                sequenceImpl.SetIsNew(false);

            return Task.FromResult((TSequence?)sequence);
        }

        public async Task<TSequence> AddAsync<TSequence>(TSequence sequence)
            where TSequence : class, ISequence
        {
            Check.NotNull(sequence, nameof(sequence));

            _logger.LogTrace(
                IntegrationEventIds.LowLevelTracing,
                "Adding {sequenceType} '{sequenceId}' to store '{sequenceStoreId}'.",
                sequence.GetType().Name,
                sequence.SequenceId,
                _id);

            if (_store.TryGetValue(sequence.SequenceId, out var oldSequence))
                await oldSequence.AbortAsync(SequenceAbortReason.IncompleteSequence).ConfigureAwait(false);

            _store[sequence.SequenceId] = sequence;

            return sequence;
        }

        public Task RemoveAsync(string sequenceId)
        {
            _logger.LogTrace(
                IntegrationEventIds.LowLevelTracing,
                "Removing sequence '{sequenceId}' from store '{sequenceStoreId}'.",
                sequenceId,
                _id);

            _store.Remove(sequenceId);
            return Task.CompletedTask;
        }

        public IReadOnlyCollection<ISequence> GetPendingSequences(bool includeUnbounded = false) =>
            _store.Values.Where(
                    sequence =>
                        sequence.IsPending && (includeUnbounded || !(sequence is UnboundedSequence)))
                .ToList();

        public IEnumerator<ISequence> GetEnumerator() => _store.Values.GetEnumerator();

        public void Dispose()
        {
            _logger.LogTrace(
                IntegrationEventIds.LowLevelTracing,
                "Disposing sequence store {sequenceStoreId}",
                _id);

            AsyncHelper.RunSynchronously(
                () => _store.Values
                    .Where(sequence => sequence.IsPending)
                    .ForEachAsync(sequence => sequence.AbortAsync(SequenceAbortReason.Disposing)));
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
