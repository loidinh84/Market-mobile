using System;
using Godot;
using MarketMobile.Core;

namespace MarketMobile.Gameplay;

/// <summary>
/// Bộ quản lý tài nguyên trong trận đấu:
/// - Củ cải Đỏ hồi 1 củ mỗi 3s
/// - Củ cải Trắng hồi 1 củ mỗi 5s
/// - Vàng trong trận kiếm bằng cách hạ địch (khởi đầu = 0)
/// - Hồi chiêu triển khai từng nhân vật chống spam
/// </summary>
public partial class BattleRadishEconomy : Node
{
	[Signal]
	public delegate void RadishesChangedEventHandler(int redRadish, int whiteRadish, float redProgress, float whiteProgress);

	[Signal]
	public delegate void MatchGoldChangedEventHandler(int matchGold);

	[Signal]
	public delegate void DeployCooldownChangedEventHandler(int characterId, float remainingSeconds, float totalSeconds);

	public int RedRadish { get; private set; }
	public int WhiteRadish { get; private set; }
	public int MatchGold { get; private set; }
	public bool UnlimitedRedRadish { get; private set; }
	public bool UnlimitedWhiteRadish { get; private set; }

	public float RedProgress => _redTimer / GameCatalog.RedRadishIntervalSeconds;
	public float WhiteProgress => _whiteTimer / GameCatalog.WhiteRadishIntervalSeconds;

	private float _redTimer = 0f;
	private float _whiteTimer = 0f;
	private readonly float[] _cooldowns = new float[8];
	private readonly float[] _maxCooldowns = new float[8];

	public override void _Ready()
	{
		ResetForNewMatch();
	}

	public void ResetForNewMatch()
	{
		var data = PlayerDataManager.Instance ?? GetNodeOrNull<PlayerDataManager>("/root/PlayerDataManager");
		bool isCustom = data != null && data.SelectedPlayType == PlayMatchType.Custom;

		UnlimitedRedRadish = isCustom && data.CustomUnlimitedRedRadish;
		UnlimitedWhiteRadish = isCustom && data.CustomUnlimitedWhiteRadish;

		RedRadish = UnlimitedRedRadish ? 99 : (isCustom ? Mathf.Max(0, data.CustomStartingRedRadish) : 0);
		WhiteRadish = UnlimitedWhiteRadish ? 99 : (isCustom ? Mathf.Max(0, data.CustomStartingWhiteRadish) : 0);
		MatchGold = 0; // Vàng trong trận luôn bắt đầu bằng 0 và tăng khi hạ địch

		_redTimer = 0f;
		_whiteTimer = 0f;
		Array.Clear(_cooldowns, 0, _cooldowns.Length);
		Array.Clear(_maxCooldowns, 0, _maxCooldowns.Length);

		EmitSignal(SignalName.RadishesChanged, RedRadish, WhiteRadish, 0f, 0f);
		EmitSignal(SignalName.MatchGoldChanged, MatchGold);
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		if (dt <= 0f) return;

		bool needNotifyRadish = false;
		if (!UnlimitedRedRadish)
		{
			_redTimer += dt;
			while (_redTimer >= GameCatalog.RedRadishIntervalSeconds)
			{
				_redTimer -= GameCatalog.RedRadishIntervalSeconds;
				RedRadish++;
			}
			needNotifyRadish = true;
		}

		if (!UnlimitedWhiteRadish)
		{
			_whiteTimer += dt;
			while (_whiteTimer >= GameCatalog.WhiteRadishIntervalSeconds)
			{
				_whiteTimer -= GameCatalog.WhiteRadishIntervalSeconds;
				WhiteRadish++;
			}
			needNotifyRadish = true;
		}

		if (needNotifyRadish)
		{
			EmitSignal(SignalName.RadishesChanged, RedRadish, WhiteRadish, RedProgress, WhiteProgress);
		}

		for (int i = 0; i < _cooldowns.Length; i++)
		{
			if (_cooldowns[i] > 0f)
			{
				_cooldowns[i] = Mathf.Max(0f, _cooldowns[i] - dt);
				EmitSignal(SignalName.DeployCooldownChanged, i, _cooldowns[i], _maxCooldowns[i]);
			}
		}
	}

	public float GetRemainingCooldown(int charId) =>
		charId >= 0 && charId < _cooldowns.Length ? _cooldowns[charId] : 0f;

	public bool CanDeployCharacter(CharacterProfile profile)
	{
		if (profile == null || !profile.IsUnlocked) return false;
		if (GetRemainingCooldown(profile.Id) > 0f) return false;

		bool hasRed = UnlimitedRedRadish || RedRadish >= profile.RedRadishCost;
		bool hasWhite = UnlimitedWhiteRadish || WhiteRadish >= profile.WhiteRadishCost;
		return hasRed && hasWhite;
	}

	public bool TryDeployCharacter(CharacterProfile profile)
	{
		if (!CanDeployCharacter(profile)) return false;

		if (!UnlimitedRedRadish) RedRadish -= profile.RedRadishCost;
		if (!UnlimitedWhiteRadish) WhiteRadish -= profile.WhiteRadishCost;
		EmitSignal(SignalName.RadishesChanged, RedRadish, WhiteRadish, RedProgress, WhiteProgress);

		if (profile.Id >= 0 && profile.Id < _cooldowns.Length)
		{
			float cd = Mathf.Max(0.2f, profile.DeployCooldownSeconds);
			_cooldowns[profile.Id] = cd;
			_maxCooldowns[profile.Id] = cd;
			EmitSignal(SignalName.DeployCooldownChanged, profile.Id, cd, cd);
		}
		return true;
	}

	/// <summary>
	/// Cộng Vàng kiếm được trong trận khi hạ địch.
	/// </summary>
	public void AddKillGold(int amount)
	{
		if (amount <= 0) return;
		MatchGold += amount;
		EmitSignal(SignalName.MatchGoldChanged, MatchGold);
	}
}
