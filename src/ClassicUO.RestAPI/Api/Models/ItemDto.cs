using System;
using System.Collections.Generic;
using ClassicUO.Game;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;

namespace ClassicUO.RestApi.Models
{
    internal sealed class ItemDto
    {
        public static readonly IReadOnlyList<ItemDto> EmptyList = Array.Empty<ItemDto>();

        private ItemDto
        (
            uint serial,
            ushort graphic,
            ushort hue,
            ushort amount,
            ushort x,
            ushort y,
            sbyte z,
            uint containerSerial,
            bool onGround,
            bool isDoor,
            Layer layer,
            string name,
            int distance,
            uint rootContainer,
            IReadOnlyList<ItemDto> contents
        )
        {
            Serial = serial;
            Graphic = graphic;
            Hue = hue;
            Amount = amount;
            X = x;
            Y = y;
            Z = z;
            ContainerSerial = containerSerial;
            OnGround = onGround;
            IsDoor = isDoor;
            Layer = layer;
            Name = name;
            Distance = distance;
            RootContainer = rootContainer;
            Contents = contents;
        }

        public uint Serial { get; }
        public ushort Graphic { get; }
        public ushort Hue { get; }
        public ushort Amount { get; }
        public ushort X { get; }
        public ushort Y { get; }
        public sbyte Z { get; }
        public uint ContainerSerial { get; }
        public bool OnGround { get; }
        public bool IsDoor { get; }
        public Layer Layer { get; }
        public string Name { get; }
        public int Distance { get; }
        public uint RootContainer { get; }
        public IReadOnlyList<ItemDto> Contents { get; }

        public static ItemDto FromItem(Item item, bool includeContents = false)
        {
            var contents = includeContents ? BuildChildren(item) : EmptyList;
            return new ItemDto(
                item.Serial,
                item.Graphic,
                item.Hue,
                item.Amount,
                item.X,
                item.Y,
                item.Z,
                item.Container,
                item.OnGround,
                item.ItemData.IsDoor,
                item.Layer,
                item.Name ?? string.Empty,
                item.Distance,
                item.RootContainer,
                contents
            );
        }

        private static IReadOnlyList<ItemDto> BuildChildren(Item item)
        {
            if (item.IsEmpty)
            {
                return EmptyList;
            }

            var children = new List<ItemDto>();

            for (LinkedObject node = item.Items; node != null; node = node.Next)
            {
                if (node is Item child)
                {
                    children.Add(FromItem(child, includeContents: true));
                }
            }

            return children;
        }
    }
}
