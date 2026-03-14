using System.Collections.Generic;
using ClassicUO.Game;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;

namespace ClassicUO.RestApi.Models
{
    internal sealed class PlayerDto
    {
        public PlayerDto(PlayerMobile player, int mapIndex, string serverName)
        {
            Serial = player.Serial;
            Name = player.Name ?? string.Empty;
            X = player.X;
            Y = player.Y;
            Z = player.Z;
            Direction = (byte)player.Direction;
            MapIndex = mapIndex;
            InWarMode = player.InWarMode;
            IsDead = player.IsDead;
            Server = serverName;
            Stats = new PlayerStatsDto(player);
            Equipment = BuildEquipment(player);
            Backpack = BuildBackpack(player);
            Skills = BuildSkills(player);
        }

        public uint Serial { get; }
        public string Name { get; }
        public ushort X { get; }
        public ushort Y { get; }
        public sbyte Z { get; }
        public byte Direction { get; }
        public int MapIndex { get; }
        public bool InWarMode { get; }
        public bool IsDead { get; }
        public string Server { get; }
        public PlayerStatsDto Stats { get; }
        public IReadOnlyList<ItemDto> Equipment { get; }
        public ItemDto? Backpack { get; }
        public IReadOnlyList<SkillDto> Skills { get; }

        private static IReadOnlyList<ItemDto> BuildEquipment(PlayerMobile player)
        {
            if (player.IsEmpty)
            {
                return ItemDto.EmptyList;
            }

            var items = new List<ItemDto>();

            for (LinkedObject node = player.Items; node != null; node = node.Next)
            {
                if (node is Item item)
                {
                    items.Add(ItemDto.FromItem(item));
                }
            }

            return items;
        }

        private static ItemDto? BuildBackpack(PlayerMobile player)
        {
            var backpack = player.FindItemByLayer(Layer.Backpack);
            return backpack != null ? ItemDto.FromItem(backpack, includeContents: true) : null;
        }

        private static IReadOnlyList<SkillDto> BuildSkills(PlayerMobile player)
        {
            if (player.Skills == null || player.Skills.Length == 0)
            {
                return System.Array.Empty<SkillDto>();
            }

            var skills = new List<SkillDto>(player.Skills.Length);

            foreach (var skill in player.Skills)
            {
                if (skill == null)
                {
                    continue;
                }

                skills.Add(new SkillDto(skill));
            }

            return skills;
        }
    }
}
