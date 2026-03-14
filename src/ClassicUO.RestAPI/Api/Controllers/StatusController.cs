using Microsoft.AspNetCore.Mvc;

namespace ClassicUO.RestApi.Controllers
{
    [ApiController]
    [Route("api/status")]
    public sealed class StatusController : ControllerBase
    {
        [HttpGet]
        public IActionResult GetStatus()
        {
            var snapshot = WorldSnapshot.Current;
            return Ok(new
            {
                snapshot.Connected,
                snapshot.InGame,
                snapshot.Server,
                snapshot.Timestamp,
                startedAt = ApiRuntime.StartedAt,
                uptimeSeconds = ApiRuntime.Uptime.TotalSeconds
            });
        }
    }
}
