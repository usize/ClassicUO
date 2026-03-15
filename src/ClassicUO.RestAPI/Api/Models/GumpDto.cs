using System.Collections.Generic;

namespace ClassicUO.RestApi.Models
{
    internal sealed class GumpDto
    {
        public uint LocalSerial { get; set; }
        public uint ServerSerial { get; set; }
        public bool IsFromServer { get; set; }
        public string GumpType { get; set; } = string.Empty;
        public int X { get; set; }
        public int Y { get; set; }
        public IReadOnlyList<GumpButtonDto> Buttons { get; set; } = [];
        public IReadOnlyList<string> TextLines { get; set; } = [];
        public IReadOnlyList<GumpCheckboxDto> Checkboxes { get; set; } = [];
        public IReadOnlyList<GumpTextEntryDto> TextEntries { get; set; } = [];
    }

    internal sealed class GumpButtonDto
    {
        public int ButtonID { get; set; }
        public int Page { get; set; }
    }

    internal sealed class GumpCheckboxDto
    {
        public uint Serial { get; set; }
        public bool IsChecked { get; set; }
    }

    internal sealed class GumpTextEntryDto
    {
        public uint Serial { get; set; }
        public string Text { get; set; } = string.Empty;
    }

    public class GumpResponseRequest
    {
        public int ButtonID { get; set; }
        public List<uint> Switches { get; set; } = [];
        public List<GumpTextEntry> TextEntries { get; set; } = [];
    }

    public class GumpTextEntry
    {
        public ushort Serial { get; set; }
        public string Text { get; set; } = string.Empty;
    }
}
