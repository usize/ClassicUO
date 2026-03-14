using ClassicUO.Game;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;

namespace ClassicUO.RestApi.Models
{
    internal sealed class MobileDto
    {
        public MobileDto(Mobile mobile, int mapIndex)
        {
            Serial = mobile.Serial;
            Name = mobile.Name ?? string.Empty;
            X = mobile.X;
            Y = mobile.Y;
            Z = mobile.Z;
            Direction = (byte)mobile.Direction;
            MapIndex = mapIndex;
            Distance = mobile.Distance;
            InWarMode = mobile.InWarMode;
            IsDead = mobile.IsDead;
            IsParalyzed = mobile.IsParalyzed;
            IsPoisoned = mobile.IsPoisoned;
            IsHuman = mobile.IsHuman;
            IsMounted = mobile.IsMounted;
            Notoriety = mobile.NotorietyFlag;
            Hits = mobile.Hits;
            HitsMax = mobile.HitsMax;
            HitsPercentage = mobile.HitsPercentage;
            Graphic = mobile.Graphic;
            Hue = mobile.Hue;
            Title = mobile.Title ?? string.Empty;
        }

        public uint Serial { get; }
        public string Name { get; }
        public ushort X { get; }
        public ushort Y { get; }
        public sbyte Z { get; }
        public byte Direction { get; }
        public int MapIndex { get; }
        public int Distance { get; }
        public bool InWarMode { get; }
        public bool IsDead { get; }
        public bool IsParalyzed { get; }
        public bool IsPoisoned { get; }
        public bool IsHuman { get; }
        public bool IsMounted { get; }
        public NotorietyFlag Notoriety { get; }
        public ushort Hits { get; }
        public ushort HitsMax { get; }
        public byte HitsPercentage { get; }
        public ushort Graphic { get; }
        public ushort Hue { get; }
        public string Title { get; }
    }
}
