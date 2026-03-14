using System;

namespace ClassicUO.RestApi.Models
{
    public sealed record SseEvent(string Type, DateTimeOffset Timestamp, object Data)
    {
        public static SseEvent Create(string type, object data)
        {
            return new SseEvent(type, DateTimeOffset.UtcNow, data ?? new { });
        }
    }
}
