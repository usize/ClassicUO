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

    public class TargetRequest
    {
        /// <summary>Serial of a mobile or item to target. Mutually exclusive with coordinates.</summary>
        public uint? Serial { get; set; }
        /// <summary>Target ground X coordinate. Provide X+Y+Z for ground targeting.</summary>
        public ushort? X { get; set; }
        public ushort? Y { get; set; }
        public short? Z { get; set; }
        /// <summary>Set to true to cancel the current targeting cursor.</summary>
        public bool Cancel { get; set; }
    }

    public class GrabRequest
    {
        /// <summary>Serial of the item to pick up and move to backpack.</summary>
        public uint Serial { get; set; }
        /// <summary>Quantity to grab. Defaults to all.</summary>
        public ushort Amount { get; set; } = 0;
    }

    public class DropRequest
    {
        /// <summary>Serial of the currently held item to drop.</summary>
        public uint Serial { get; set; }
        /// <summary>Container serial to drop into. If omitted, drops on ground.</summary>
        public uint? Container { get; set; }
        /// <summary>X position within container or ground.</summary>
        public ushort X { get; set; } = 0xFFFF;
        /// <summary>Y position within container or ground.</summary>
        public ushort Y { get; set; } = 0xFFFF;
        /// <summary>Z position (ground only).</summary>
        public sbyte Z { get; set; } = 0;
    }

    public class PathfindRequest
    {
        /// <summary>Target X coordinate. Provide X+Y, or Serial, but not both.</summary>
        public int? X { get; set; }
        public int? Y { get; set; }
        /// <summary>Target Z coordinate. Defaults to player's current Z if omitted.</summary>
        public int? Z { get; set; }
        /// <summary>Serial of a mobile or ground item to walk toward (stops 1 tile away).</summary>
        public uint? Serial { get; set; }
    }

    public class TravelRequest
    {
        /// <summary>Goal X coordinate (required).</summary>
        public int? X { get; set; }
        /// <summary>Goal Y coordinate (required).</summary>
        public int? Y { get; set; }
        /// <summary>Goal Z coordinate. Defaults to the player's current Z if omitted.</summary>
        public int? Z { get; set; }
    }

    public class PickUpRequest
    {
        /// <summary>Serial of the item to pick up (puts in cursor).</summary>
        public uint Serial { get; set; }
        /// <summary>Amount to pick up. Defaults to all.</summary>
        public int Amount { get; set; } = -1;
    }

    public class EquipRequest
    {
        /// <summary>Container serial to equip to (defaults to player).</summary>
        public uint? Container { get; set; }
    }
}
