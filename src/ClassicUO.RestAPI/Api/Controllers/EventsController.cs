using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace ClassicUO.RestApi.Controllers
{
    [ApiController]
    [Route("api/events")]
    public sealed class EventsController : ControllerBase
    {
        private readonly EventBus _eventBus;

        public EventsController(EventBus eventBus)
        {
            _eventBus = eventBus;
        }

        [HttpGet]
        public async Task GetEvents()
        {
            Response.Headers["Cache-Control"] = "no-cache";
            Response.Headers["Connection"] = "keep-alive";
            Response.Headers["Content-Type"] = "text/event-stream";

            var reader = _eventBus.Subscribe(HttpContext.RequestAborted);
            var cancellation = HttpContext.RequestAborted;

            await foreach (var e in reader.ReadAllAsync(cancellation))
            {
                var json = JsonSerializer.Serialize(e);
                var payload = Encoding.UTF8.GetBytes($"data: {json}\n\n");
                await Response.Body.WriteAsync(payload, cancellation);
                await Response.Body.FlushAsync(cancellation);
            }
        }
    }
}
