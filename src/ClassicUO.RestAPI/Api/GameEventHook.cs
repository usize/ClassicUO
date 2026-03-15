using System;
using System.Collections.Generic;
using System.Net.Sockets;
using ClassicUO.Configuration;
using ClassicUO.Game;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Gumps;
using ClassicUO.RestApi;
using ClassicUO.RestApi.Models;
using ClassicUO.Network;

namespace ClassicUO.RestApi
{
    internal sealed class GameEventHook : IDisposable
    {
        private readonly World _world;
        private readonly EventBus _eventBus;
        private readonly HashSet<uint> _mobiles = new();
        private readonly HashSet<uint> _items = new();
        private readonly Dictionary<uint, byte> _mobileHealth = new();

        private bool _targeting;
        private bool _inGame;
        private bool _shopOpen;
        private readonly HashSet<uint> _openGumps = new();
        private ushort _playerX;
        private ushort _playerY;
        private sbyte _playerZ;
        private int _mapIndex;
        private ushort _hits;
        private ushort _hitsMax;
        private ushort _mana;
        private ushort _manaMax;
        private ushort _stamina;
        private ushort _staminaMax;
        private bool _disposed;

        public GameEventHook(World world, EventBus eventBus)
        {
            _world = world;
            _eventBus = eventBus;
            _inGame = world.InGame;

            NetClient.Socket.Connected += OnConnected;
            NetClient.Socket.Disconnected += OnDisconnected;
            world.MessageManager.MessageReceived += OnMessageReceived;
            world.Journal.EntryAdded += OnJournalEntry;
        }

        public void Poll()
        {
            if (_disposed)
            {
                return;
            }

            PublishPlayerState();
            PublishTargetingState();
            PublishShopState();
            PublishGumpState();
            PublishMobiles();
            PublishItems();
        }

        private void PublishPlayerState()
        {
            var player = _world.Player;
            var inGame = _world.InGame;

            if (inGame != _inGame)
            {
                _inGame = inGame;
                _eventBus.Publish(SseEvent.Create("in_game", new
                {
                    inGame,
                    player = player != null ? new PlayerDto(player, _world.MapIndex, _world.ServerName) : null
                }));
            }

            if (player == null)
            {
                return;
            }

            if (player.X != _playerX || player.Y != _playerY || player.Z != _playerZ || _world.MapIndex != _mapIndex)
            {
                _playerX = player.X;
                _playerY = player.Y;
                _playerZ = player.Z;
                _mapIndex = _world.MapIndex;
                _eventBus.Publish("player_move", new
                {
                    x = _playerX,
                    y = _playerY,
                    z = _playerZ,
                    map = _mapIndex
                });
            }

            if (
                player.Hits != _hits
                || player.HitsMax != _hitsMax
                || player.Mana != _mana
                || player.ManaMax != _manaMax
                || player.Stamina != _stamina
                || player.StaminaMax != _staminaMax
            )
            {
                _hits = player.Hits;
                _hitsMax = player.HitsMax;
                _mana = player.Mana;
                _manaMax = player.ManaMax;
                _stamina = player.Stamina;
                _staminaMax = player.StaminaMax;

                _eventBus.Publish("player_stats", new
                {
                    hits = _hits,
                    hitsMax = _hitsMax,
                    mana = _mana,
                    manaMax = _manaMax,
                    stamina = _stamina,
                    staminaMax = _staminaMax
                });
            }
        }

        private void PublishTargetingState()
        {
            if (_world.TargetManager == null)
            {
                return;
            }

            var targeting = _world.TargetManager.IsTargeting;

            if (targeting != _targeting)
            {
                _targeting = targeting;
                _eventBus.Publish(targeting ? "target_requested" : "target_cancelled", new { targeting });
            }
        }

        private void PublishShopState()
        {
            var gump = UIManager.GetGump<ShopGump>();
            var isOpen = gump != null;

            if (isOpen != _shopOpen)
            {
                _shopOpen = isOpen;
                if (isOpen)
                {
                    _eventBus.Publish("shop_opened", new
                    {
                        vendorSerial = gump.LocalSerial,
                        isBuyGump = gump.IsBuyGump,
                    });
                }
                else
                {
                    _eventBus.Publish("shop_closed", new { });
                }
            }
        }

        private void PublishGumpState()
        {
            // Track server-sent gumps opening/closing
            var currentGumps = new HashSet<uint>();
            foreach (var gump in UIManager.Gumps)
            {
                if (!gump.IsFromServer) continue;
                // Skip ShopGumps — handled separately
                if (gump is ShopGump) continue;

                currentGumps.Add(gump.LocalSerial);

                if (_openGumps.Add(gump.LocalSerial))
                {
                    // New server gump opened — extract text for immediate feedback
                    var textLines = new List<string>();
                    CollectText(gump, textLines);

                    _eventBus.Publish("gump_opened", new
                    {
                        localSerial = gump.LocalSerial,
                        serverSerial = gump.ServerSerial,
                        textLines,
                    });
                }
            }

            // Detect closed gumps
            var closed = new List<uint>();
            foreach (var serial in _openGumps)
            {
                if (!currentGumps.Contains(serial))
                    closed.Add(serial);
            }
            foreach (var serial in closed)
            {
                _openGumps.Remove(serial);
                _eventBus.Publish("gump_closed", new { localSerial = serial });
            }
        }

        private static void CollectText(Game.UI.Controls.Control parent, List<string> lines)
        {
            foreach (var child in parent.Children)
            {
                if (child is Game.UI.Controls.Label lbl && !string.IsNullOrWhiteSpace(lbl.Text))
                    lines.Add(lbl.Text);
                else if (child is Game.UI.Controls.HtmlControl html && !string.IsNullOrWhiteSpace(html.Text))
                    lines.Add(html.Text);

                if (child.Children.Count > 0)
                    CollectText(child, lines);
            }
        }

        private void PublishMobiles()
        {
            var current = _world.Mobiles;

            if (current == null || current.Count == 0)
            {
                if (_mobiles.Count > 0)
                {
                    foreach (var serial in _mobiles)
                    {
                        _eventBus.Publish("mobile_removed", new { serial });
                    }

                    _mobiles.Clear();
                    _mobileHealth.Clear();
                }

                return;
            }

            var playerSerial = _world.Player?.Serial ?? 0;

            foreach (var mobile in current.Values)
            {
                if (mobile == null || mobile.Serial == playerSerial)
                {
                    continue;
                }

                if (_mobiles.Add(mobile.Serial))
                {
                    _eventBus.Publish(SseEvent.Create("mobile_added", new MobileDto(mobile, _world.MapIndex)));
                }

                byte hp = mobile.HitsPercentage;
                if (!_mobileHealth.TryGetValue(mobile.Serial, out var previous) || previous != hp)
                {
                    _mobileHealth[mobile.Serial] = hp;
                    _eventBus.Publish("mobile_hp", new { serial = mobile.Serial, hitsPercentage = hp });
                }
            }

            var removed = new List<uint>();

            foreach (var serial in _mobiles)
            {
                if (!current.ContainsKey(serial))
                {
                    removed.Add(serial);
                }
            }

            foreach (var serial in removed)
            {
                _mobiles.Remove(serial);
                _mobileHealth.Remove(serial);
                _eventBus.Publish("mobile_removed", new { serial });
            }
        }

        private void PublishItems()
        {
            var items = _world.Items;

            if (items == null)
            {
                return;
            }

            foreach (var item in items.Values)
            {
                if (item == null || !item.OnGround)
                {
                    continue;
                }

                if (_items.Add(item.Serial))
                {
                    _eventBus.Publish(SseEvent.Create("item_added", ItemDto.FromItem(item, includeContents: true)));
                }
            }

            var removed = new List<uint>();

            foreach (var serial in _items)
            {
                if (!items.TryGetValue(serial, out var item) || item == null || !item.OnGround)
                {
                    removed.Add(serial);
                }
            }

            foreach (var serial in removed)
            {
                _items.Remove(serial);
                _eventBus.Publish("item_removed", new { serial });
            }
        }

        private void OnMessageReceived(object sender, MessageEventArgs e)
        {
            _eventBus.Publish("chat", new
            {
                text = e.Text,
                name = e.Name,
                hue = e.Hue,
                messageType = e.Type,
                serial = e.Parent?.Serial ?? 0u
            });
        }

        private void OnJournalEntry(object sender, JournalEntry e)
        {
            _eventBus.Publish(SseEvent.Create("journal_entry", new JournalEntryDto(e)));
        }

        private void OnConnected(object sender, EventArgs e)
        {
            _eventBus.Publish("connected", new
            {
                Settings.GlobalSettings.IP,
                Settings.GlobalSettings.Port
            });
        }

        private void OnDisconnected(object sender, SocketError error)
        {
            _eventBus.Publish("disconnected", new { error });
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            NetClient.Socket.Connected -= OnConnected;
            NetClient.Socket.Disconnected -= OnDisconnected;
            _world.MessageManager.MessageReceived -= OnMessageReceived;
            _world.Journal.EntryAdded -= OnJournalEntry;
            _mobiles.Clear();
            _items.Clear();
            _mobileHealth.Clear();
            _openGumps.Clear();
        }
    }
}
