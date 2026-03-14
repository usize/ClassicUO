using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using ClassicUO.RestApi;

namespace ClassicUO.RestApi
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            // cuoapi.dll is a .NET Framework 4.0 assembly and can fail normal resolution
            // in .NET 10. Register a fallback that loads it explicitly from the output dir.
            AppDomain.CurrentDomain.AssemblyResolve += (_, e) =>
            {
                if (new AssemblyName(e.Name).Name == "cuoapi")
                {
                    var path = Path.Combine(AppContext.BaseDirectory, "cuoapi.dll");
                    return File.Exists(path) ? Assembly.LoadFrom(path) : null;
                }
                return null;
            };

            var actionQueue = new ActionQueue();
            var eventBus = new EventBus();

            Client.GameFactory = host => new ApiGameController(host, actionQueue, eventBus);

            await using var apiServer = ApiServer.Start(actionQueue, eventBus, 9000);
            await apiServer.WaitUntilStartedAsync();

            Bootstrap.Boot(null, args);
        }
    }
}
