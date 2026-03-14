using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;

namespace ClassicUO.RestApi.Controllers
{
    [ApiController]
    [Route("api/journal")]
    public sealed class JournalController : ControllerBase
    {
        [HttpGet]
        public IActionResult GetJournal([FromQuery] DateTime? since = null, [FromQuery] int limit = 100)
        {
            var entries = WorldSnapshot.Current.Journal.AsEnumerable();

            if (since.HasValue)
            {
                entries = entries.Where(e => e.Timestamp >= since.Value);
            }

            if (limit > 0)
            {
                entries = entries.Take(limit);
            }

            return Ok(entries);
        }
    }
}
