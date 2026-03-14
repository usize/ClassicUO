using System.Collections.Generic;

namespace ClassicUO.RestApi.Models
{
    internal sealed class WorldStateDto
    {
        public WorldStateDto(WorldSnapshot snapshot)
        {
            Timestamp = snapshot.Timestamp;
            Connected = snapshot.Connected;
            InGame = snapshot.InGame;
            Server = snapshot.Server;
            Player = snapshot.Player;
            Mobiles = snapshot.Mobiles;
            Items = snapshot.GroundItems;
            Journal = snapshot.Journal;
        }

        public System.DateTimeOffset Timestamp { get; }
        public bool Connected { get; }
        public bool InGame { get; }
        public string Server { get; }
        public PlayerDto? Player { get; }
        public IReadOnlyList<MobileDto> Mobiles { get; }
        public IReadOnlyList<ItemDto> Items { get; }
        public IReadOnlyList<JournalEntryDto> Journal { get; }
    }
}
