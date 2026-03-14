using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ClassicUO.RestApi
{
    public static class ApiServer
    {
        public static ApiServerHandle Start(ActionQueue actionQueue, EventBus eventBus, int port)
        {
            var builder = WebApplication.CreateBuilder();
            builder.Services.AddSingleton(actionQueue);
            builder.Services.AddSingleton(eventBus);
            builder.Services
                .AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                });

            var app = builder.Build();

            app.MapControllers();

            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            app.Lifetime.ApplicationStarted.Register(() => started.TrySetResult());

            var task = app.RunAsync($"http://127.0.0.1:{port}");

            return new ApiServerHandle(app, task, started.Task);
        }
    }

    public sealed class ApiServerHandle : IAsyncDisposable
    {
        private readonly WebApplication _app;
        private readonly Task _runTask;
        private readonly Task _started;

        internal ApiServerHandle(WebApplication app, Task runTask, Task started)
        {
            _app = app;
            _runTask = runTask;
            _started = started;
        }

        public Task WaitUntilStartedAsync() => _started;

        public async ValueTask DisposeAsync()
        {
            _app.Lifetime.StopApplication();
            await _runTask.ConfigureAwait(false);
        }
    }
}
