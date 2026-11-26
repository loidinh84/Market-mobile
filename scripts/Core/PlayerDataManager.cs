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

    private const string SaveFilePath = "user://save_data.json";
    private const int DefaultCoins = 123456;
    private const int DefaultRau = 123456;

    public int Coins { get; private set; } = DefaultCoins;
    public int Rau { get; private set; } = DefaultRau;
    public int SelectedCharacterIndex { get; private set; } = 0;
    public bool IsHatUnlocked { get; private set; } = false;
    public int EquippedSkinIndex { get; private set; } = 0;

    private CharacterProfile[] _characters;
    public CharacterProfile[] Characters => _characters;

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
        _characters = CreateDefaultCharacters();
    }

    public static CharacterProfile[] CreateDefaultCharacters() => new[]
    {
        new CharacterProfile { Id = 0, Name = "Bà Bảy", Role = "Cận chiến", Damage = 15, Health = 250, Defense = 20, Price = 0, IsUnlocked = true, Tint = Colors.White },
        new CharacterProfile { Id = 1, Name = "Chú Tư Bán Cá", Role = "Đỡ đòn", Damage = 10, Health = 420, Defense = 40, Price = 2500, Currency = CurrencyType.Gold, IsUnlocked = false, Tint = new Color(0.9f, 0.95f, 1.0f) },
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

    public bool TryUnlockCharacter(int index)
    {
        var character = GetCharacter(index);
        if (character == null || character.IsUnlocked) return false;

        if (!TrySpendCurrency(character.Currency, character.Price)) return false;

        character.IsUnlocked = true;
        SelectedCharacterIndex = index;
        EmitSignal(SignalName.SelectedCharacterChanged, index);
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
        SaveToDisk();
        return true;
    }

    public void SetEquippedSkin(int skinIndex)
    {
        if (skinIndex == 1 && !IsHatUnlocked) return;
        EquippedSkinIndex = skinIndex == 1 ? 1 : 0;
        SaveToDisk();
    }

    public void ResetAllData()
    {
        InitializeDefaults();
        EmitSignal(SignalName.CurrenciesChanged, Coins, Rau);
        EmitSignal(SignalName.SelectedCharacterChanged, SelectedCharacterIndex);
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
