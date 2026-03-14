using System;
using ClassicUO.Game.Data;
using ClassicUO.Game.Managers;

namespace ClassicUO.RestApi.Models
{
    internal sealed class JournalEntryDto
    {
        public JournalEntryDto(JournalEntry entry)
        {
            Timestamp = entry.Time;
            Text = entry.Text ?? string.Empty;
            Name = entry.Name ?? string.Empty;
            Hue = entry.Hue;
            MessageType = entry.MessageType;
            TextType = entry.TextType;
        }

        public DateTime Timestamp { get; }
        public string Text { get; }
        public string Name { get; }
        public ushort Hue { get; }
        public MessageType MessageType { get; }
        public TextType TextType { get; }
    }
}
