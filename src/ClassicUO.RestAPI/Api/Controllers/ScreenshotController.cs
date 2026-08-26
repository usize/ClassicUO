using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ClassicUO.RestApi.Controllers
{
    // Returns the current rendered scene (isometric world view, no UI overlays)
    // as a PNG image. Captured from the world render target on the game thread.
    [ApiController]
    [Route("api/screenshot")]
    public sealed class ScreenshotController : ControllerBase
    {
        private static readonly TimeSpan CaptureTimeout = TimeSpan.FromSeconds(5);

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            if (!WorldSnapshot.Current.InGame)
            {
                return StatusCode(StatusCodes.Status409Conflict, new { error = "not in game" });
            }

            byte[] png;
            try
            {
                png = await ScreenshotCapture.RequestAsync(CaptureTimeout);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
            }

            if (png == null)
            {
                return StatusCode(StatusCodes.Status504GatewayTimeout, new { error = "capture timed out" });
            }

            return File(png, "image/png");
        }
    }
}
