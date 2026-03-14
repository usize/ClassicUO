using ClassicUO.Game.GameObjects;

namespace ClassicUO.RestApi.Models
{
    internal sealed class PlayerStatsDto
    {
        public PlayerStatsDto(PlayerMobile player)
        {
            Strength = player.Strength;
            Dexterity = player.Dexterity;
            Intelligence = player.Intelligence;
            Hits = player.Hits;
            HitsMax = player.HitsMax;
            Mana = player.Mana;
            ManaMax = player.ManaMax;
            Stamina = player.Stamina;
            StaminaMax = player.StaminaMax;
            Gold = player.Gold;
            Weight = player.Weight;
            WeightMax = player.WeightMax;
            Followers = player.Followers;
            FollowersMax = player.FollowersMax;
            Luck = player.Luck;
            TithingPoints = player.TithingPoints;
            PhysicalResistance = player.PhysicalResistance;
            FireResistance = player.FireResistance;
            ColdResistance = player.ColdResistance;
            PoisonResistance = player.PoisonResistance;
            EnergyResistance = player.EnergyResistance;
            StatsCap = player.StatsCap;
        }

        public ushort Strength { get; }
        public ushort Dexterity { get; }
        public ushort Intelligence { get; }
        public ushort Hits { get; }
        public ushort HitsMax { get; }
        public ushort Mana { get; }
        public ushort ManaMax { get; }
        public ushort Stamina { get; }
        public ushort StaminaMax { get; }
        public uint Gold { get; }
        public ushort Weight { get; }
        public ushort WeightMax { get; }
        public byte Followers { get; }
        public byte FollowersMax { get; }
        public ushort Luck { get; }
        public uint TithingPoints { get; }
        public short PhysicalResistance { get; }
        public short FireResistance { get; }
        public short ColdResistance { get; }
        public short PoisonResistance { get; }
        public short EnergyResistance { get; }
        public short StatsCap { get; }
    }
}
