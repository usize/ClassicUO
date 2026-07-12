using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ClassicUO.RestApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace ClassicUO.RestApi.Controllers
{
    [ApiController]
    [Route("api/summary")]
    public sealed class SummaryController : ControllerBase
    {
        private static readonly string[] MapNames =
            { "Felucca", "Trammel", "Ilshenar", "Malas", "Tokuno", "TerMur" };

        private static readonly string[] FacingNames =
            { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

        [HttpGet]
        [Produces("text/plain")]
        public ContentResult GetSummary()
        {
            var snap = WorldSnapshot.Current;
            var grid = WorldSnapshot.CurrentTileGrid;
            var sb   = new StringBuilder();

            // ── Status bar ───────────────────────────────────────────────────────
            var status  = snap.InGame ? "IN GAME" : (snap.Connected ? "connected" : "offline");
            var server  = string.IsNullOrEmpty(snap.Server) ? "—" : snap.Server;
            var uptime  = ApiRuntime.Uptime;
            var uptimeS = uptime.TotalHours >= 1
                ? $"{(int)uptime.TotalHours}h{uptime.Minutes:D2}m"
                : $"{uptime.Minutes}m{uptime.Seconds:D2}s";

            Divider(sb);
            sb.AppendLine($"  {status}  •  {server}  •  up {uptimeS}");
            Divider(sb);

            if (snap.Player is not PlayerDto p)
            {
                sb.AppendLine("  Not in game.");
                Divider(sb);
                return Content(sb.ToString(), "text/plain");
            }

            // ── Player ───────────────────────────────────────────────────────────
            var map    = p.MapIndex < MapNames.Length ? MapNames[p.MapIndex] : $"Map{p.MapIndex}";
            var facing = FacingNames[p.Direction & 7];
            var st     = p.Stats;
            var war    = p.InWarMode ? "  [WAR]"  : "";
            var dead   = p.IsDead    ? "  [DEAD]" : "";

            var targeting = p.IsTargeting ? $"  [TARGET CURSOR ACTIVE — {p.TargetingType}, click now: ./ai/uo target <serial>]" : "";

            sb.AppendLine($"  {p.Name}  (0x{p.Serial:X8})  {map}  ({p.X}, {p.Y}, {p.Z})  facing {facing}{war}{dead}{targeting}");
            sb.AppendLine($"  HP {st.Hits}/{st.HitsMax}  MP {st.Mana}/{st.ManaMax}  Stam {st.Stamina}/{st.StaminaMax}  Gold {st.Gold:N0}  Wt {st.Weight}/{st.WeightMax}");
            if (st.Followers > 0)
                sb.AppendLine($"  Pets: {st.Followers}/{st.FollowersMax}");

            var topSkills = p.Skills.Where(sk => sk.Value > 0)
                                    .OrderByDescending(sk => sk.Value)
                                    .Take(6).ToList();
            if (topSkills.Count > 0)
                sb.AppendLine("  Skills: " + string.Join("  ", topSkills.Select(sk => $"{sk.Name} {sk.Value:F1}")));

            // ── Build overlay (entities on top of tile grid) ─────────────────────
            var overlay = new char[TileGrid.Width, TileGrid.Height];
            var glyphsUsed = new SortedSet<char>();

            void Place(int gx, int gy, char glyph)
            {
                if (gx < 0 || gx >= TileGrid.Width || gy < 0 || gy >= TileGrid.Height) return;
                if (overlay[gx, gy] == '\0' || TileSymbols.Priority(glyph) > TileSymbols.Priority(overlay[gx, gy]))
                    overlay[gx, gy] = glyph;
            }

            foreach (var item in snap.GroundItems)
            {
                var (glyph, _) = TileSymbols.ForItem(item);
                int gx = item.X - grid.PlayerX + TileGrid.RadiusX;
                int gy = item.Y - grid.PlayerY + TileGrid.RadiusY;
                Place(gx, gy, glyph);
                glyphsUsed.Add(glyph);
            }

            foreach (var mob in snap.Mobiles)
            {
                var (glyph, _) = TileSymbols.ForMobile(mob);
                int gx = mob.X - grid.PlayerX + TileGrid.RadiusX;
                int gy = mob.Y - grid.PlayerY + TileGrid.RadiusY;
                Place(gx, gy, glyph);
                glyphsUsed.Add(glyph);
            }

            // You are always at center
            overlay[TileGrid.RadiusX, TileGrid.RadiusY] = '@';
            glyphsUsed.Add('@');

            // Collect terrain glyphs that appear in the grid
            for (int gy = 0; gy < TileGrid.Height; gy++)
                for (int gx = 0; gx < TileGrid.Width; gx++)
                {
                    char c = grid.Cells[gx, gy];
                    if (c == '#' || c == '+' || c == '~') glyphsUsed.Add(c);
                }

            // ── Legend ───────────────────────────────────────────────────────────
            Divider(sb);
            sb.Append("  KEY  ");
            foreach (var g in glyphsUsed)
            {
                if (TileSymbols.Descriptions.TryGetValue(g, out var desc))
                    sb.Append($"{g}={desc}  ");
            }
            sb.AppendLine();

            // ── ASCII map  (N at top, 41 wide × 21 tall) ─────────────────────────
            sb.AppendLine($"  MAP  {TileGrid.RadiusX}t E/W  {TileGrid.RadiusY}t N/S  (N↑)");

            for (int gy = 0; gy < TileGrid.Height; gy++)
            {
                sb.Append("  ");
                for (int gx = 0; gx < TileGrid.Width; gx++)
                {
                    char ch = overlay[gx, gy];
                    if (ch != '\0')
                        sb.Append(ch);
                    else
                    {
                        char tile = grid.Cells[gx, gy];
                        sb.Append(tile == '\0' ? ' ' : tile);
                    }
                }
                sb.AppendLine();
            }

            // ── Stairs ───────────────────────────────────────────────────────────
            if (grid.Stairs.Count > 0)
            {
                Divider(sb);
                sb.AppendLine("  STAIRS  (^ = up to next floor, v = down, X = both)");
                foreach (var stair in grid.Stairs)
                {
                    int dx = stair.X - p.X;
                    int dy = stair.Y - p.Y;
                    var dir = Bearing(dx, dy);
                    var kind = (stair.Up && stair.Down) ? "up+down" : stair.Up ? "up" : "down";
                    sb.AppendLine($"  Stairs {kind,-7}  ({stair.X}, {stair.Y})  {dir}  DX {dx:+#;-#;0} DY {dy:+#;-#;0}");
                    sb.AppendLine($"    → To go {kind}: ./ai/uo goto {stair.X} {stair.Y}");
                }
            }

            // ── Entity list ──────────────────────────────────────────────────────
            Divider(sb);
            var entities = BuildEntityRows(snap, p);

            if (entities.Count > 0)
            {
                sb.AppendLine($"  {"":1}  {"SERIAL",-12} {"NAME",-22} {"TYPE",-10} {"DIST",4}  {"DIR",3}  {"DX",4} {"DY",4}  STATUS");
                foreach (var e in entities)
                    sb.AppendLine($"  {e.Glyph}  0x{e.Serial:X8}  {e.Name,-22} {e.Type,-10} {e.Distance,3}t  {e.Dir,3}  {e.Dx,+4} {e.Dy,+4}  {e.Status}");
            }
            else
            {
                sb.AppendLine("  No other entities in range.");
            }

            // ── Journal ──────────────────────────────────────────────────────────
            Divider(sb);
            var journal = snap.Journal;
            if (journal.Count == 0)
            {
                sb.AppendLine("  Journal empty.");
            }
            else
            {
                var recent = journal.Count > 20
                    ? (IList<JournalEntryDto>)journal.Skip(journal.Count - 20).ToList()
                    : (IList<JournalEntryDto>)journal;

                foreach (var entry in recent)
                {
                    var who  = string.IsNullOrEmpty(entry.Name) ? "" : $"[{entry.Name}] ";
                    var text = $"{who}{entry.Text}";
                    if (text.Length > 68) text = text[..65] + "…";
                    sb.AppendLine($"  {entry.Timestamp:HH:mm:ss}  {text}");
                }
            }

            Divider(sb);
            return Content(sb.ToString(), "text/plain");
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static void Divider(StringBuilder sb) =>
            sb.AppendLine(new string('─', 76));

        private sealed record EntityRow(
            char Glyph, uint Serial, string Name, string Type,
            int Distance, string Dir, int Dx, int Dy, string Status);

        private static List<EntityRow> BuildEntityRows(WorldSnapshot snap, PlayerDto player)
        {
            var rows = new List<EntityRow>();

            foreach (var mob in snap.Mobiles.OrderBy(m => m.Distance))
            {
                var (glyph, label) = TileSymbols.ForMobile(mob);
                int dx = mob.X - player.X;
                int dy = mob.Y - player.Y;

                var flags = new List<string>();
                if (mob.IsParalyzed) flags.Add("para");
                if (mob.IsPoisoned)  flags.Add("pois");
                if (mob.InWarMode)   flags.Add("war");

                var hp     = mob.HitsMax > 0 ? $"HP {mob.HitsPercentage}%" : "HP ?";
                var status = $"{mob.Notoriety}  {hp}" + (flags.Count > 0 ? "  " + string.Join(" ", flags) : "");

                rows.Add(new EntityRow(glyph, mob.Serial, mob.Name, label, mob.Distance, Bearing(dx, dy), dx, dy, status));
            }

            foreach (var item in snap.GroundItems.OrderBy(i => i.Distance))
            {
                var (glyph, label) = TileSymbols.ForItem(item);
                int dx  = item.X - player.X;
                int dy  = item.Y - player.Y;
                var name = string.IsNullOrEmpty(item.Name) ? $"0x{item.Graphic:X4}" : item.Name;
                var qty  = item.Amount > 1 ? $" x{item.Amount}" : "";
                rows.Add(new EntityRow(glyph, item.Serial, name + qty, label, item.Distance, Bearing(dx, dy), dx, dy, ""));
            }

            return rows;
        }

        // Direction from player to entity (UO: +X=East, +Y=South)
        private static string Bearing(int dx, int dy)
        {
            if (dx == 0 && dy == 0) return "here";
            var deg = Math.Atan2(dy, dx) * 180.0 / Math.PI;
            var idx = (int)Math.Round(((deg + 360) % 360) / 45.0) % 8;
            return new[] { "E", "SE", "S", "SW", "W", "NW", "N", "NE" }[idx];
        }
    }
}
