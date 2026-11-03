using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ChildGame.Services;

namespace ChildGame.UI
{
    /// <summary>
    /// The main shop, the street vendor's stall and the cash packages (docs/3-shop-design.md), drawn as the village store
    /// and the vendor's bamboo tray with a drawing for every item (official generated art, <see cref="ShopArt"/>).
    /// </summary>
    public static class ShopScreens
    {
        /// <summary>Raised after anything changes the wallet or inventory (the hub refreshes its counters).</summary>
        public static event Action Changed;

        // ---- Main shop ----

        public static void Main(string focusItem = null)
        {
            GameObject root = null;
            var card = Ui.Modal("ShopScreen", new Vector2(1640, 980), () => Close(root), out root);
            Backdrop(card, ShopArt.Storefront(new Vector2(16.4f, 9.8f))); // the village store (owner 2026-10-08: upgrade store art)
            Sign(card, "shop.title", new Vector2(460, 100));
            var wallet = Wallet(card);

            var grid = Ui.Rect(card, "Grid").gameObject.AddComponent<GridLayoutGroup>();
            ((RectTransform)grid.transform).Stretch(new Vector2(0, 0.2f), new Vector2(1, 0.88f), 30);
            grid.cellSize = new Vector2(372, 300); // four across: three snacks joined the store (owner 2026-10-09)
            grid.spacing = new Vector2(20, 24);
            grid.childAlignment = TextAnchor.UpperCenter;
            var refreshers = new List<Action>();
            foreach (var item in Shop.Main)
                refreshers.Add(ItemCard(grid.transform, item, false, item.id == focusItem, () => { foreach (var r in refreshers) r(); wallet(); }));

            // Bottom row: watch an ad, buy đồng, close.
            var row = Ui.Row(card, "Bottom", 30);
            row.Place(new Vector2(0.5f, 0f), new Vector2(1560, 120), new Vector2(0, 30));
            var ad = Ui.Button(row.transform, "shop.ad", null, Ui.Sun, 36).Size(640, 110);
            var adLabel = ad.GetComponentInChildren<LocText>();
            void RefreshAd()
            {
                var left = Shop.AdsLeft(Game.Save, DailyLimit.Today);
                adLabel.SetFormat("shop.ad", Shop.Money(Shop.AdReward), left, Shop.AdsPerDay);
                ad.interactable = left > 0 && Game.Ads.IsReady("shop");
            }
            ad.onClick.AddListener(() =>
            {
                ad.interactable = false;
                Game.Ads.Show("shop", rewarded =>
                {
                    if (rewarded && Shop.RewardAd(Game.Save, DailyLimit.Today)) Saved();
                    else if (!rewarded) UiScreens.Notice("shop.ad.none"); // no ad, or closed early: no reward, try again later (CrazyGames rule)
                    RefreshAd();
                    wallet();
                    foreach (var r in refreshers) r();
                });
            });
            refreshers.Add(RefreshAd);
            RefreshAd();
            GameRunner.Instance.StartCoroutine(WhileOpen(ad.gameObject, RefreshAd)); // an ad may finish loading while the shop is open
            ad.gameObject.SetActive(Game.Ads.Supported); // no ad button where there are no ads
            var cash = Ui.Button(row.transform, "shop.cash", () => Cash(() => { wallet(); foreach (var r in refreshers) r(); }), Ui.Leaf, 38).Size(440, 110);
            cash.gameObject.SetActive(Game.Store.Available); // no purchases on the web
            Ui.Button(row.transform, "common.close", () => Close(root), Ui.InkSoft, 38).Size(320, 110);
        }

        // ---- Street vendor ----

        public static void Vendor()
        {
            GameObject root = null;
            var card = Ui.Modal("VendorScreen", new Vector2(1180, 820), () => Close(root), out root);
            Backdrop(card, ShopArt.BambooTray(new Vector2(11.8f, 8.2f))); // her woven bamboo tray
            Sign(card, "vendor.title", new Vector2(450, 100));
            var wallet = Wallet(card, 640f);
            var grid = Ui.Rect(card, "Grid").gameObject.AddComponent<GridLayoutGroup>();
            ((RectTransform)grid.transform).Stretch(new Vector2(0, 0.18f), new Vector2(1, 0.85f), 30);
            grid.cellSize = new Vector2(540, 420);
            grid.spacing = new Vector2(24, 24);
            grid.childAlignment = TextAnchor.UpperCenter;
            var refreshers = new List<Action>();
            foreach (var item in Shop.Vendor)
                refreshers.Add(ItemCard(grid.transform, item, true, false, () => { foreach (var r in refreshers) r(); wallet(); }));
            Ui.Button(card, "common.close", () => Close(root), Ui.InkSoft, 38).Place(new Vector2(0.5f, 0f), new Vector2(360, 100), new Vector2(0, 30));
        }

        // ---- Cash packages (Google Play; simulated until the store SDK is added) ----

        public static void Cash(Action afterPurchase)
        {
            GameObject root = null;
            var card = Ui.Modal("CashScreen", new Vector2(1100, 600), () => Screens.Close(root), out root, 30);
            Ui.Label(card, "shop.cash.title", 54, Ui.Ink).Place(new Vector2(0.5f, 1f), new Vector2(1000, 100), new Vector2(0, -24));
            var row = Ui.Row(card, "Packages", 30);
            row.Place(new Vector2(0.5f, 0.5f), new Vector2(1040, 220), new Vector2(0, 10));
            foreach (var amount in Shop.CashPackages)
            {
                var a = amount;
                var b = Ui.Button(row.transform, "shop.cash.pack", null, Ui.Leaf, 40).Size(320, 200);
                b.name = "btn.shop.cash." + a;
                var label = b.GetComponentInChildren<LocText>();
                label.SetFormat("shop.cash.pack", Shop.Money(a));
                var id = MonetizationIds.CashProduct(a);
                var price = Game.Store.Price(id); // the real price from Google Play, under the amount
                if (price != null) label.GetComponent<TMPro.TextMeshProUGUI>().text += "\n<size=70%>" + price + "</size>";
                b.onClick.AddListener(() =>
                {
                    b.interactable = false;
                    Game.Store.Purchase(id, ok =>
                    {
                        b.interactable = true;
                        if (!ok) { Sfx.Play(Snd.Denied); return; }
                        Sfx.Play(Snd.Coin);
                        Changed?.Invoke(); // the store has already granted and saved it
                        afterPurchase?.Invoke();
                    });
                });
            }
            Ui.Button(card, "common.close", () => Screens.Close(root), Ui.InkSoft, 38).Place(new Vector2(0.5f, 0f), new Vector2(320, 100), new Vector2(0, 30));
        }

        // ---- Parts ----

        /// <summary>Calls <paramref name="refresh"/> every half second while <paramref name="alive"/> exists.</summary>
        private static System.Collections.IEnumerator WhileOpen(GameObject alive, Action refresh)
        {
            while (alive != null)
            {
                yield return new WaitForSecondsRealtime(0.5f);
                if (alive != null) refresh();
            }
        }

        /// <summary>The đồng balance at the top right; returns its refresh.</summary>
        private static Action Wallet(RectTransform card, float width = 760f)
        {
            var panel = PaperUi.Style(Ui.Panel(card, "Wallet", Color.white), new Color32(252, 244, 222, 255)).Place(new Vector2(1f, 1f), new Vector2(width, 96), new Vector2(-36, -24));
            Ui.Icon(panel.transform, "coin", PaperUi.Coin(), Color.white, 64).Place(new Vector2(0f, 0.5f), new Vector2(64, 64), new Vector2(18, 0));
            var text = Ui.Text(panel.transform, "", 32, new Color32(200, 140, 0, 255), TextAlignmentOptions.Left);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.enableAutoSizing = true;
            text.fontSizeMin = 22;
            text.fontSizeMax = 32;
            text.Stretch(Vector2.zero, Vector2.one);
            text.margin = new Vector4(90, 0, 10, 0);
            void Refresh() => text.text = Loc.Format("shop.wallet", Shop.Money(Game.Save.currency), Game.Save.paper, Game.Save.sandalPairs);
            Refresh();
            return Refresh;
        }

        /// <summary>One item card: icon, name, what it does, price and buy button(s). Returns its refresh.</summary>
        private static Action ItemCard(Transform parent, ShopItem item, bool vendor, bool focus, Action anyChange)
        {
            var card = PaperUi.Style(Ui.Panel(parent, "item." + item.id, Color.white), focus ? new Color32(255, 230, 150, 255) : new Color32(252, 246, 228, 255));
            var t = card.transform;
            var w = parent.GetComponent<GridLayoutGroup>().cellSize.x;
            ItemIcon(t, item.id).Place(new Vector2(0f, 1f), new Vector2(104, 104), new Vector2(16, -16));
            var name = Ui.Label(t, item.NameKey, 34, Ui.Ink, TextAlignmentOptions.MidlineLeft);
            name.enableAutoSizing = true;
            name.fontSizeMin = 22;
            name.fontSizeMax = 34;
            name.Place(new Vector2(0f, 1f), new Vector2(w - 146, 104), new Vector2(130, -16));
            Ui.Label(t, item.DescriptionKey, 24, Ui.InkSoft, TextAlignmentOptions.TopLeft, false).Place(new Vector2(0f, 1f), new Vector2(w - 40, 76), new Vector2(20, -128));
            var buy = Ui.Button(t, "shop.buy", () => Purchase(item, false, anyChange), Ui.Leaf, vendor ? 22 : 30);
            buy.name = "btn.buy." + item.id;
            buy.Place(new Vector2(0f, 0f), new Vector2(vendor ? (w - 56) / 2 : w - 40, 80), new Vector2(20, 20));
            Button bySandals = null;
            if (vendor && item.sandalPrice > 0)
            {
                bySandals = Ui.Button(t, "shop.buy.sandals", () => Purchase(item, true, anyChange), Ui.Sky, 22);
                bySandals.name = "btn.buysandals." + item.id;
                bySandals.Place(new Vector2(1f, 0f), new Vector2((w - 56) / 2, 80), new Vector2(-20, 20));
            }
            var label = buy.GetComponentInChildren<LocText>();
            void Refresh()
            {
                var why = Shop.CannotBuy(Game.Save, item);
                if (why == "shop.owned") label.Key = "shop.owned";
                else label.SetFormat("shop.buy", Shop.Money(item.price));
                buy.interactable = why == null;
                if (bySandals != null)
                {
                    bySandals.GetComponentInChildren<LocText>().SetFormat("shop.buy.sandals", item.sandalPrice);
                    bySandals.interactable = Shop.CannotBuy(Game.Save, item, true) == null;
                }
            }
            Refresh();
            return Refresh;
        }

        private static void Purchase(ShopItem item, bool withSandals, Action anyChange)
        {
            if (!Shop.Buy(Game.Save, item, withSandals)) { Sfx.Play(Snd.Denied); return; }
            Sfx.Play(Snd.Coin, 0.8f);
            Saved();
            anyChange?.Invoke();
            // Snacks and food: a story every time (owner rule; the owner's lines, 2026-10-09).
            if (item.storyKeys.Length > 0) UiScreens.Dialogue(item.storyKeys, null);
        }

        private static void Saved()
        {
            Game.Commit();
            Changed?.Invoke();
        }

        private static void Close(GameObject root)
        {
            Screens.Close(root);
            Changed?.Invoke();
        }


        /// <summary>Paints the shop's art over the whole card.</summary>
        private static void Backdrop(RectTransform card, Sprite art)
        {
            var img = card.GetComponent<Image>();
            img.sprite = art;
            img.type = Image.Type.Simple;
            img.color = Color.white;
        }

        /// <summary>The shop's name on a paper sign at the top left.</summary>
        private static void Sign(RectTransform card, string key, Vector2 size)
        {
            var sign = PaperUi.Style(Ui.Panel(card, "Sign", Color.white), new Color32(252, 244, 222, 255)).Place(new Vector2(0f, 1f), size, new Vector2(36, -24));
            var title = Ui.Label(sign.transform, key, 52, PaperUi.InkColor);
            title.Stretch(Vector2.zero, Vector2.one, 8);
            title.enableAutoSizing = true;
            title.fontSizeMin = 32;
            title.fontSizeMax = 52;
            title.textWrappingMode = TextWrappingModes.NoWrap;
        }

        /// <summary>The item's drawing (official generated art: <see cref="ShopArt"/>).</summary>
        public static RectTransform ItemIcon(Transform parent, string id)
        {
            var img = Ui.Rect(parent, "Icon").gameObject.AddComponent<Image>();
            img.sprite = ShopArt.Item(id);
            img.preserveAspect = true;
            img.raycastTarget = false;
            return (RectTransform)img.transform;
        }
    }
}
