using System.Collections.Generic;

namespace ClassicUO.RestApi.Models
{
    internal sealed class ShopItemDto
    {
        public uint Serial { get; set; }
        public ushort Graphic { get; set; }
        public ushort Hue { get; set; }
        public int Amount { get; set; }
        public uint Price { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    internal sealed class ShopStateDto
    {
        public bool IsOpen { get; set; }
        public bool IsBuyGump { get; set; }
        public uint VendorSerial { get; set; }
        public IReadOnlyList<ShopItemDto> Items { get; set; } = [];
    }

    public class ShopBuyRequest
    {
        public List<ShopBuyItem> Items { get; set; } = [];
    }

    public class ShopBuyItem
    {
        public uint Serial { get; set; }
        public ushort Quantity { get; set; } = 1;
    }
}
