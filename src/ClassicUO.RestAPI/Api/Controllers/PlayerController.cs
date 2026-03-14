using ClassicUO.RestApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace ClassicUO.RestApi.Controllers
{
    [ApiController]
    [Route("api/player")]
    public sealed class PlayerController : ControllerBase
    {
        [HttpGet]
        public IActionResult GetPlayer()
        {
            var player = WorldSnapshot.Current.Player;
            return player == null ? NotFound() : Ok(player);
        }

        [HttpGet("stats")]
        public IActionResult GetStats()
        {
            var stats = WorldSnapshot.Current.Player?.Stats;
            return stats == null ? NotFound() : Ok(stats);
        }

        [HttpGet("skills")]
        public IActionResult GetSkills()
        {
            var skills = WorldSnapshot.Current.Player?.Skills ?? System.Array.Empty<SkillDto>();
            return Ok(skills);
        }

        [HttpGet("inventory")]
        public IActionResult GetInventory()
        {
            var backpack = WorldSnapshot.Current.Player?.Backpack;
            return backpack == null ? NotFound() : Ok(backpack);
        }

        [HttpGet("equipment")]
        public IActionResult GetEquipment()
        {
            var equipment = WorldSnapshot.Current.Player?.Equipment ?? ItemDto.EmptyList;
            return Ok(equipment);
        }
    }
}
