using System;
using System.Collections.Generic;
using ClassicUO;
using ClassicUO.Game;
using ClassicUO.Game.Data;
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
    }
}
