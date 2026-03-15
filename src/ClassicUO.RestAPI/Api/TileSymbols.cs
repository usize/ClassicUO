using System;
using System.Collections.Generic;
using ClassicUO.Game.Data;
using ClassicUO.RestApi.Models;

namespace ClassicUO.RestApi
{
    /// <summary>
    /// Maps UO game objects to single-character ASCII glyphs for the world map.
    /// Priority: you > guard > NPC > player > monster > animal > dead > items (gold first).
    /// </summary>
    internal static class TileSymbols
    {
        // ── Glyph definitions ────────────────────────────────────────────────────

        public static readonly IReadOnlyDictionary<char, string> Descriptions = new Dictionary<char, string>
        {
            // Terrain (from TileGrid)
            ['#'] = "wall/impassable",
            ['+'] = "door",
            ['~'] = "water",
            ['.'] = "open ground",
            ['^'] = "stairs up",
            ['v'] = "stairs down",
            ['X'] = "stairs up+down",

            // Mobiles
            ['@'] = "you",
            ['G'] = "guard",
            ['N'] = "NPC / vendor",
            ['H'] = "human player",
            ['M'] = "hostile creature",
            ['A'] = "animal",
            ['%'] = "corpse",

            // Items
            ['$'] = "gold coins",
            ['p'] = "potion",
            ['r'] = "reagent",
            ['s'] = "scroll",
            ['b'] = "bandage",
            ['C'] = "container / chest",
            ['w'] = "weapon",
            ['a'] = "armor / clothing",
            ['f'] = "food",
            ['i'] = "item",
        };

        // ── Graphic ID lookup (exact & range) ────────────────────────────────────

        // Gold coins: 0x0EEA–0x0EF2  (copper, silver, gold in 1/few/many variants)
        private const ushort GoldMin = 0x0EEA;
        private const ushort GoldMax = 0x0EF2;

        // Potions: 0x0F06–0x0F30
        private const ushort PotionMin = 0x0F06;
        private const ushort PotionMax = 0x0F30;

        // Keg of potions
        private const ushort PotionKeg = 0x0FAE;

        // Bandages
        private const ushort BandageGraphic = 0x0E21;

        // Human corpse container
        private const ushort CorpseGraphic = 0x2006;

        // Creature corpses are in the animation frame range; they appear as items
        // with graphic >= 0x4000 in the world item list.
        private const ushort CorpseRangeMin = 0x4000;

        // ── Name keywords ────────────────────────────────────────────────────────

        private static readonly string[] ReagentNames =
        {
            "black pearl", "blood moss", "mandrake", "garlic", "ginseng",
            "nightshade", "spider's silk", "sulfurous ash", "fertile dirt",
            "bat wing", "grave dust", "daemon blood", "nox crystal", "pig iron",
            "dragon's blood", "dead wood", "wyrm heart", "bone",
        };

        private static readonly string[] ContainerNames =
        {
            "backpack", "pouch", "bag", "sack", "box", "chest", "crate",
            "barrel", "keg", "cabinet", "basket", "coffer", "strongbox",
        };

        private static readonly string[] WeaponNames =
        {
            "sword", "axe", "mace", "dagger", "spear", "halberd",
            "bow", "crossbow", "staff", "wand", "kryss", "katana",
            "scimitar", "club", "lance", "pike", "bardiche", "cutlass",
            "broadsword", "longsword", "waraxe", "maul",
        };

        private static readonly string[] ArmorNames =
        {
            "helm", "helmet", "gorget", "pauldron", "gauntlet", "leggings",
            "tunic", "armor", "shield", "plate", "chain", "ring mail",
            "leather", "robe", "kilt", "cloak", "skirt", "doublet",
            "surcoat", "jingasa", "do", "kasa", "suneate",
        };

        private static readonly string[] FoodNames =
        {
            "bread", "loaf", "meat", "fish", "apple", "pear", "peach",
            "grapes", "cheese", "ham", "poultry", "sausage", "cabbage",
            "carrot", "turnip", "pumpkin", "lettuce", "ribs", "bacon",
        };

        private static readonly string[] ScrollNames =
        {
            "scroll of", "spell scroll", "scroll",
        };

        // ── Public API ───────────────────────────────────────────────────────────

        /// <summary>Glyph + label for a ground item, ordered by gameplay importance.</summary>
        public static (char glyph, string label) ForItem(ItemDto item)
        {
            ushort g    = item.Graphic;
            var name    = (item.Name ?? string.Empty).ToLowerInvariant();

            // Door (tiledata flag)
            if (item.IsDoor) return ('+', "door");

            // Graphic-ID–based (most reliable)
            if (g >= GoldMin     && g <= GoldMax)    return ('$', "gold");
            if (g >= PotionMin   && g <= PotionMax)  return ('p', "potion");
            if (g == PotionKeg)                      return ('p', "potion keg");
            if (g == BandageGraphic)                 return ('b', "bandage");
            if (g == CorpseGraphic || g >= CorpseRangeMin) return ('%', "corpse");

            // Name-based (covers items whose graphics vary across shards/patches)
            if (NameMatches(name, ReagentNames))   return ('r', "reagent");
            if (NameMatches(name, ScrollNames))    return ('s', "scroll");
            if (NameMatches(name, ContainerNames)) return ('C', "container");
            if (NameMatches(name, WeaponNames))    return ('w', "weapon");
            if (NameMatches(name, ArmorNames))     return ('a', "armor");
            if (NameMatches(name, FoodNames))      return ('f', "food");

            return ('i', "item");
        }

        /// <summary>Glyph + label for a mobile, ordered by gameplay importance.</summary>
        public static (char glyph, string label) ForMobile(MobileDto mob)
        {
            if (mob.IsDead)
                return ('%', "corpse");

            var nameLower = (mob.Name ?? string.Empty).ToLowerInvariant();

            if (mob.IsHuman)
            {
                // Guards: invulnerable humans whose name contains "guard"
                if (mob.Notoriety == NotorietyFlag.Invulnerable && nameLower.Contains("guard"))
                    return ('G', "guard");

                // Other invulnerable humans = NPC (vendors, quest givers, etc.)
                if (mob.Notoriety == NotorietyFlag.Invulnerable)
                    return ('N', "NPC");

                return ('H', "human player");
            }

            // Non-human
            // Innocent/Ally notoriety on a non-human = tamed or neutral animal
            if (mob.Notoriety == NotorietyFlag.Innocent || mob.Notoriety == NotorietyFlag.Ally)
                return ('A', "animal");

            return ('M', "creature");
        }

        /// <summary>Priority for resolving glyph conflicts when multiple objects share a tile.</summary>
        public static int Priority(char glyph) => glyph switch
        {
            '@' => 100,
            'G' => 90,
            'N' => 80,
            'H' => 75,
            'M' => 70,
            'A' => 65,
            '%' => 60,
            '$' => 50,
            'p' => 45,
            'r' => 40,
            's' => 38,
            'b' => 37,
            'C' => 35,
            'w' => 30,
            'a' => 28,
            'f' => 25,
            'i' => 10,
            _   => 0,
        };

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static bool NameMatches(string name, string[] keywords)
        {
            foreach (var k in keywords)
                if (name.Contains(k, StringComparison.Ordinal))
                    return true;
            return false;
        }
    }
}
