using System;
using System.Collections.Generic;
using System.Threading;
using ClassicUO.Game;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.RestApi.Models;
using ClassicUO.Network;

namespace ClassicUO.RestApi
{
    internal readonly struct StairInfo
    {
        public readonly int X, Y;
        public readonly bool Up, Down;
        public StairInfo(int x, int y, bool up, bool down) { X = x; Y = y; Up = up; Down = down; }
    }

    // Tile grid sampled around the player on the game thread.
    // Grid is 41 wide x 21 tall (20-tile radius x, 10-tile radius y), N at top.
    // gridX=20, gridY=10 is the player's tile.
    // World coords: worldX = playerX + (gridX - 20), worldY = playerY + (gridY - 10)
    internal sealed class TileGrid
    {
        public const int RadiusX = 20;
        public const int RadiusY = 10;
        public const int Width   = RadiusX * 2 + 1; // 41
        public const int Height  = RadiusY * 2 + 1; // 21

        // Cell values: ' '=unloaded  '.'=open  '#'=wall  '+'=door  '~'=water
        //              '^'=stair up  'v'=stair down  'X'=stair both directions
        public readonly char[,] Cells = new char[Width, Height];
        public ushort PlayerX;
        public ushort PlayerY;
        public sbyte  PlayerZ;

        /// <summary>World-coordinate stair positions visible from the current floor.</summary>
        public readonly List<StairInfo> Stairs = new List<StairInfo>();

        public static readonly TileGrid Empty = new TileGrid();

        public static TileGrid Sample(World world)
        {
            var g = new TileGrid
            {
                PlayerX = world.Player.X,
                PlayerY = world.Player.Y,
                PlayerZ = world.Player.Z,
            };

            for (int gy = 0; gy < Height; gy++)
            {
                for (int gx = 0; gx < Width; gx++)
                {
                    int wx = g.PlayerX + (gx - RadiusX);
                    int wy = g.PlayerY + (gy - RadiusY);
                    var tc = ClassifyTile(world, wx, wy, g.PlayerZ);
                    g.Cells[gx, gy] = tc.Glyph;
                    if (tc.HasStairUp || tc.HasStairDown)
                        g.Stairs.Add(new StairInfo(wx, wy, tc.HasStairUp, tc.HasStairDown));
                }
            }

            // Overlay dynamic ground items (furniture, chests, etc.) that block movement
            // but are invisible to the static tile pass above.
            if (world.Items != null)
            {
                foreach (var item in world.Items.Values)
                {
                    if (item == null || !item.OnGround) continue;
                    int dz = item.Z - g.PlayerZ;
                    if (dz < -5 || dz > 19) continue; // same floor filter
                    int gx = item.X - g.PlayerX + RadiusX;
                    int gy = item.Y - g.PlayerY + RadiusY;
                    if (gx < 0 || gx >= Width || gy < 0 || gy >= Height) continue;

                    var data = item.ItemData;
                    if (data.IsDoor)
                        g.Cells[gx, gy] = '+';
                    else if ((data.IsImpassable || data.IsWall) && g.Cells[gx, gy] != '+')
                        g.Cells[gx, gy] = '#';
                }
            }

            return g;
        }

        // Result of ClassifyTile — tile glyph plus any stair info at this position.
        internal readonly struct TileClassification
        {
            public readonly char Glyph;
            public readonly bool HasStairUp;    // bridge tile leading to a higher floor
            public readonly bool HasStairDown;  // bridge tile leading to a lower floor
            public TileClassification(char glyph, bool up, bool down)
                { Glyph = glyph; HasStairUp = up; HasStairDown = down; }
        }

        private static TileClassification ClassifyTile(World world, int wx, int wy, sbyte playerZ)
        {
            var head = world.Map?.GetTile(wx, wy, load: false);
            if (head == null)
                return new TileClassification(' ', false, false);

            bool hasDoor = false, hasWall = false, hasWater = false;
            bool hasStairUp = false, hasStairDown = false;

            // UO floors are ~20 Z units apart. Only classify walls/doors on the
            // player's current floor. Stair/bridge tiles are exempt from the floor
            // filter because they span the gap between floors and must be visible
            // from both sides so the AI knows where to walk.
            const int ZBelow =  5;
            const int ZAbove = 19;
            const int StairRange = 25; // wide enough to see stairs one floor away

            for (var obj = head; obj != null; obj = obj.TNext)
            {
                if (obj is Static s)
                {
                    int dz = s.Z - playerZ;
                    var d = s.ItemData;

                    if (d.IsBridge && Math.Abs(dz) <= StairRange)
                    {
                        // Bridge (stair/ramp/ladder): classify direction from player's floor
                        if (dz > 0) hasStairUp   = true;
                        else        hasStairDown  = true;
                        continue; // stairs are walkable — don't mark as wall
                    }

                    if (dz < -ZBelow || dz > ZAbove) continue;
                    if (d.IsDoor)                        hasDoor = true;
                    else if (d.IsWall || d.IsImpassable) hasWall = true;
                    if (d.IsWet)                         hasWater = true;
                }
                else if (obj is Multi m)
                {
                    int dz = m.Z - playerZ;
                    var d = m.ItemData;

                    if (d.IsBridge && Math.Abs(dz) <= StairRange)
                    {
                        if (dz > 0) hasStairUp  = true;
                        else        hasStairDown = true;
                        continue;
                    }

                    if (dz < -ZBelow || dz > ZAbove) continue;
                    if (d.IsDoor)                        hasDoor = true;
                    else if (d.IsWall || d.IsImpassable) hasWall = true;
                }
            }

            char glyph;
            if      (hasDoor)              glyph = '+';
            else if (hasWall)              glyph = '#';
            else if (hasWater)             glyph = '~';
            else if (hasStairUp && hasStairDown) glyph = 'X'; // stairs in both directions
            else if (hasStairUp)           glyph = '^';
            else if (hasStairDown)         glyph = 'v';
            else                           glyph = '.';

            return new TileClassification(glyph, hasStairUp, hasStairDown);
        }
    }

    internal sealed record WorldSnapshot(
        DateTimeOffset Timestamp,
        bool Connected,
        bool InGame,
        string Server,
        PlayerDto? Player,
        IReadOnlyList<MobileDto> Mobiles,
        IReadOnlyList<ItemDto> GroundItems,
        IReadOnlyList<JournalEntryDto> Journal
    )
    {
        private const int MaxJournalEntries = 200;
        private const int TileGridUpdateInterval = 60; // ticks (~1 second at 60fps)

        private static WorldSnapshot _current = CreateEmpty();
        private static TileGrid _tileGrid = TileGrid.Empty;
        private static int _tickCount = 0;

        public static WorldSnapshot Current => Volatile.Read(ref _current);
        public static TileGrid CurrentTileGrid => Volatile.Read(ref _tileGrid);

        public static void Update(World world)
        {
            Volatile.Write(ref _current, Capture(world));

            // Resample tile grid less frequently — it's a read across 861 tiles
            if (world?.Player != null && (_tickCount++ % TileGridUpdateInterval == 0))
            {
                Volatile.Write(ref _tileGrid, TileGrid.Sample(world));
            }
        }

        public static WorldSnapshot Capture(World world)
        {
            if (world == null)
            {
                return CreateEmpty() with
                {
                    Timestamp = DateTimeOffset.UtcNow,
                    Connected = NetClient.Socket.IsConnected
                };
            }

            var timestamp = DateTimeOffset.UtcNow;
            var player = world.Player != null ? new PlayerDto(world.Player, world.MapIndex, world.ServerName) : null;
            var mobiles = BuildMobiles(world);
            var items = BuildGroundItems(world);
            var journal = BuildJournalEntries(world.Journal);

            return new WorldSnapshot(
                timestamp,
                NetClient.Socket.IsConnected,
                world.InGame,
                world.ServerName,
                player,
                mobiles,
                items,
                journal
            );
        }

        private static IReadOnlyList<MobileDto> BuildMobiles(World world)
        {
            if (world.Mobiles == null || world.Mobiles.Count == 0)
                return Array.Empty<MobileDto>();

            var list = new List<MobileDto>(world.Mobiles.Count);
            var playerSerial = world.Player?.Serial ?? 0;

            sbyte playerZ = world.Player?.Z ?? 0;
            foreach (var mobile in world.Mobiles.Values)
            {
                if (mobile == null || mobile.Serial == playerSerial)
                    continue;
                // Skip mobiles on other floors (more than one floor away)
                int dz = mobile.Z - playerZ;
                if (dz < -20 || dz > 20) continue;
                list.Add(new MobileDto(mobile, world.MapIndex));
            }

            return list;
        }

        private static IReadOnlyList<ItemDto> BuildGroundItems(World world)
        {
            if (world.Items == null || world.Items.Count == 0)
                return Array.Empty<ItemDto>();

            var list = new List<ItemDto>();

            sbyte playerZ = world.Player?.Z ?? 0;
            foreach (var item in world.Items.Values)
            {
                if (item == null || !item.OnGround)
                    continue;
                // Skip items on other floors
                int dz = item.Z - playerZ;
                if (dz < -20 || dz > 20) continue;
                list.Add(ItemDto.FromItem(item, includeContents: true));
            }

            return list;
        }

        private static IReadOnlyList<JournalEntryDto> BuildJournalEntries(JournalManager journal)
        {
            if (journal == null || JournalManager.Entries.Count == 0)
                return Array.Empty<JournalEntryDto>();

            var list = new List<JournalEntryDto>();
            var total = JournalManager.Entries.Count;
            var startIndex = Math.Max(0, total - MaxJournalEntries);

            for (int i = startIndex; i < total; i++)
            {
                var entry = JournalManager.Entries[i];
                if (entry != null)
                    list.Add(new JournalEntryDto(entry));
            }

            return list;
        }

        private static WorldSnapshot CreateEmpty()
        {
            return new WorldSnapshot(
                DateTimeOffset.MinValue,
                false,
                false,
                string.Empty,
                null,
                Array.Empty<MobileDto>(),
                Array.Empty<ItemDto>(),
                Array.Empty<JournalEntryDto>()
            );
        }
    }
}
