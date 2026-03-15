using System;
using System.Collections.Generic;
using ClassicUO.Game;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.RestApi.Models;
using ClassicUO.Utility.Logging;
using Microsoft.AspNetCore.Mvc;

namespace ClassicUO.RestApi.Controllers
{
    [ApiController]
    [Route("api/containers")]
    public sealed class ContainerController : ControllerBase
    {
        private readonly ActionQueue _actionQueue;

        public ContainerController(ActionQueue actionQueue)
        {
            _actionQueue = actionQueue;
        }

        /// <summary>List the contents of a container (backpack, chest, corpse, etc.).</summary>
        [HttpGet("{serial}")]
        public IActionResult GetContents(uint serial)
        {
            var tcs = new System.Threading.Tasks.TaskCompletionSource<IReadOnlyList<ItemDto>>();
            _actionQueue.Enqueue(() =>
            {
                try
                {
                    tcs.SetResult(ReadContents(serial));
                }
                catch (Exception ex)
                {
                    Log.Error($"Failed to read container 0x{serial:X8}: {ex}");
                    tcs.SetResult(ItemDto.EmptyList);
                }
            });

            return Ok(tcs.Task.GetAwaiter().GetResult());
        }

        /// <summary>Get the player's backpack contents.</summary>
        [HttpGet("backpack")]
        public IActionResult GetBackpack()
        {
            var tcs = new System.Threading.Tasks.TaskCompletionSource<object>();
            _actionQueue.Enqueue(() =>
            {
                try
                {
                    var world = Client.Game?.UO?.World;
                    var backpack = world?.Player?.FindItemByLayer(Layer.Backpack);
                    if (backpack == null)
                    {
                        tcs.SetResult(new { error = "No backpack found" });
                        return;
                    }

                    tcs.SetResult(new
                    {
                        serial = backpack.Serial,
                        items = ReadContents(backpack.Serial),
                    });
                }
                catch (Exception ex)
                {
                    Log.Error($"Failed to read backpack: {ex}");
                    tcs.SetResult(new { error = ex.Message });
                }
            });

            var result = tcs.Task.GetAwaiter().GetResult();
            return Ok(result);
        }

        private static IReadOnlyList<ItemDto> ReadContents(uint serial)
        {
            var world = Client.Game?.UO?.World;
            if (world == null) return ItemDto.EmptyList;

            Item container = world.Items.Get(serial);
            if (container == null) return ItemDto.EmptyList;

            var list = new List<ItemDto>();
            for (LinkedObject node = container.Items; node != null; node = node.Next)
            {
                if (node is Item child)
                {
                    list.Add(ItemDto.FromItem(child, includeContents: true));
                }
            }
            return list;
        }
    }
}
