using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace MarketMobile.Core;

public enum CurrencyType
{
    Gold,
    Rau
}

public enum GachaRarity
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary
}

public sealed class GachaRewardResult
{
    public string Name { get; set; }
    public string AmountText { get; set; }
    public string IconPath { get; set; }
    public Color IconTint { get; set; } = Colors.White;
    public GachaRarity Rarity { get; set; } = GachaRarity.Common;
    public string Note { get; set; } = "";
}

public enum TradeAssetType
{
    Item,
    Gold,
    Rau
}

public class MarketTradeListing
{
    public string Id { get; set; }
    public string SellerName { get; set; }
    public bool IsPlayerOwned { get; set; }
    public TradeAssetType GiveType { get; set; }
    public string GiveItemId { get; set; }
    public int GiveAmount { get; set; }
    public TradeAssetType ReceiveType { get; set; }
    public string ReceiveItemId { get; set; }
    public int ReceiveAmount { get; set; }
}

public class CharacterProfile
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Role { get; set; }
    public int Damage { get; set; }
    public int Health { get; set; }
    public int Defense { get; set; }
    public int Price { get; set; }
    public CurrencyType Currency { get; set; } = CurrencyType.Gold;
    public int AltPrice { get; set; } = 0;
    public CurrencyType AltCurrency { get; set; } = CurrencyType.Rau;
    public bool IsUnlocked { get; set; }
    public Color Tint { get; set; } = Colors.White;
}

/// <summary>
/// Autoload quản lý dữ liệu người chơi dùng chung giữa các màn hình và lưu xuống user://save_data.json.
/// </summary>
public partial class PlayerDataManager : Node
{
    public static PlayerDataManager Instance { get; private set; }

    [Signal]
    public delegate void CurrenciesChangedEventHandler(int coins, int rau);

    [Signal]
    public delegate void SelectedCharacterChangedEventHandler(int charIndex);

    [Signal]
    public delegate void InventoryChangedEventHandler();

    [Signal]
    public delegate void GachaStateChangedEventHandler(int tickets, int pityCount);

    private const string SaveFilePath = "user://save_data.json";
    private const int DefaultCoins = 123456;
    private const int DefaultRau = 123456;
    private const int DefaultGachaTickets = 5;

    public const int TicketPriceGold = 100;
    public const int TicketPriceRau = 10;
    public const int GachaPityLimit = 50;
    public const int GachaMilestoneMax = 30;
    public static readonly int[] GachaMilestoneTargets = { 1, 5, 10, 15, 30 };

    public int Coins { get; private set; } = DefaultCoins;
    public int Rau { get; private set; } = DefaultRau;
    public int GachaTickets { get; private set; } = DefaultGachaTickets;
    public int GachaPityCount { get; private set; } = 0;
    public int GachaMilestoneProgress { get; private set; } = 0;
    public int SelectedCharacterIndex { get; private set; } = 0;
    public bool IsHatUnlocked { get; private set; } = false;
    public int EquippedSkinIndex { get; private set; } = 0;
    public string EquippedShopSkinId { get; private set; } = "outfit_default";
    public bool HasClaimedStarterShopGift { get; private set; } = false;
    public bool HasReceivedInventoryStarterGift { get; private set; } = false;
    public bool HasReceivedLuckBagGift { get; private set; } = false;

    /// <summary>
    /// Tab đích tạm thời khi chuyển sang shop_screen (ví dụ 3 = Đổi Vàng/Rau khi bấm dấu +).
    /// </summary>
    public int PendingShopTabIndex { get; set; } = -1;

    /// <summary>
    /// Nhân vật cần mở xem trước khi chuyển sang character_screen từ Cửa hàng.
    /// </summary>
    public int PendingPreviewCharacterIndex { get; set; } = -1;

    /// <summary>
    /// Khi true, lobby_screen sẽ tự mở popup Cài đặt ngay khi _Ready().
    /// </summary>
    public bool PendingOpenSettings { get; set; } = false;

    /// <summary>
    /// Khi true, lobby_screen sẽ tự mở tab Túi đồ (tab 4) ngay khi _Ready().
    /// </summary>
    public bool PendingOpenInventory { get; set; } = false;

    private CharacterProfile[] _characters;
    private readonly Dictionary<string, int> _itemQuantities = new();
    private readonly List<string> _recentItemIds = new();
    private readonly HashSet<string> _unlockedExtraSkins = new();
    private readonly HashSet<int> _claimedGachaMilestones = new();
    private readonly List<MarketTradeListing> _playerMarketListings = new();
    public CharacterProfile[] Characters => _characters;
    public IReadOnlyList<MarketTradeListing> PlayerMarketListings => _playerMarketListings;
    public IReadOnlyDictionary<string, int> ItemQuantities => _itemQuantities;
    public IReadOnlyList<string> RecentItemIds => _recentItemIds;

    public CharacterProfile ActiveCharacter =>
        _characters != null && SelectedCharacterIndex >= 0 && SelectedCharacterIndex < _characters.Length
            ? _characters[SelectedCharacterIndex]
            : null;

    private class SaveDataDto
    {
        public int Coins { get; set; } = DefaultCoins;
        public int Rau { get; set; } = DefaultRau;
        public int GachaTickets { get; set; } = DefaultGachaTickets;
        public int GachaPityCount { get; set; } = 0;
        public int GachaMilestoneProgress { get; set; } = 0;
        public List<int> ClaimedGachaMilestones { get; set; } = new();
        public int SelectedCharacterIndex { get; set; } = 0;
        public bool IsHatUnlocked { get; set; } = false;
        public int EquippedSkinIndex { get; set; } = 0;
        public string EquippedShopSkinId { get; set; } = "outfit_default";
        public bool HasClaimedStarterShopGift { get; set; } = false;
        public bool HasReceivedInventoryStarterGift { get; set; } = false;
        public bool HasReceivedLuckBagGift { get; set; } = false;
        public Dictionary<string, int> Items { get; set; } = new();
        public List<string> RecentItems { get; set; } = new();
        public List<string> UnlockedExtraSkins { get; set; } = new();
        public List<MarketTradeListing> PlayerMarketListings { get; set; } = new();
        public List<CharacterSaveDto> Characters { get; set; } = new();
    }

    private class CharacterSaveDto
    {
        public int Id { get; set; }
        public int Damage { get; set; }
        public int Health { get; set; }
        public int Defense { get; set; }
        public bool IsUnlocked { get; set; }
    }

    public override void _EnterTree()
    {
        Instance = this;
        InitializeDefaults();
        LoadFromDisk();
    }

    public override void _ExitTree()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void InitializeDefaults()
    {
        Coins = DefaultCoins;
        Rau = DefaultRau;
        GachaTickets = DefaultGachaTickets;
        GachaPityCount = 0;
        GachaMilestoneProgress = 0;
        SelectedCharacterIndex = 0;
        IsHatUnlocked = false;
        EquippedSkinIndex = 0;
        EquippedShopSkinId = "outfit_default";
        HasClaimedStarterShopGift = false;
        HasReceivedInventoryStarterGift = true;
        HasReceivedLuckBagGift = true;
        _itemQuantities.Clear();
        _itemQuantities["gift_tan_thu"] = 1;
        _itemQuantities["gift_loc_cho"] = 1;
        _recentItemIds.Clear();
        _recentItemIds.Add("gift_loc_cho");
        _recentItemIds.Add("gift_tan_thu");
        _unlockedExtraSkins.Clear();
        _claimedGachaMilestones.Clear();
        _playerMarketListings.Clear();
        _characters = CreateDefaultCharacters();
    }

    public static CharacterProfile[] CreateDefaultCharacters() => new[]
    {
        new CharacterProfile { Id = 0, Name = "Bà Bảy", Role = "Cận chiến", Damage = 15, Health = 250, Defense = 20, Price = 0, IsUnlocked = true, Tint = Colors.White },
        new CharacterProfile { Id = 1, Name = "Chú Tư Bán Cá", Role = "Đỡ đòn", Damage = 10, Health = 420, Defense = 40, Price = 2500, Currency = CurrencyType.Gold, AltPrice = 200, AltCurrency = CurrencyType.Rau, IsUnlocked = false, Tint = new Color(0.9f, 0.95f, 1.0f) },
        new CharacterProfile { Id = 2, Name = "Bé Ba Bán Hoa", Role = "Xạ thủ", Damage = 35, Health = 180, Defense = 15, Price = 5000, Currency = CurrencyType.Gold, IsUnlocked = false, Tint = new Color(1.0f, 0.9f, 0.95f) },
        new CharacterProfile { Id = 3, Name = "Cậu Năm Shipper", Role = "Sát thủ", Damage = 45, Health = 210, Defense = 10, Price = 500, Currency = CurrencyType.Rau, IsUnlocked = false, Tint = new Color(0.95f, 1.0f, 0.9f) },
    };

    public int GetBalance(CurrencyType currency) => currency == CurrencyType.Rau ? Rau : Coins;

    public CharacterProfile GetCharacter(int index) =>
        _characters != null && index >= 0 && index < _characters.Length ? _characters[index] : null;

    public void SetBalances(int coins, int rau, bool save = true)
    {
        Coins = Mathf.Max(0, coins);
        Rau = Mathf.Max(0, rau);
        EmitSignal(SignalName.CurrenciesChanged, Coins, Rau);
        if (save) SaveToDisk();
    }

    public void AddCurrency(CurrencyType currency, int amount)
    {
        if (amount <= 0) return;

        if (currency == CurrencyType.Rau) Rau += amount;
        else Coins += amount;

        EmitSignal(SignalName.CurrenciesChanged, Coins, Rau);
        SaveToDisk();
    }

    public bool TrySpendCurrency(CurrencyType currency, int amount)
    {
        if (amount < 0 || GetBalance(currency) < amount) return false;

        if (currency == CurrencyType.Rau) Rau -= amount;
        else Coins -= amount;

        EmitSignal(SignalName.CurrenciesChanged, Coins, Rau);
        SaveToDisk();
        return true;
    }

    public bool SetSelectedCharacter(int index)
    {
        var character = GetCharacter(index);
        if (character == null || !character.IsUnlocked) return false;

        if (SelectedCharacterIndex == index) return true;
        SelectedCharacterIndex = index;
        EmitSignal(SignalName.SelectedCharacterChanged, index);
        SaveToDisk();
        return true;
    }

    public bool TryUnlockCharacter(int index, CurrencyType? chosenCurrency = null)
    {
        var character = GetCharacter(index);
        if (character == null || character.IsUnlocked) return false;

        int cost;
        CurrencyType currency;
        if (chosenCurrency == null || chosenCurrency == character.Currency)
        {
            cost = character.Price;
            currency = character.Currency;
        }
        else if (character.AltPrice > 0 && chosenCurrency == character.AltCurrency)
        {
            cost = character.AltPrice;
            currency = character.AltCurrency;
        }
        else
        {
            return false;
        }

        if (!TrySpendCurrency(currency, cost)) return false;

        character.IsUnlocked = true;
        SelectedCharacterIndex = index;
        EmitSignal(SignalName.SelectedCharacterChanged, index);
        EmitSignal(SignalName.InventoryChanged);
        SaveToDisk();
        return true;
    }

    public bool TryUpgradeStats(int index, int addDamage, int addHealth, int addDefense, int totalCost)
    {
        var character = GetCharacter(index);
        if (character == null || !character.IsUnlocked) return false;
        if (!TrySpendCurrency(CurrencyType.Gold, totalCost)) return false;

        character.Damage = Mathf.Min(100, character.Damage + Mathf.Max(0, addDamage));
        character.Health += Mathf.Max(0, addHealth);
        character.Defense = Mathf.Min(100, character.Defense + Mathf.Max(0, addDefense));
        SaveToDisk();
        return true;
    }

    public bool TryUnlockHat(int price)
    {
        if (IsHatUnlocked) return true;
        if (!TrySpendCurrency(CurrencyType.Gold, price)) return false;

        IsHatUnlocked = true;
        EquippedSkinIndex = 1;
        EquippedShopSkinId = "hat_pink";
        EmitSignal(SignalName.InventoryChanged);
        SaveToDisk();
        return true;
    }

    public void SetEquippedSkin(int skinIndex)
    {
        if (skinIndex == 1 && !IsHatUnlocked) return;
        EquippedSkinIndex = skinIndex == 1 ? 1 : 0;
        EquippedShopSkinId = EquippedSkinIndex == 1 ? "hat_pink" : "outfit_default";
        EmitSignal(SignalName.InventoryChanged);
        SaveToDisk();
    }

    public int ConsumePendingShopTabIndex(int fallbackTab = 0)
    {
        int tab = PendingShopTabIndex >= 0 ? PendingShopTabIndex : fallbackTab;
        PendingShopTabIndex = -1;
        return tab;
    }

    public int ConsumePendingPreviewCharacterIndex(int fallbackIndex = 0)
    {
        int index = PendingPreviewCharacterIndex >= 0 ? PendingPreviewCharacterIndex : fallbackIndex;
        PendingPreviewCharacterIndex = -1;
        return index;
    }

    public int GetItemQuantity(string itemId) =>
        !string.IsNullOrEmpty(itemId) && _itemQuantities.TryGetValue(itemId, out int qty) ? qty : 0;

    private void RecordRecentItem(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return;
        _recentItemIds.Remove(itemId);
        _recentItemIds.Insert(0, itemId);
        if (_recentItemIds.Count > 24)
        {
            _recentItemIds.RemoveRange(24, _recentItemIds.Count - 24);
        }
    }

    public bool TryBuyShopItem(string itemId, int quantity, CurrencyType currency, int totalPrice)
    {
        if (string.IsNullOrEmpty(itemId) || quantity <= 0 || totalPrice < 0) return false;
        if (!TrySpendCurrency(currency, totalPrice)) return false;

        _itemQuantities[itemId] = GetItemQuantity(itemId) + quantity;
        RecordRecentItem(itemId);
        EmitSignal(SignalName.InventoryChanged);
        SaveToDisk();
        return true;
    }

    public sealed class GiftRewardEntry
    {
        public string Name { get; init; }
        public string AmountText { get; init; }
        public string IconPath { get; init; }
        public Color IconTint { get; init; } = Colors.White;
        public Color AccentColor { get; init; } = new Color(1.0f, 0.85f, 0.30f, 1f);
    }

    public bool TryOpenGiftBag(string giftId, out List<GiftRewardEntry> rewards, out string rewardSummary)
    {
        rewards = new List<GiftRewardEntry>();
        rewardSummary = "";
        int qty = GetItemQuantity(giftId);
        if (string.IsNullOrEmpty(giftId) || qty <= 0) return false;

        int nextQty = qty - 1;
        if (nextQty > 0) _itemQuantities[giftId] = nextQty;
        else _itemQuantities.Remove(giftId);

        if (giftId == "gift_tan_thu")
        {
            Coins += 300;
            Rau += 50;
            _itemQuantities["item_tra_da"] = GetItemQuantity("item_tra_da") + 2;
            RecordRecentItem("item_tra_da");

            rewards.Add(new GiftRewardEntry
            {
                Name = "Vàng Chợ",
                AmountText = "+300",
                IconPath = "res://assets/sprites/icons/coin.png",
                AccentColor = new Color(1.0f, 0.84f, 0.25f, 1f)
            });
            rewards.Add(new GiftRewardEntry
            {
                Name = "Rau Sạch",
                AmountText = "+50",
                IconPath = "res://assets/sprites/icons/rau.png",
                AccentColor = new Color(0.45f, 0.94f, 0.45f, 1f)
            });
            rewards.Add(new GiftRewardEntry
            {
                Name = "Trà Đá Hồi Sức",
                AmountText = "x2",
                IconPath = "res://assets/sprites/icons/emotion.png",
                IconTint = new Color(0.55f, 0.95f, 1.0f, 1f),
                AccentColor = new Color(0.42f, 0.90f, 1.0f, 1f)
            });
            rewardSummary = "Đã mở Túi Quà Tân Thủ: +300 Vàng · +50 Rau · x2 Trà Đá Hồi Sức!";
        }
        else
        {
            int rollGold = GD.RandRange(150, 400);
            int rollRau = GD.RandRange(25, 65);
            string[] consumablePool = { "item_tra_da", "item_nuoc_mia", "item_bua_x2", "item_hat_giong" };
            string pickedId = consumablePool[GD.RandRange(0, consumablePool.Length - 1)];
            var pickedMeta = GameCatalog.FindConsumable(pickedId);
            string pickedName = pickedMeta?.Name ?? pickedId;

            Coins += rollGold;
            Rau += rollRau;
            _itemQuantities[pickedId] = GetItemQuantity(pickedId) + 1;
            RecordRecentItem(pickedId);

            rewards.Add(new GiftRewardEntry
            {
                Name = "Vàng Chợ",
                AmountText = $"+{rollGold:N0}",
                IconPath = "res://assets/sprites/icons/coin.png",
                AccentColor = new Color(1.0f, 0.84f, 0.25f, 1f)
            });
            rewards.Add(new GiftRewardEntry
            {
                Name = "Rau Sạch",
                AmountText = $"+{rollRau}",
                IconPath = "res://assets/sprites/icons/rau.png",
                AccentColor = new Color(0.45f, 0.94f, 0.45f, 1f)
            });
            rewards.Add(new GiftRewardEntry
            {
                Name = pickedName,
                AmountText = "x1",
                IconPath = pickedMeta?.IconPath ?? "res://assets/sprites/icons/tab_shop.png",
                IconTint = pickedMeta?.IconTint ?? Colors.White,
                AccentColor = new Color(0.55f, 0.95f, 0.72f, 1f)
            });
            rewardSummary = $"Đã mở Túi Lộc: +{rollGold:N0} Vàng · +{rollRau} Rau · x1 {pickedName}!";
        }

        EmitSignal(SignalName.CurrenciesChanged, Coins, Rau);
        EmitSignal(SignalName.InventoryChanged);
        SaveToDisk();
        return true;
    }

    public bool IsSkinUnlocked(string skinId) => skinId switch
    {
        "outfit_default" => true,
        "hat_pink" => IsHatUnlocked,
        _ => !string.IsNullOrEmpty(skinId) && _unlockedExtraSkins.Contains(skinId)
    };

    public bool TryBuyShopSkin(string skinId, CurrencyType currency, int price)
    {
        if (string.IsNullOrEmpty(skinId)) return false;
        if (skinId == "hat_pink") return TryUnlockHat(price);
        if (IsSkinUnlocked(skinId)) return true;
        if (!TrySpendCurrency(currency, price)) return false;

        _unlockedExtraSkins.Add(skinId);
        EquippedShopSkinId = skinId;
        EmitSignal(SignalName.InventoryChanged);
        SaveToDisk();
        return true;
    }

    public bool TryEquipShopSkin(string skinId)
    {
        if (!IsSkinUnlocked(skinId)) return false;

        EquippedShopSkinId = skinId;
        if (skinId == "outfit_default") EquippedSkinIndex = 0;
        else if (skinId == "hat_pink") EquippedSkinIndex = 1;

        EmitSignal(SignalName.InventoryChanged);
        SaveToDisk();
        return true;
    }

    public bool TryExchangeCurrency(CurrencyType costCurrency, int costAmount, int gainCoins, int gainRau, bool isStarterGift = false)
    {
        if (isStarterGift && HasClaimedStarterShopGift) return false;
        if (costAmount > 0 && !TrySpendCurrency(costCurrency, costAmount)) return false;

        if (isStarterGift) HasClaimedStarterShopGift = true;

        int nextCoins = Coins + Mathf.Max(0, gainCoins);
        int nextRau = Rau + Mathf.Max(0, gainRau);
        SetBalances(nextCoins, nextRau, save: true);
        EmitSignal(SignalName.InventoryChanged);
        return true;
    }

    public bool TryExecuteMarketTrade(MarketTradeListing listing)
    {
        if (listing == null || listing.GiveAmount <= 0 || listing.ReceiveAmount <= 0) return false;

        switch (listing.GiveType)
        {
            case TradeAssetType.Item:
                if (string.IsNullOrEmpty(listing.GiveItemId) || GetItemQuantity(listing.GiveItemId) < listing.GiveAmount)
                    return false;
                break;
            case TradeAssetType.Gold:
                if (Coins < listing.GiveAmount) return false;
                break;
            case TradeAssetType.Rau:
                if (Rau < listing.GiveAmount) return false;
                break;
        }

        // Deduct what buyer gives
        int nextCoins = Coins;
        int nextRau = Rau;
        if (listing.GiveType == TradeAssetType.Item)
        {
            int left = GetItemQuantity(listing.GiveItemId) - listing.GiveAmount;
            if (left > 0) _itemQuantities[listing.GiveItemId] = left;
            else _itemQuantities.Remove(listing.GiveItemId);
        }
        else if (listing.GiveType == TradeAssetType.Gold)
        {
            nextCoins -= listing.GiveAmount;
        }
        else
        {
            nextRau -= listing.GiveAmount;
        }

        // Grant what buyer receives
        if (listing.ReceiveType == TradeAssetType.Item && !string.IsNullOrEmpty(listing.ReceiveItemId))
        {
            _itemQuantities[listing.ReceiveItemId] = GetItemQuantity(listing.ReceiveItemId) + listing.ReceiveAmount;
            RecordRecentItem(listing.ReceiveItemId);
        }
        else if (listing.ReceiveType == TradeAssetType.Gold)
        {
            nextCoins += listing.ReceiveAmount;
        }
        else if (listing.ReceiveType == TradeAssetType.Rau)
        {
            nextRau += listing.ReceiveAmount;
        }

        SetBalances(nextCoins, nextRau, save: false);
        EmitSignal(SignalName.InventoryChanged);
        SaveToDisk();
        return true;
    }

    public bool TryPostPlayerMarketListing(string giveItemId, int giveAmount, TradeAssetType receiveType, string receiveItemId, int receiveAmount)
    {
        if (string.IsNullOrEmpty(giveItemId) || giveAmount <= 0 || receiveAmount <= 0) return false;
        if (GetItemQuantity(giveItemId) < giveAmount) return false;
        if (receiveType == TradeAssetType.Item && (string.IsNullOrEmpty(receiveItemId) || receiveItemId == giveItemId)) return false;

        int remaining = GetItemQuantity(giveItemId) - giveAmount;
        if (remaining > 0) _itemQuantities[giveItemId] = remaining;
        else _itemQuantities.Remove(giveItemId);

        _playerMarketListings.Insert(0, new MarketTradeListing
        {
            Id = $"player_trade_{Guid.NewGuid():N}"[..20],
            SellerName = "Sạp của bạn",
            IsPlayerOwned = true,
            GiveType = TradeAssetType.Item,
            GiveItemId = giveItemId,
            GiveAmount = giveAmount,
            ReceiveType = receiveType,
            ReceiveItemId = receiveItemId,
            ReceiveAmount = receiveAmount
        });

        EmitSignal(SignalName.InventoryChanged);
        SaveToDisk();
        return true;
    }

    public bool TryCancelPlayerMarketListing(string listingId)
    {
        int idx = _playerMarketListings.FindIndex(x => x.Id == listingId);
        if (idx < 0) return false;

        var listing = _playerMarketListings[idx];
        _playerMarketListings.RemoveAt(idx);

        if (listing.GiveType == TradeAssetType.Item && !string.IsNullOrEmpty(listing.GiveItemId) && listing.GiveAmount > 0)
        {
            _itemQuantities[listing.GiveItemId] = GetItemQuantity(listing.GiveItemId) + listing.GiveAmount;
        }

        EmitSignal(SignalName.InventoryChanged);
        SaveToDisk();
        return true;
    }

    public bool TryBuyGachaTickets(int count, CurrencyType currency)
    {
        if (count <= 0) return false;
        int unitPrice = currency == CurrencyType.Rau ? TicketPriceRau : TicketPriceGold;
        long totalCostLong = (long)unitPrice * count;
        if (GetBalance(currency) < totalCostLong) return false;

        int totalCost = (int)totalCostLong;
        if (currency == CurrencyType.Rau) Rau -= totalCost;
        else Coins -= totalCost;

        long nextTickets = (long)GachaTickets + count;
        GachaTickets = nextTickets > int.MaxValue ? int.MaxValue : (int)nextTickets;
        RecordRecentItem("res_gacha_ticket");
        EmitSignal(SignalName.CurrenciesChanged, Coins, Rau);
        EmitSignal(SignalName.GachaStateChanged, GachaTickets, GachaPityCount);
        SaveToDisk();
        return true;
    }

    public List<GachaRewardResult> LastAutoClaimedMilestones { get; } = new();

    public List<GachaRewardResult> TryExecuteGachaRoll(int count)
    {
        if (count <= 0 || GachaTickets < count) return null;

        GachaTickets -= count;
        LastAutoClaimedMilestones.Clear();

        int cycleProgress = GachaMilestoneProgress + count;
        while (cycleProgress >= GachaMilestoneMax)
        {
            foreach (int target in GachaMilestoneTargets)
            {
                if (!_claimedGachaMilestones.Contains(target))
                {
                    _claimedGachaMilestones.Add(target);
                    LastAutoClaimedMilestones.Add(GrantGachaMilestoneRewardInternal(target));
                }
            }
            _claimedGachaMilestones.Clear();
            cycleProgress -= GachaMilestoneMax;
        }
        GachaMilestoneProgress = cycleProgress;

        var results = new List<GachaRewardResult>(count);
        bool hasUncommonOrBetter = false;

        for (int i = 0; i < count; i++)
        {
            GachaPityCount++;
            bool forceHighTier = GachaPityCount >= GachaPityLimit;
            bool guaranteeLastTen = (count >= 10 && i == count - 1 && !hasUncommonOrBetter);

            GachaRewardResult reward;
            if (forceHighTier)
            {
                reward = GrantHighTierGachaReward(preferCharacter: GD.Randf() < 0.35f);
                GachaPityCount = 0;
            }
            else
            {
                int roll = GD.RandRange(0, 9999);
                if (roll < 100) // 1% Nhân vật
                {
                    reward = GrantCharacterGachaReward();
                    GachaPityCount = 0;
                }
                else if (roll < 400) // 3% Trang phục
                {
                    reward = GrantSkinGachaReward();
                    GachaPityCount = 0;
                }
                else if (roll < 1000) // 6% +2 Vé
                {
                    GachaTickets += 2;
                    RecordRecentItem("res_gacha_ticket");
                    reward = new GachaRewardResult
                    {
                        Name = "Vé Rương May Mắn",
                        AmountText = "x2",
                        IconPath = "res://assets/sprites/icons/ticket.png",
                        Rarity = GachaRarity.Rare,
                        Note = "Hoàn thêm 2 Vé"
                    };
                }
                else if (roll < 3500 || guaranteeLastTen) // 25% Gói Vàng / Rau lớn
                {
                    reward = GrantResourcePackReward(largePack: true);
                }
                else // 65% Vật phẩm & tài nguyên thường
                {
                    reward = GrantCommonGachaReward();
                }
            }

            if (reward.Rarity >= GachaRarity.Uncommon)
            {
                hasUncommonOrBetter = true;
            }
            results.Add(reward);
        }

        EmitSignal(SignalName.CurrenciesChanged, Coins, Rau);
        EmitSignal(SignalName.InventoryChanged);
        EmitSignal(SignalName.GachaStateChanged, GachaTickets, GachaPityCount);
        SaveToDisk();
        return results;
    }

    private GachaRewardResult GrantHighTierGachaReward(bool preferCharacter)
    {
        if (preferCharacter)
        {
            return GrantCharacterGachaReward();
        }
        return GrantSkinGachaReward();
    }

    private GachaRewardResult GrantCharacterGachaReward()
    {
        var lockedChars = new List<CharacterProfile>();
        foreach (var ch in _characters)
        {
            if (!ch.IsUnlocked) lockedChars.Add(ch);
        }

        if (lockedChars.Count > 0)
        {
            var picked = lockedChars[GD.RandRange(0, lockedChars.Count - 1)];
            picked.IsUnlocked = true;
            return new GachaRewardResult
            {
                Name = picked.Name,
                AmountText = "Mở khóa",
                IconPath = "res://assets/sprites/icons/avatar_character.png",
                IconTint = picked.Tint,
                Rarity = GachaRarity.Legendary,
                Note = $"Nhân vật mới ({picked.Role})"
            };
        }

        Coins += 1200;
        return new GachaRewardResult
        {
            Name = "Vàng Quy Đổi",
            AmountText = "+1,200",
            IconPath = "res://assets/sprites/icons/coin.png",
            Rarity = GachaRarity.Legendary,
            Note = "Đã sở hữu hết nhân vật"
        };
    }

    private GachaRewardResult GrantSkinGachaReward()
    {
        (string Id, string Name, string IconPath, Color Tint)[] skinPool =
        {
            ("hat_pink", "Nón Hoa Hồng", "res://assets/sprites/icons/hat_pink.png", Colors.White),
            ("skin_apron_gold", "Áo Dài Hoàng Kim", "res://assets/sprites/icons/ao_dai.png", new Color(1.0f, 0.88f, 0.42f, 1f)),
            ("skin_hat_jade", "Nón Ngọc Bích", "res://assets/sprites/icons/hat_pink.png", new Color(0.55f, 1.0f, 0.85f, 1f)),
            ("skin_night_violet", "Áo Dài Chợ Đêm", "res://assets/sprites/icons/ao_dai.png", new Color(0.75f, 0.58f, 1.0f, 1f))
        };

        var lockedIndices = new List<int>();
        for (int i = 0; i < skinPool.Length; i++)
        {
            if (!IsSkinUnlocked(skinPool[i].Id)) lockedIndices.Add(i);
        }

        if (lockedIndices.Count > 0)
        {
            var picked = skinPool[lockedIndices[GD.RandRange(0, lockedIndices.Count - 1)]];
            if (picked.Id == "hat_pink") IsHatUnlocked = true;
            else _unlockedExtraSkins.Add(picked.Id);

            return new GachaRewardResult
            {
                Name = picked.Name,
                AmountText = "Mở khóa",
                IconPath = picked.IconPath,
                IconTint = picked.Tint,
                Rarity = GachaRarity.Epic,
                Note = "Trang phục hiếm"
            };
        }

        Coins += 800;
        return new GachaRewardResult
        {
            Name = "Vàng Quy Đổi",
            AmountText = "+800",
            IconPath = "res://assets/sprites/icons/coin.png",
            Rarity = GachaRarity.Epic,
            Note = "Đã sở hữu hết trang phục"
        };
    }

    private GachaRewardResult GrantResourcePackReward(bool largePack)
    {
        bool giveRau = GD.Randf() < 0.45f;
        if (giveRau)
        {
            int amount = largePack ? 60 : 20;
            Rau += amount;
            return new GachaRewardResult
            {
                Name = "Rau Sạch",
                AmountText = $"+{amount}",
                IconPath = "res://assets/sprites/icons/rau.png",
                Rarity = largePack ? GachaRarity.Uncommon : GachaRarity.Common,
                Note = largePack ? "Gói lớn" : "Tài nguyên"
            };
        }

        int gold = largePack ? 500 : 150;
        Coins += gold;
        return new GachaRewardResult
        {
            Name = "Vàng Chợ",
            AmountText = $"+{gold:N0}",
            IconPath = "res://assets/sprites/icons/coin.png",
            Rarity = largePack ? GachaRarity.Uncommon : GachaRarity.Common,
            Note = largePack ? "Gói lớn" : "Tài nguyên"
        };
    }

    private GachaRewardResult GrantCommonGachaReward()
    {
        float roll = GD.Randf();
        if (roll < 0.30f)
        {
            return GrantResourcePackReward(largePack: false);
        }
        if (roll < 0.42f)
        {
            _itemQuantities["gift_loc_cho"] = GetItemQuantity("gift_loc_cho") + 1;
            RecordRecentItem("gift_loc_cho");
            return new GachaRewardResult
            {
                Name = "Túi Lộc Phiên Chợ",
                AmountText = "x1",
                IconPath = "res://assets/sprites/icons/coin_poster.png",
                Rarity = GachaRarity.Uncommon,
                Note = "Mở trong Túi đồ -> Túi quà"
            };
        }

        (string Id, string Name, string IconPath, Color Tint)[] items =
        {
            ("item_tra_da", "Trà Đá Hồi Sức", "res://assets/sprites/icons/emotion.png", new Color(0.55f, 0.95f, 1.0f, 1f)),
            ("item_gio_hang", "Giỏ Hàng Siêu Tốc", "res://assets/sprites/icons/tab_shop.png", new Color(1.0f, 0.92f, 0.65f, 1f)),
            ("item_nuoc_mia", "Nước Mía Tăng Lực", "res://assets/sprites/icons/upgrade.png", new Color(1.0f, 0.85f, 0.35f, 1f)),
            ("item_bua_x2", "Bùa X2 Doanh Thu", "res://assets/sprites/icons/coin.png", Colors.White),
            ("item_hat_giong", "Túi Hạt Giống Quý", "res://assets/sprites/icons/rau.png", Colors.White),
            ("item_giap_noi", "Khiên Nắp Vung", "res://assets/sprites/icons/role.png", new Color(0.70f, 0.90f, 1.0f, 1f))
        };

        var picked = items[GD.RandRange(0, items.Length - 1)];
        _itemQuantities[picked.Id] = GetItemQuantity(picked.Id) + 1;
        RecordRecentItem(picked.Id);
        return new GachaRewardResult
        {
            Name = picked.Name,
            AmountText = "x1",
            IconPath = picked.IconPath,
            IconTint = picked.Tint,
            Rarity = GachaRarity.Common,
            Note = "Vật phẩm chợ"
        };
    }

    public bool IsGachaMilestoneClaimed(int milestoneTurns) => _claimedGachaMilestones.Contains(milestoneTurns);

    public GachaRewardResult TryClaimGachaMilestone(int milestoneTurns)
    {
        if (Array.IndexOf(GachaMilestoneTargets, milestoneTurns) < 0) return null;
        if (GachaMilestoneProgress < milestoneTurns || _claimedGachaMilestones.Contains(milestoneTurns)) return null;

        _claimedGachaMilestones.Add(milestoneTurns);
        GachaRewardResult reward = GrantGachaMilestoneRewardInternal(milestoneTurns);

        if (GachaMilestoneProgress >= GachaMilestoneMax && _claimedGachaMilestones.Count >= GachaMilestoneTargets.Length)
        {
            GachaMilestoneProgress = 0;
            _claimedGachaMilestones.Clear();
        }

        EmitSignal(SignalName.CurrenciesChanged, Coins, Rau);
        EmitSignal(SignalName.InventoryChanged);
        EmitSignal(SignalName.GachaStateChanged, GachaTickets, GachaPityCount);
        SaveToDisk();
        return reward;
    }

    private GachaRewardResult GrantGachaMilestoneRewardInternal(int milestoneTurns)
    {
        switch (milestoneTurns)
        {
            case 1:
                Coins += 150;
                return new GachaRewardResult
                {
                    Name = "Vàng Mốc 1",
                    AmountText = "+150",
                    IconPath = "res://assets/sprites/icons/coin.png",
                    Rarity = GachaRarity.Common,
                    Note = "Quà mốc 1 lượt"
                };
            case 5:
                _itemQuantities["item_tra_da"] = GetItemQuantity("item_tra_da") + 1;
                RecordRecentItem("item_tra_da");
                return new GachaRewardResult
                {
                    Name = "Trà Đá Hồi Sức",
                    AmountText = "x1",
                    IconPath = "res://assets/sprites/icons/emotion.png",
                    IconTint = new Color(0.55f, 0.95f, 1.0f, 1f),
                    Rarity = GachaRarity.Uncommon,
                    Note = "Quà mốc 5 lượt"
                };
            case 10:
                Rau += 50;
                return new GachaRewardResult
                {
                    Name = "Rau Sạch",
                    AmountText = "+50",
                    IconPath = "res://assets/sprites/icons/rau.png",
                    Rarity = GachaRarity.Uncommon,
                    Note = "Quà mốc 10 lượt"
                };
            case 15:
                GachaTickets += 2;
                RecordRecentItem("res_gacha_ticket");
                return new GachaRewardResult
                {
                    Name = "Vé Rương May Mắn",
                    AmountText = "+2 Vé",
                    IconPath = "res://assets/sprites/icons/ticket.png",
                    Rarity = GachaRarity.Rare,
                    Note = "Quà mốc 15 lượt"
                };
            default: // 30 lượt
                Coins += 1200;
                Rau += 80;
                return new GachaRewardResult
                {
                    Name = "Túi Lộc Đại Cát",
                    AmountText = "+1,200V · +80R",
                    IconPath = "res://assets/sprites/icons/coin_poster.png",
                    Rarity = GachaRarity.Legendary,
                    Note = "Quà mốc 30 lượt"
                };
        }
    }

    public void ResetGachaMilestones()
    {
        GachaMilestoneProgress = 0;
        _claimedGachaMilestones.Clear();
        EmitSignal(SignalName.GachaStateChanged, GachaTickets, GachaPityCount);
        SaveToDisk();
    }

    public void ResetAllData()
    {
        InitializeDefaults();
        EmitSignal(SignalName.CurrenciesChanged, Coins, Rau);
        EmitSignal(SignalName.GachaStateChanged, GachaTickets, GachaPityCount);
        EmitSignal(SignalName.SelectedCharacterChanged, SelectedCharacterIndex);
        EmitSignal(SignalName.InventoryChanged);
        SaveToDisk();
    }

    public void SaveToDisk()
    {
        try
        {
            var dto = new SaveDataDto
            {
                Coins = Coins,
                Rau = Rau,
                GachaTickets = GachaTickets,
                GachaPityCount = GachaPityCount,
                GachaMilestoneProgress = GachaMilestoneProgress,
                ClaimedGachaMilestones = new List<int>(_claimedGachaMilestones),
                SelectedCharacterIndex = SelectedCharacterIndex,
                IsHatUnlocked = IsHatUnlocked,
                EquippedSkinIndex = EquippedSkinIndex,
                EquippedShopSkinId = EquippedShopSkinId,
                HasClaimedStarterShopGift = HasClaimedStarterShopGift,
                HasReceivedInventoryStarterGift = HasReceivedInventoryStarterGift,
                HasReceivedLuckBagGift = HasReceivedLuckBagGift,
                Items = new Dictionary<string, int>(_itemQuantities),
                RecentItems = new List<string>(_recentItemIds),
                UnlockedExtraSkins = new List<string>(_unlockedExtraSkins),
                PlayerMarketListings = new List<MarketTradeListing>(_playerMarketListings),
                Characters = new List<CharacterSaveDto>(_characters.Length)
            };

            foreach (var ch in _characters)
            {
                dto.Characters.Add(new CharacterSaveDto
                {
                    Id = ch.Id,
                    Damage = ch.Damage,
                    Health = ch.Health,
                    Defense = ch.Defense,
                    IsUnlocked = ch.IsUnlocked
                });
            }

            string json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
            using var file = FileAccess.Open(SaveFilePath, FileAccess.ModeFlags.Write);
            if (file != null)
            {
                file.StoreString(json);
            }
            else
            {
                GD.PrintErr($"[PlayerDataManager] Không thể ghi file save: {FileAccess.GetOpenError()}");
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[PlayerDataManager] Lỗi lưu dữ liệu: {ex.Message}");
        }
    }

    private void LoadFromDisk()
    {
        if (!FileAccess.FileExists(SaveFilePath)) return;

        try
        {
            using var file = FileAccess.Open(SaveFilePath, FileAccess.ModeFlags.Read);
            if (file == null) return;

            string json = file.GetAsText();
            if (string.IsNullOrWhiteSpace(json)) return;

            var dto = JsonSerializer.Deserialize<SaveDataDto>(json);
            if (dto == null) return;

            Coins = Mathf.Max(0, dto.Coins);
            Rau = Mathf.Max(0, dto.Rau);
            GachaTickets = Mathf.Max(0, dto.GachaTickets);
            GachaPityCount = Mathf.Clamp(dto.GachaPityCount, 0, GachaPityLimit - 1);
            GachaMilestoneProgress = Mathf.Clamp(dto.GachaMilestoneProgress, 0, GachaMilestoneMax);
            _claimedGachaMilestones.Clear();
            if (dto.ClaimedGachaMilestones != null)
            {
                foreach (int m in dto.ClaimedGachaMilestones)
                {
                    if (Array.IndexOf(GachaMilestoneTargets, m) >= 0)
                        _claimedGachaMilestones.Add(m);
                }
            }
            IsHatUnlocked = dto.IsHatUnlocked;
            EquippedSkinIndex = (dto.EquippedSkinIndex == 1 && IsHatUnlocked) ? 1 : 0;
            HasClaimedStarterShopGift = dto.HasClaimedStarterShopGift;
            HasReceivedInventoryStarterGift = dto.HasReceivedInventoryStarterGift;
            HasReceivedLuckBagGift = dto.HasReceivedLuckBagGift;

            _itemQuantities.Clear();
            if (dto.Items != null)
            {
                foreach (var kv in dto.Items)
                {
                    if (!string.IsNullOrEmpty(kv.Key) && kv.Value > 0)
                    {
                        _itemQuantities[kv.Key] = kv.Value;
                    }
                }
            }

            if (!HasReceivedInventoryStarterGift)
            {
                HasReceivedInventoryStarterGift = true;
                _itemQuantities["gift_tan_thu"] = GetItemQuantity("gift_tan_thu") + 1;
                RecordRecentItem("gift_tan_thu");
            }
            if (!HasReceivedLuckBagGift)
            {
                HasReceivedLuckBagGift = true;
                _itemQuantities["gift_loc_cho"] = GetItemQuantity("gift_loc_cho") + 1;
                RecordRecentItem("gift_loc_cho");
            }

            _unlockedExtraSkins.Clear();
            if (dto.UnlockedExtraSkins != null)
            {
                foreach (var skinId in dto.UnlockedExtraSkins)
                {
                    if (!string.IsNullOrEmpty(skinId))
                    {
                        _unlockedExtraSkins.Add(skinId);
                    }
                }
            }

            _recentItemIds.Clear();
            if (dto.RecentItems != null)
            {
                foreach (var recentId in dto.RecentItems)
                {
                    if (!string.IsNullOrEmpty(recentId) && !_recentItemIds.Contains(recentId))
                    {
                        _recentItemIds.Add(recentId);
                    }
                }
            }

            _playerMarketListings.Clear();
            if (dto.PlayerMarketListings != null)
            {
                foreach (var listing in dto.PlayerMarketListings)
                {
                    if (listing != null && !string.IsNullOrEmpty(listing.Id) && listing.GiveAmount > 0 && listing.ReceiveAmount > 0)
                    {
                        listing.IsPlayerOwned = true;
                        _playerMarketListings.Add(listing);
                    }
                }
            }

            string candidateSkin = dto.EquippedShopSkinId;
            EquippedShopSkinId = !string.IsNullOrEmpty(candidateSkin) && IsSkinUnlocked(candidateSkin)
                ? candidateSkin
                : (EquippedSkinIndex == 1 ? "hat_pink" : "outfit_default");

            if (dto.Characters != null)
            {
                foreach (var saved in dto.Characters)
                {
                    foreach (var ch in _characters)
                    {
                        if (ch.Id == saved.Id)
                        {
                            ch.Damage = Mathf.Clamp(saved.Damage, 0, 100);
                            ch.Health = Mathf.Max(1, saved.Health);
                            ch.Defense = Mathf.Clamp(saved.Defense, 0, 100);
                            ch.IsUnlocked = ch.Id == 0 || saved.IsUnlocked;
                            break;
                        }
                    }
                }
            }

            if (dto.SelectedCharacterIndex >= 0 &&
                dto.SelectedCharacterIndex < _characters.Length &&
                _characters[dto.SelectedCharacterIndex].IsUnlocked)
            {
                SelectedCharacterIndex = dto.SelectedCharacterIndex;
            }
            else
            {
                SelectedCharacterIndex = 0;
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[PlayerDataManager] Lỗi đọc file save, dùng dữ liệu mặc định: {ex.Message}");
            InitializeDefaults();
        }
    }
}
