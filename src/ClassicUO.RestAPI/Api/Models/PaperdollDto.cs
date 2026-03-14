using System.Collections.Generic;
using ClassicUO.Game;
using ClassicUO.Game.GameObjects;

namespace ClassicUO.RestApi.Models
{
    internal sealed class PaperdollEquipDto
    {
        public uint   Serial  { get; init; }
        public string Layer   { get; init; } = "";
        public ushort Graphic { get; init; }
        public ushort Hue     { get; init; }
        public string Name    { get; init; } = "";
    }

    internal sealed class PaperdollDto
    {
        public uint   Serial   { get; init; }
        public string Name     { get; init; } = "";
        public string Title    { get; init; } = "";
        public string Race     { get; init; } = "";
        public string Gender   { get; init; } = "";
        public ushort Body     { get; init; }   // body/animation graphic
        public ushort Hue      { get; init; }
        public bool   IsHuman  { get; init; }
        public IReadOnlyList<PaperdollEquipDto> Equipment { get; init; } = System.Array.Empty<PaperdollEquipDto>();

        public static PaperdollDto From(Mobile mob)
        {
            var equip = new List<PaperdollEquipDto>();

            for (LinkedObject node = mob.Items; node != null; node = node.Next)
            {
                if (node is not Item item || item.IsDestroyed)
                    continue;

                equip.Add(new PaperdollEquipDto
                {
                    Serial  = item.Serial,
                    Layer   = item.Layer.ToString(),
                    Graphic = item.Graphic,
                    Hue     = item.Hue,
                    Name    = item.Name ?? string.Empty,
                });
            }

            return new PaperdollDto
            {
                Serial    = mob.Serial,
                Name      = mob.Name      ?? string.Empty,
                Title     = mob.Title     ?? string.Empty,
                Race      = mob.Race.ToString(),
                Gender    = mob.IsFemale ? "Female" : "Male",
                Body      = mob.Graphic,
                Hue       = mob.Hue,
                IsHuman   = mob.IsHuman,
                Equipment = equip,
            };
        }
    }
}
