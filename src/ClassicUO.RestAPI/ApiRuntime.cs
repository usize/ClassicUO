using System;

namespace ClassicUO.RestApi
{
    internal static class ApiRuntime
    {
        public static DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;

        public static TimeSpan Uptime => DateTimeOffset.UtcNow - StartedAt;
    }
}
