using System;
using System.Linq;
using System.Threading.Tasks;
using ClassicUO.RestApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace ClassicUO.RestApi.Controllers
{
    [ApiController]
    [Route("api/world")]
    public sealed class WorldController : ControllerBase
    {
        private readonly ActionQueue _actionQueue;

        public WorldController(ActionQueue actionQueue)
        {
            _actionQueue = actionQueue;
        }

        [HttpGet]
        public IActionResult GetWorld() =>
            Ok(new WorldStateDto(WorldSnapshot.Current));

        [HttpGet("mobiles")]
        public IActionResult GetMobiles() =>
            Ok(WorldSnapshot.Current.Mobiles);

        [HttpGet("mobiles/{serial}")]
        public IActionResult GetMobile(uint serial)
        {
            var mobile = WorldSnapshot.Current.Mobiles.FirstOrDefault(m => m.Serial == serial);
            return mobile == null ? NotFound() : Ok(mobile);
        }

        /// <summary>
        /// Returns the full paperdoll for any nearby mobile — equipment is read live
        /// on the game thread from Mobile.Items (populated by the server's 0x78 packet).
        /// No extra server request is needed for mobiles already in visual range.
        /// </summary>
        [HttpGet("mobiles/{serial}/paperdoll")]
        public async Task<IActionResult> GetPaperdoll(uint serial)
        {
            var tcs = new TaskCompletionSource<PaperdollDto?>(TaskCreationOptions.RunContinuationsAsynchronously);

            _actionQueue.Enqueue(() =>
            {
                try
                {
                    var world = Client.Game?.UO?.World;
                    if (world == null || !world.Mobiles.TryGetValue(serial, out var mob) || mob.IsDestroyed)
                    {
                        tcs.TrySetResult(null);
                        return;
                    }
                    tcs.TrySetResult(PaperdollDto.From(mob));
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });

            var result = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(3));
            return result == null ? NotFound($"Mobile 0x{serial:X8} not in range.") : Ok(result);
        }

        [HttpGet("items")]
        public IActionResult GetItems() =>
            Ok(WorldSnapshot.Current.GroundItems);

        [HttpGet("items/{serial}")]
        public IActionResult GetItem(uint serial)
        {
            var item = WorldSnapshot.Current.GroundItems.FirstOrDefault(i => i.Serial == serial);
            return item == null ? NotFound() : Ok(item);
        }

        /// <summary>
        /// Returns the current tile grid snapshot centered on the player.
        /// Width=41, Height=21 (RadiusX=20, RadiusY=10).
        /// Cell values: ' '=unloaded, '.'=open, '#'=wall, '+'=door, '~'=water,
        ///              '^'=stair up, 'v'=stair down, 'X'=stair both directions.
        /// World coords: worldX = playerX + (colIndex - radiusX),
        ///               worldY = playerY + (rowIndex - radiusY).
        /// </summary>
        [HttpGet("tiles")]
        public IActionResult GetTiles()
        {
            var grid = WorldSnapshot.CurrentTileGrid;
            var rows = new string[TileGrid.Height];
            for (int gy = 0; gy < TileGrid.Height; gy++)
            {
                var sb = new System.Text.StringBuilder(TileGrid.Width);
                for (int gx = 0; gx < TileGrid.Width; gx++)
                {
                    char c = grid.Cells[gx, gy];
                    sb.Append(c == '\0' ? ' ' : c);
                }
                rows[gy] = sb.ToString();
            }
            return Ok(new
            {
                playerX = (int)grid.PlayerX,
                playerY = (int)grid.PlayerY,
                playerZ = (int)grid.PlayerZ,
                radiusX = TileGrid.RadiusX,
                radiusY = TileGrid.RadiusY,
                width   = TileGrid.Width,
                height  = TileGrid.Height,
                rows    = rows,
            });
        }
    }
}
