using System;
using System.Collections.Generic;
using System.Linq;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Gumps;
using ClassicUO.Network;
using ClassicUO.RestApi.Models;
using ClassicUO.Utility.Logging;
using Microsoft.AspNetCore.Mvc;

namespace ClassicUO.RestApi.Controllers
{
    [ApiController]
    [Route("api/shop")]
    public sealed class ShopController : ControllerBase
    {
        private readonly ActionQueue _actionQueue;

        public ShopController(ActionQueue actionQueue)
        {
            _actionQueue = actionQueue;
        }

        /// <summary>Get the currently open shop gump's inventory.</summary>
        [HttpGet]
        public IActionResult GetShop()
        {
            ShopStateDto state = null;
            // Must read UI state on the game thread
            var tcs = new System.Threading.Tasks.TaskCompletionSource<ShopStateDto>();
            _actionQueue.Enqueue(() =>
            {
                try
                {
                    tcs.SetResult(ReadShopState());
                }
                catch (Exception ex)
                {
                    Log.Error($"Failed to read shop state: {ex}");
                    tcs.SetResult(new ShopStateDto { IsOpen = false });
                }
            });

            state = tcs.Task.GetAwaiter().GetResult();
            return Ok(state);
        }

        /// <summary>Buy items from the currently open vendor shop.</summary>
        [HttpPost("buy")]
        public IActionResult Buy([FromBody] ShopBuyRequest request)
        {
            if (request.Items == null || request.Items.Count == 0)
                return BadRequest("items array is required");

            var items = request.Items
                .Select(i => new Tuple<uint, ushort>(i.Serial, i.Quantity))
                .ToArray();

            _actionQueue.Enqueue(() =>
            {
                try
                {
                    var gump = UIManager.GetGump<ShopGump>();
                    if (gump == null || !gump.IsBuyGump)
                    {
                        Log.Warn("Buy request but no buy gump is open");
                        return;
                    }

                    NetClient.Socket.Send_BuyRequest(gump.LocalSerial, items);
                }
                catch (Exception ex)
                {
                    Log.Error($"Buy request failed: {ex}");
                }
            });

            return Accepted();
        }

        /// <summary>Sell items to the currently open vendor shop.</summary>
        [HttpPost("sell")]
        public IActionResult Sell([FromBody] ShopBuyRequest request)
        {
            if (request.Items == null || request.Items.Count == 0)
                return BadRequest("items array is required");

            var items = request.Items
                .Select(i => new Tuple<uint, ushort>(i.Serial, i.Quantity))
                .ToArray();

            _actionQueue.Enqueue(() =>
            {
                try
                {
                    var gump = UIManager.GetGump<ShopGump>();
                    if (gump == null || gump.IsBuyGump)
                    {
                        Log.Warn("Sell request but no sell gump is open");
                        return;
                    }

                    NetClient.Socket.Send_SellRequest(gump.LocalSerial, items);
                }
                catch (Exception ex)
                {
                    Log.Error($"Sell request failed: {ex}");
                }
            });

            return Accepted();
        }

        private static ShopStateDto ReadShopState()
        {
            var gump = UIManager.GetGump<ShopGump>();
            if (gump == null)
                return new ShopStateDto { IsOpen = false };

            var snapshot = gump.GetShopItemSnapshot();
            var items = new List<ShopItemDto>(snapshot.Count);
            foreach (var si in snapshot)
            {
                items.Add(new ShopItemDto
                {
                    Serial = si.Serial,
                    Graphic = si.Graphic,
                    Hue = si.Hue,
                    Amount = si.Amount,
                    Price = si.Price,
                    Name = si.Name,
                });
            }

            return new ShopStateDto
            {
                IsOpen = true,
                IsBuyGump = gump.IsBuyGump,
                VendorSerial = gump.LocalSerial,
                Items = items,
            };
        }
    }
}
