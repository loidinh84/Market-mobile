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

    private const string SaveFilePath = "user://save_data.json";
    private const int DefaultCoins = 123456;
    private const int DefaultRau = 123456;

    public int Coins { get; private set; } = DefaultCoins;
    public int Rau { get; private set; } = DefaultRau;
    public int SelectedCharacterIndex { get; private set; } = 0;
    public bool IsHatUnlocked { get; private set; } = false;
    public int EquippedSkinIndex { get; private set; } = 0;
    public string EquippedShopSkinId { get; private set; } = "outfit_default";
    public bool HasClaimedStarterShopGift { get; private set; } = false;

    /// <summary>
    /// Tab đích tạm thời khi chuyển sang shop_screen (ví dụ 3 = Đổi Vàng/Rau khi bấm dấu +).
    /// </summary>
    public int PendingShopTabIndex { get; set; } = -1;

    /// <summary>
    /// Nhân vật cần mở xem trước khi chuyển sang character_screen từ Cửa hàng.
    /// </summary>
    public int PendingPreviewCharacterIndex { get; set; } = -1;

    /// <summary>
    /// Khi true, lobby_screen sẽ tự mở SettingsPanel ngay khi _Ready().
    /// </summary>
    public bool PendingOpenSettings { get; set; } = false;

    private CharacterProfile[] _characters;
    private readonly Dictionary<string, int> _itemQuantities = new();
    private readonly HashSet<string> _unlockedExtraSkins = new();
    private readonly List<MarketTradeListing> _playerMarketListings = new();
    public CharacterProfile[] Characters => _characters;
    public IReadOnlyList<MarketTradeListing> PlayerMarketListings => _playerMarketListings;
    public IReadOnlyDictionary<string, int> ItemQuantities => _itemQuantities;

    public CharacterProfile ActiveCharacter =>
        _characters != null && SelectedCharacterIndex >= 0 && SelectedCharacterIndex < _characters.Length
            ? _characters[SelectedCharacterIndex]
            : null;

    private class SaveDataDto
    {
        public int Coins { get; set; } = DefaultCoins;
        public int Rau { get; set; } = DefaultRau;
        public int SelectedCharacterIndex { get; set; } = 0;
        public bool IsHatUnlocked { get; set; } = false;
        public int EquippedSkinIndex { get; set; } = 0;
        public string EquippedShopSkinId { get; set; } = "outfit_default";
        public bool HasClaimedStarterShopGift { get; set; } = false;
        public Dictionary<string, int> Items { get; set; } = new();
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
        SelectedCharacterIndex = 0;
        IsHatUnlocked = false;
        EquippedSkinIndex = 0;
        EquippedShopSkinId = "outfit_default";
        HasClaimedStarterShopGift = false;
        _itemQuantities.Clear();
        _unlockedExtraSkins.Clear();
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

    public bool TryBuyShopItem(string itemId, int quantity, CurrencyType currency, int totalPrice)
    {
        if (string.IsNullOrEmpty(itemId) || quantity <= 0 || totalPrice < 0) return false;
        if (!TrySpendCurrency(currency, totalPrice)) return false;

        _itemQuantities[itemId] = GetItemQuantity(itemId) + quantity;
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

    public void ResetAllData()
    {
        InitializeDefaults();
        EmitSignal(SignalName.CurrenciesChanged, Coins, Rau);
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
                SelectedCharacterIndex = SelectedCharacterIndex,
                IsHatUnlocked = IsHatUnlocked,
                EquippedSkinIndex = EquippedSkinIndex,
                EquippedShopSkinId = EquippedShopSkinId,
                HasClaimedStarterShopGift = HasClaimedStarterShopGift,
                Items = new Dictionary<string, int>(_itemQuantities),
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
            IsHatUnlocked = dto.IsHatUnlocked;
            EquippedSkinIndex = (dto.EquippedSkinIndex == 1 && IsHatUnlocked) ? 1 : 0;
            HasClaimedStarterShopGift = dto.HasClaimedStarterShopGift;

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
