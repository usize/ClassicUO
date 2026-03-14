using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;
using ClassicUO.RestApi.Models;

namespace ClassicUO.RestApi
{
    public sealed class EventBus
    {
        private readonly ConcurrentDictionary<Guid, Channel<SseEvent>> _consumers = new();

        public ChannelReader<SseEvent> Subscribe(CancellationToken cancellationToken)
        {
            var channel = Channel.CreateUnbounded<SseEvent>(new UnboundedChannelOptions
            {
                SingleReader = false,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });

            var id = Guid.NewGuid();
            _consumers[id] = channel;

            cancellationToken.Register(() =>
            {
                channel.Writer.TryComplete();
                _consumers.TryRemove(id, out _);
            });

            return channel.Reader;
        }

        public void Publish(SseEvent e)
        {
            foreach (var consumer in _consumers.Values)
            {
                consumer.Writer.TryWrite(e);
            }
        }

        public void Publish(string type, object data)
        {
            Publish(SseEvent.Create(type, data));
        }
    }
}
