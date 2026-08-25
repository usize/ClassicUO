using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ClassicUO;
using ClassicUO.Game;
using ClassicUO.Game.Data;
using ClassicUO.Network;
using ClassicUO.RestApi.Models;
using ClassicUO.Utility.Logging;
using Microsoft.AspNetCore.Mvc;

namespace ClassicUO.RestApi.Controllers
{
    [ApiController]
    [Route("api/actions")]
    public sealed class ActionsController : ControllerBase
    {
        private static readonly IReadOnlyDictionary<string, Direction> DirectionMap = new Dictionary<string, Direction>(StringComparer.OrdinalIgnoreCase)
        {
            ["n"] = Direction.North,
            ["north"] = Direction.North,
            ["ne"] = Direction.Right,
            ["northeast"] = Direction.Right,
            ["e"] = Direction.East,
            ["east"] = Direction.East,
            ["se"] = Direction.Down,
            ["southeast"] = Direction.Down,
            ["s"] = Direction.South,
            ["south"] = Direction.South,
            ["sw"] = Direction.Left,
            ["southwest"] = Direction.Left,
            ["w"] = Direction.West,
            ["west"] = Direction.West,
            ["nw"] = Direction.Up,
            ["northwest"] = Direction.Up
        };

        private static readonly IReadOnlyDictionary<string, MessageType> MessageTypeMap = new Dictionary<string, MessageType>(StringComparer.OrdinalIgnoreCase)
        {
            ["regular"] = MessageType.Regular,
            ["whisper"] = MessageType.Whisper,
            ["yell"] = MessageType.Yell,
            ["emote"] = MessageType.Emote,
            ["party"] = MessageType.Party,
            ["guild"] = MessageType.Guild
        };

        private readonly ActionQueue _actionQueue;

        public ActionsController(ActionQueue actionQueue)
        {
            _actionQueue = actionQueue;
        }

        [HttpPost("say")]
        public IActionResult Say([FromBody] SayRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Text))
            {
                return BadRequest("text is required");
            }

            var messageType = MessageTypeMap.TryGetValue(request.Type ?? string.Empty, out var mt)
                ? mt
                : MessageType.Regular;

            Enqueue(() => GameActions.Say(request.Text, type: messageType));
            return Accepted();
        }

        [HttpPost("move")]
        public IActionResult Move([FromBody] MoveRequest request)
        {
            Direction direction;

            if (request.DirectionIndex.HasValue)
            {
                var idx = request.DirectionIndex.Value;
                if (idx < 0 || idx > 7)
                    return BadRequest("directionIndex must be 0–7 (0=N, 1=NE, 2=E, 3=SE, 4=S, 5=SW, 6=W, 7=NW)");
                direction = (Direction)idx;
            }
            else if (!string.IsNullOrWhiteSpace(request.Direction))
            {
                if (!DirectionMap.TryGetValue(request.Direction, out direction))
                    return BadRequest("unknown direction, use: n/north, ne, e/east, se, s/south, sw, w/west, nw");
            }
            else
            {
                return BadRequest("provide 'direction' (string) or 'directionIndex' (0–7)");
            }

            Enqueue(() =>
            {
                var world = GetWorld();
                var player = world?.Player;
                player?.Walk(direction, request.Run);
            });

            return Accepted();
        }

        [HttpPost("use")]
        public IActionResult Use([FromBody] UseRequest request)
        {
            Enqueue(() =>
            {
                var world = GetWorld();
                if (world == null)
                {
                    return;
                }

                GameActions.DoubleClick(world, request.Serial);
            });

            return Accepted();
        }

        [HttpPost("attack")]
        public IActionResult Attack([FromBody] AttackRequest request)
        {
            Enqueue(() =>
            {
                var world = GetWorld();
                if (world == null)
                {
                    return;
                }

                GameActions.Attack(world, request.Serial);
            });

            return Accepted();
        }

        [HttpPost("skill")]
        public IActionResult UseSkill([FromBody] SkillRequest request)
        {
            Enqueue(() => GameActions.UseSkill(request.Index));
            return Accepted();
        }

        [HttpPost("spell")]
        public IActionResult CastSpell([FromBody] SpellRequest request)
        {
            Enqueue(() => GameActions.CastSpell(request.Index));
            return Accepted();
        }

        [HttpPost("warmode")]
        public IActionResult ToggleWarMode([FromBody] WarmodeRequest request)
        {
            Enqueue(() =>
            {
                var world = GetWorld();
                var player = world?.Player;
                if (player != null)
                {
                    GameActions.RequestWarMode(player, request.Enabled);
                }
            });

            return Accepted();
        }

        [HttpPost("pathfind")]
        public async Task<IActionResult> Pathfind([FromBody] PathfindRequest request)
        {
            int? pathLength;

            if (request.Serial.HasValue)
            {
                var serial = request.Serial.Value;
                pathLength = await EnqueueRun<int?>(() =>
                {
                    var world = GetWorld();
                    if (world?.Player == null) return null;

                    int tx, ty, tz;
                    if (world.Mobiles.TryGetValue(serial, out var mob) && mob != null && !mob.IsDestroyed)
                    {
                        tx = mob.X; ty = mob.Y; tz = mob.Z;
                    }
                    else if (world.Items.TryGetValue(serial, out var item) && item != null && !item.IsDestroyed)
                    {
                        tx = item.X; ty = item.Y; tz = item.Z;
                    }
                    else return null;

                    return world.Player.Pathfinder.WalkTo(tx, ty, tz, 1);
                });

                return PathfindAck(pathLength);
            }

            if (!request.X.HasValue || !request.Y.HasValue)
                return BadRequest("provide 'serial' to walk to an entity, or 'x' and 'y' for coordinates");

            var targetX = request.X.Value;
            var targetY = request.Y.Value;
            var targetZ = request.Z;
            pathLength = await EnqueueRun<int?>(() =>
            {
                var world = GetWorld();
                if (world?.Player == null) return null;
                return world.Player.Pathfinder.WalkTo(targetX, targetY, targetZ ?? world.Player.Z, 0);
            });

            return PathfindAck(pathLength);
        }

        /// <summary>Start server-side chunked travel to world coordinates (segmented A*, progress in player.travel).</summary>
        [HttpPost("travel")]
        public async Task<IActionResult> Travel([FromBody] TravelRequest request)
        {
            if (!request.X.HasValue || !request.Y.HasValue)
            {
                return BadRequest("provide 'x' and 'y'");
            }

            var goalX = request.X.Value;
            var goalY = request.Y.Value;

            var result = await EnqueueRun<TravelResultDto>(() =>
            {
                var world = GetWorld();
                if (world?.Player == null) return null;

                TravelState.GoalX = goalX;
                TravelState.GoalY = goalY;
                TravelState.GoalZ = (sbyte)(request.Z ?? world.Player.Z);
                TravelState.GoalZExplicit = request.Z.HasValue;
                TravelState.Active = true;
                TravelState.LastPathfindTicks = 0;
                TravelState.ConsecutiveFailures = 0;

                return new TravelResultDto(
                    true,
                    goalX,
                    goalY,
                    Math.Max(Math.Abs(world.Player.X - goalX), Math.Abs(world.Player.Y - goalY)));
            });

            if (result == null)
            {
                return Ok(new { active = false, goalX, goalY, tilesRemaining = 0, error = "timeout" });
            }

            return Ok(new { result.Active, result.GoalX, result.GoalY, result.TilesRemaining });
        }

        [HttpPost("stopwalk")]
        public IActionResult StopWalk()
        {
            Enqueue(() =>
            {
                TravelState.Active = false;
                GetWorld()?.Player?.Pathfinder.StopAutoWalk();
            });
            return Accepted();
        }

        /// <summary>Let pathfinding route through standing monsters (client-side, default off).</summary>
        [HttpPost("ignoremobiles")]
        public async Task<IActionResult> IgnoreMobiles([FromBody] IgnoreMobilesRequest request)
        {
            var value = await EnqueueRun<bool?>(() =>
            {
                Pathfinder.IgnoreMobileObstacles = request.Enabled;
                return Pathfinder.IgnoreMobileObstacles;
            });

            if (value == null)
            {
                return Ok(new { ignoreMobiles = request.Enabled, error = "timeout" });
            }

            return Ok(new { ignoreMobiles = value.Value });
        }

        /// <summary>Request a full client/server state resync (clears stuck walk state).</summary>
        [HttpPost("resync")]
        public IActionResult Resync()
        {
            Enqueue(() => NetClient.Socket.Send_Resync());
            return Ok(new { sent = true });
        }

        /// <summary>Opens the nearest door in the player's facing direction (server macro).</summary>
        [HttpPost("opendoor")]
        public IActionResult OpenDoor()
        {
            Enqueue(() => GameActions.OpenDoor());
            return Accepted();
        }

        /// <summary>Respond to a targeting cursor with a serial, coordinates, or cancel.</summary>
        [HttpPost("target")]
        public IActionResult Target([FromBody] TargetRequest request)
        {
            if (request.Cancel)
            {
                Enqueue(() => GetWorld()?.TargetManager?.CancelTarget());
                return Accepted();
            }

            if (request.Serial.HasValue)
            {
                var serial = request.Serial.Value;
                Enqueue(() => GetWorld()?.TargetManager?.Target(serial));
                return Accepted();
            }

            if (request.X.HasValue && request.Y.HasValue && request.Z.HasValue)
            {
                var x = request.X.Value;
                var y = request.Y.Value;
                var z = request.Z.Value;
                Enqueue(() => GetWorld()?.TargetManager?.Target(0, x, y, z));
                return Accepted();
            }

            return BadRequest("provide 'serial', 'x'+'y'+'z' for ground target, or 'cancel':true");
        }

        /// <summary>Pick up an item and move it directly to the player's backpack.</summary>
        [HttpPost("grab")]
        public IActionResult Grab([FromBody] GrabRequest request)
        {
            var serial = request.Serial;
            var amount = request.Amount;
            Enqueue(() =>
            {
                var world = GetWorld();
                if (world?.Player == null) return;

                ushort qty = amount > 0 ? amount : ushort.MaxValue;
                GameActions.GrabItem(world, serial, qty);
            });

            return Accepted();
        }

        [HttpPost("pickup")]
        public IActionResult PickUp([FromBody] PickUpRequest request)
        {
            var serial = request.Serial;
            var amount = request.Amount;
            Enqueue(() =>
            {
                var world = GetWorld();
                if (world?.Player == null) return;

                GameActions.PickUp(world, serial, 0, 0, amount);
            });

            return Accepted();
        }

        [HttpPost("equip")]
        public IActionResult Equip([FromBody] EquipRequest request)
        {
            var container = request.Container;
            Enqueue(() =>
            {
                var world = GetWorld();
                if (world?.Player == null) return;

                GameActions.Equip(world, container ?? 0);
            });

            return Accepted();
        }

        /// <summary>Drop an item into a container or on the ground. If the item is not currently held, it will be picked up first.</summary>
        [HttpPost("drop")]
        public IActionResult Drop([FromBody] DropRequest request)
        {
            Enqueue(() =>
            {
                var world = GetWorld();
                if (world?.Player == null) return;

                var held = Client.Game.UO.GameCursor.ItemHold;
                if (!held.Enabled || held.Serial != request.Serial)
                {
                    if (!world.Items.TryGetValue(request.Serial, out var item) || item == null || item.IsDestroyed)
                    {
                        Log.Warn($"Drop failed: item 0x{request.Serial:X8} not found");
                        return;
                    }
                    if (item.OnGround && item.Distance > Constants.DRAG_ITEMS_DISTANCE)
                    {
                        Log.Warn($"Drop failed: item 0x{request.Serial:X8} is {item.Distance} tiles away (max {Constants.DRAG_ITEMS_DISTANCE})");
                        return;
                    }
                    if (!GameActions.PickUp(world, request.Serial, 0, 0, -1))
                    {
                        Log.Warn($"Drop failed: could not pick up item 0x{request.Serial:X8}");
                        return;
                    }
                }

                var container = request.Container ?? 0xFFFF_FFFF;
                ushort x = request.X;
                ushort y = request.Y;
                sbyte z = request.Z;

                // For ground drops, 0xFFFF means "not specified" — drop at player's feet
                if (container == 0xFFFF_FFFF && (x == 0xFFFF || y == 0xFFFF))
                {
                    x = world.Player.X;
                    y = world.Player.Y;
                    z = world.Player.Z;
                }

                GameActions.DropItem(request.Serial, x, y, z, container);
            });

            return Accepted();
        }

        private static World GetWorld()
        {
            return Client.Game?.UO?.World;
        }

        private void Enqueue(Action action)
        {
            _actionQueue.Enqueue(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Log.Error($"REST action failed: {ex}");
                }
            });
        }

        private async Task<T> EnqueueRun<T>(Func<T> action, int timeoutMs = 5000)
        {
            try
            {
                return await _actionQueue.EnqueueAwait(action).WaitAsync(TimeSpan.FromMilliseconds(timeoutMs));
            }
            catch (OperationCanceledException)
            {
                return default;
            }
        }

        private IActionResult PathfindAck(int? pathLength)
        {
            if (pathLength == null)
            {
                return Ok(new { pathFound = false, pathLength = 0, error = "timeout" });
            }

            return Ok(new { pathFound = pathLength.Value > 0, pathLength = pathLength.Value });
        }
    }
}
