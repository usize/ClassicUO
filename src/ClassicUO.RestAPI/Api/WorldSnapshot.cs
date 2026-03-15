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
        public readonly char[,] Cells = new char[Width, Height];
        public ushort PlayerX;
        public ushort PlayerY;
        public sbyte  PlayerZ;

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
                    g.Cells[gx, gy] = ClassifyTile(world, wx, wy, g.PlayerZ);
                }
            }

            // Overlay dynamic ground items (furniture, chests, etc.) that block movement
            // but are invisible to the static tile pass above.
            if (world.Items != null)
            {
                foreach (var item in world.Items.Values)
                {
                    if (item == null || !item.OnGround) continue;
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

        private static char ClassifyTile(World world, int wx, int wy, sbyte playerZ)
        {
            var head = world.Map?.GetTile(wx, wy, load: false);
            if (head == null)
                return ' '; // chunk not loaded — leave blank

            bool hasDoor = false, hasWall = false, hasWater = false;

            for (var obj = head; obj != null; obj = obj.TNext)
            {
                if (obj is Static s)
                {
                    var d = s.ItemData;
                    // Ignore overhead roofs that are well above the player
                    if (d.IsRoof && Math.Abs(s.Z - playerZ) > 20)
                        continue;
                    if (d.IsDoor)         hasDoor = true;
                    else if (d.IsWall || d.IsImpassable) hasWall = true;
                    if (d.IsWet)          hasWater = true;
                }
                else if (obj is Multi m)
                {
                    var d = m.ItemData;
                    if (d.IsDoor)         hasDoor = true;
                    else if (d.IsWall || d.IsImpassable) hasWall = true;
                }
            }

            if (hasDoor)  return '+';
            if (hasWall)  return '#';
            if (hasWater) return '~';
            return '.';
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

            foreach (var mobile in world.Mobiles.Values)
            {
                if (mobile == null || mobile.Serial == playerSerial)
                    continue;
                list.Add(new MobileDto(mobile, world.MapIndex));
            }

            return list;
        }

        private static IReadOnlyList<ItemDto> BuildGroundItems(World world)
        {
            if (world.Items == null || world.Items.Count == 0)
                return Array.Empty<ItemDto>();

            var list = new List<ItemDto>();

            foreach (var item in world.Items.Values)
            {
                if (item == null || !item.OnGround)
                    continue;
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
