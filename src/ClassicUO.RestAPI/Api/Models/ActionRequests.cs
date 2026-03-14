namespace ClassicUO.RestApi.Models
{
    public class MoveRequest
    {
        /// <summary>Named direction: "north"/"n", "south"/"s", "east"/"e", "west"/"w", "ne", "se", "sw", "nw"</summary>
        public string? Direction { get; set; }
        /// <summary>Numeric direction: 0=North, 1=NE, 2=East, 3=SE, 4=South, 5=SW, 6=West, 7=NW</summary>
        public int? DirectionIndex { get; set; }
        public bool Run { get; set; }
    }

    public class SayRequest
    {
        public string Text { get; set; } = string.Empty;
        public string Type { get; set; } = "regular";
    }

    public class UseRequest
    {
        public uint Serial { get; set; }
    }

    public class AttackRequest
    {
        public uint Serial { get; set; }
    }

    public class SkillRequest
    {
        public int Index { get; set; }
    }

    public class SpellRequest
    {
        public int Index { get; set; }
    }

    public class WarmodeRequest
    {
        public bool Enabled { get; set; }
    }
}
