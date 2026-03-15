using System;
using System.Collections.Generic;
using System.Linq;
using ClassicUO.Game;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Game.UI.Gumps;
using ClassicUO.RestApi.Models;
using ClassicUO.Utility.Logging;
using Microsoft.AspNetCore.Mvc;

namespace ClassicUO.RestApi.Controllers
{
    [ApiController]
    [Route("api/gumps")]
    public sealed class GumpController : ControllerBase
    {
        private readonly ActionQueue _actionQueue;

        public GumpController(ActionQueue actionQueue)
        {
            _actionQueue = actionQueue;
        }

        /// <summary>List all open gumps with their controls.</summary>
        [HttpGet]
        public IActionResult GetGumps()
        {
            var tcs = new System.Threading.Tasks.TaskCompletionSource<List<GumpDto>>();
            _actionQueue.Enqueue(() =>
            {
                try
                {
                    tcs.SetResult(ReadGumps());
                }
                catch (Exception ex)
                {
                    Log.Error($"Failed to read gumps: {ex}");
                    tcs.SetResult([]);
                }
            });

            return Ok(tcs.Task.GetAwaiter().GetResult());
        }

        /// <summary>Respond to a gump by clicking a button.</summary>
        [HttpPost("{localSerial}/respond")]
        public IActionResult Respond(uint localSerial, [FromBody] GumpResponseRequest request)
        {
            var switches = request.Switches?.ToArray() ?? [];
            var entries = request.TextEntries?
                .Select(e => new Tuple<ushort, string>(e.Serial, e.Text ?? string.Empty))
                .ToArray() ?? [];

            _actionQueue.Enqueue(() =>
            {
                try
                {
                    var gump = FindGump(localSerial);
                    if (gump == null)
                    {
                        Log.Warn($"Gump 0x{localSerial:X8} not found for response");
                        return;
                    }

                    GameActions.ReplyGump(
                        gump.LocalSerial,
                        gump.ServerSerial,
                        request.ButtonID,
                        switches,
                        entries
                    );

                    gump.Dispose();
                }
                catch (Exception ex)
                {
                    Log.Error($"Gump response failed: {ex}");
                }
            });

            return Accepted();
        }

        /// <summary>Close a gump (sends button 0 = cancel/close).</summary>
        [HttpDelete("{localSerial}")]
        public IActionResult Close(uint localSerial)
        {
            _actionQueue.Enqueue(() =>
            {
                try
                {
                    var gump = FindGump(localSerial);
                    if (gump == null) return;

                    // Button 0 = close/cancel for server gumps
                    if (gump.IsFromServer && gump.ServerSerial != 0)
                    {
                        GameActions.ReplyGump(gump.LocalSerial, gump.ServerSerial, 0, null, null);
                    }

                    gump.Dispose();
                }
                catch (Exception ex)
                {
                    Log.Error($"Gump close failed: {ex}");
                }
            });

            return Accepted();
        }

        private static Gump FindGump(uint localSerial)
        {
            foreach (var gump in UIManager.Gumps)
            {
                if (gump.LocalSerial == localSerial)
                    return gump;
            }
            return null;
        }

        private static List<GumpDto> ReadGumps()
        {
            var result = new List<GumpDto>();

            foreach (var gump in UIManager.Gumps)
            {
                // Skip internal client gumps that aren't useful to the AI
                if (!gump.IsFromServer && gump.GumpType != GumpType.None)
                    continue;

                // Only expose server-sent gumps (NPC dialogs, menus, etc.)
                if (!gump.IsFromServer)
                    continue;

                var dto = new GumpDto
                {
                    LocalSerial = gump.LocalSerial,
                    ServerSerial = gump.ServerSerial,
                    IsFromServer = gump.IsFromServer,
                    GumpType = gump.GumpType.ToString(),
                    X = gump.X,
                    Y = gump.Y,
                };

                var buttons = new List<GumpButtonDto>();
                var textLines = new List<string>();
                var checkboxes = new List<GumpCheckboxDto>();
                var textEntries = new List<GumpTextEntryDto>();

                CollectControls(gump, buttons, textLines, checkboxes, textEntries);

                dto.Buttons = buttons;
                dto.TextLines = textLines;
                dto.Checkboxes = checkboxes;
                dto.TextEntries = textEntries;

                result.Add(dto);
            }

            return result;
        }

        private static void CollectControls(
            Control parent,
            List<GumpButtonDto> buttons,
            List<string> textLines,
            List<GumpCheckboxDto> checkboxes,
            List<GumpTextEntryDto> textEntries)
        {
            foreach (var child in parent.Children)
            {
                switch (child)
                {
                    case Button btn when btn.ButtonAction == ButtonAction.Activate:
                        buttons.Add(new GumpButtonDto { ButtonID = btn.ButtonID, Page = btn.Page });
                        break;

                    case Label lbl when !string.IsNullOrWhiteSpace(lbl.Text):
                        textLines.Add(lbl.Text);
                        break;

                    case HtmlControl html when !string.IsNullOrWhiteSpace(html.Text):
                        textLines.Add(html.Text);
                        break;

                    case Checkbox cb:
                        checkboxes.Add(new GumpCheckboxDto
                        {
                            Serial = cb.LocalSerial,
                            IsChecked = cb.IsChecked,
                        });
                        break;

                    case StbTextBox tb:
                        textEntries.Add(new GumpTextEntryDto
                        {
                            Serial = tb.LocalSerial,
                            Text = tb.Text ?? string.Empty,
                        });
                        break;
                }

                // Recurse into child containers
                if (child.Children.Count > 0)
                    CollectControls(child, buttons, textLines, checkboxes, textEntries);
            }
        }
    }
}
