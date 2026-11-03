using System.Collections.Generic;

namespace ChildGame
{
    public enum ItemKind { Paper, Unlock, SandalPair, Snack, Food }

    /// <summary>A shop item. Prices in đồng are placeholders until the owner sets them.</summary>
    public class ShopItem
    {
        public string id;
        public ItemKind kind;
        public int price;
        /// <summary>Street-vendor food only: price in sandal pairs (0 = not accepted).</summary>
        public int sandalPrice;
        public string[] storyKeys = new string[0];

        public string NameKey => "item." + id;
        public string DescriptionKey => "item." + id + ".desc";
    }

    /// <summary>
    /// The shops and inventory rules (docs/3-shop-design.md, owner 2026-10-07):
    /// <list type="bullet">
    /// <item>New game: 0 đồng, 1 sheet of paper, 1 pair of sandals.</item>
    /// <item>Paper: 1 sheet per play of Caro and Drawing, sold one sheet at a time.</item>
    /// <item>Pencil + crayons unlock Drawing; the kite unlocks Kite Flying (bought once).</item>
    /// <item>Sandals: at most one pair, bought as a pair; losing either sandal in Trộm xoài loses the pair; Trộm xoài is
    /// locked without sandals.</item>
    /// <item>Snacks (main shop) and food (street vendor) trigger a story on every purchase. Vendor food can also be
    /// paid for with sandals.</item>
    /// <item>One ad = 2,000 đồng (daily limit: placeholder 5). Cash packages 5,000 / 10,000 / 20,000 đồng.</item>
    /// </list>
    /// </summary>
    public static class Shop
    {
        public const int MaxSandalPairs = 1;
        public const int AdReward = 2000;
#if UNITY_WEBGL && !UNITY_EDITOR
        public const int AdsPerDay = 10; // web (owner 2026-10-10: no purchases there, so more rewarded ads)
#else
        public const int AdsPerDay = 5; // ⚠ placeholder: the owner said ads have a limit but not the number
#endif
        public static readonly int[] CashPackages = { 5000, 10000, 20000 };

        // ⚠ Prices are placeholders. Snacks and food: the owner's list (2026-10-09).
        public static readonly ShopItem[] Main =
        {
            new ShopItem { id = "paper", kind = ItemKind.Paper, price = 1000 },
            new ShopItem { id = "crayons", kind = ItemKind.Unlock, price = 15000 },
            new ShopItem { id = "kite", kind = ItemKind.Unlock, price = 10000 },
            new ShopItem { id = "sandals", kind = ItemKind.SandalPair, price = 3000 },
            new ShopItem { id = "keoc", kind = ItemKind.Snack, price = 2000, storyKeys = new[] { "story.keoc.1" } },
            new ShopItem { id = "mitom", kind = ItemKind.Snack, price = 2000, storyKeys = new[] { "story.mitom.1" } },
            new ShopItem { id = "bigbabol", kind = ItemKind.Snack, price = 2000, storyKeys = new[] { "story.bigbabol.1" } },
        };

        public static readonly ShopItem[] Vendor =
        {
            new ShopItem { id = "keokeo", kind = ItemKind.Food, price = 2000, sandalPrice = 1, storyKeys = new[] { "story.keokeo.1" } },
            new ShopItem { id = "carem", kind = ItemKind.Food, price = 2000, sandalPrice = 1, storyKeys = new[] { "story.carem.1" } },
        };

        public static ShopItem Find(string id)
        {
            foreach (var i in Main) if (i.id == id) return i;
            foreach (var i in Vendor) if (i.id == id) return i;
            return null;
        }

        public static bool Owns(SaveData s, string id) => s.owned.Contains(id);

        /// <summary>Why the player can't buy it right now (a text key), or null if they can.</summary>
        public static string CannotBuy(SaveData s, ShopItem item, bool withSandals = false)
        {
            if (item.kind == ItemKind.Unlock && Owns(s, item.id)) return "shop.owned";
            if (item.kind == ItemKind.SandalPair && s.sandalPairs >= MaxSandalPairs) return "shop.owned";
            if (withSandals) return item.sandalPrice > 0 && s.sandalPairs >= item.sandalPrice ? null : "shop.nosandals";
            return s.currency >= item.price ? null : "shop.nomoney";
        }

        /// <summary>Buys an item. Returns false (and changes nothing) if it can't be bought.</summary>
        public static bool Buy(SaveData s, ShopItem item, bool withSandals = false)
        {
            if (CannotBuy(s, item, withSandals) != null) return false;
            if (withSandals) s.sandalPairs -= item.sandalPrice;
            else s.currency -= item.price;
            switch (item.kind)
            {
                case ItemKind.Paper: s.paper++; break;
                case ItemKind.Unlock: s.owned.Add(item.id); break;
                case ItemKind.SandalPair: s.sandalPairs++; break;
                default: s.snacksEaten++; break; // snacks and food are eaten at once; the story is the point
            }
            return true;
        }

        // ---- Mini game requirements ----

        /// <summary>The text key explaining why a mini game is locked (an item to buy), or null if it is open.</summary>
        public static string LockReason(SaveData s, MiniGameDefinition def)
        {
            if (!string.IsNullOrEmpty(def.unlockItem) && !Owns(s, def.unlockItem)) return "lock." + def.unlockItem;
            if (def.needsSandals && s.sandalPairs <= 0) return "lock.sandals";
            return null;
        }

        /// <summary>The item to show in the shop for a locked mini game.</summary>
        public static string LockItem(MiniGameDefinition def) => !string.IsNullOrEmpty(def.unlockItem) ? def.unlockItem : def.needsSandals ? "sandals" : null;

        public static bool HasPaperFor(SaveData s, MiniGameDefinition def) => s.paper >= def.paperPerPlay;

        /// <summary>Takes the paper for one play. False if there is not enough.</summary>
        public static bool UsePaper(SaveData s, MiniGameDefinition def)
        {
            if (!HasPaperFor(s, def)) return false;
            s.paper -= def.paperPerPlay;
            return true;
        }

        /// <summary>Trộm xoài: losing one sandal loses the pair (owner rule).</summary>
        public static void LoseSandalPair(SaveData s)
        {
            if (s.sandalPairs > 0) s.sandalPairs--;
        }

        // ---- Ads and cash ----

        public static int AdsLeft(SaveData s, string today) => DailyLimit.Left(s.adsToday, AdsPerDay, today);

        public static bool RewardAd(SaveData s, string today)
        {
            if (!DailyLimit.Consume(s.adsToday, AdsPerDay, today)) return false;
            s.currency += AdReward;
            return true;
        }

        public static void AddCash(SaveData s, int amount) => s.currency += amount;

        /// <summary>Money as players read it: 2.000 in Vietnamese, 2,000 in English.</summary>
        public static string Money(int amount) =>
            amount.ToString("N0", System.Globalization.CultureInfo.InvariantCulture).Replace(",", Loc.Current == Language.Vietnamese ? "." : ",");
    }
}
