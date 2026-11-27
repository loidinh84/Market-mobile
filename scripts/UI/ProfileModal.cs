using Godot;
using MarketMobile.Core;
using MarketMobile.Utils;

namespace MarketMobile.UI;

/// <summary>
/// Popup Hồ sơ người chơi sử dụng profile_modal.png (khung ngọc-vàng) và profile_scroll.png (cuộn cờ đỏ).
/// Hiển thị thanh EXP, mức Rank mùa hiện tại và Rank cao nhất từng đạt (7 bậc rank),
/// kèm đổi tên, sao chép ID và thành tích chiến đấu.
/// </summary>
public partial class ProfileModal : Control
{
	[Signal]
	public delegate void ClosedEventHandler();

	public readonly struct RankTierInfo
	{
		public readonly string Name;
		public readonly string TexturePath;
		public readonly Color AccentColor;

		public RankTierInfo(string name, string texturePath, Color accentColor)
		{
			Name = name;
			TexturePath = texturePath;
			AccentColor = accentColor;
		}
	}

	public static readonly RankTierInfo[] RankTiers =
	{
		new("Đồng",         "res://assets/sprites/ui/ranks_128x128/rank_01_dong.png",         new Color(0.90f, 0.64f, 0.44f, 1f)),
		new("Bạc",          "res://assets/sprites/ui/ranks_128x128/rank_02_bac.png",          new Color(0.86f, 0.92f, 0.98f, 1f)),
		new("Vàng",         "res://assets/sprites/ui/ranks_128x128/rank_03_vang.png",         new Color(1.00f, 0.85f, 0.36f, 1f)),
		new("Ngọc Bích",    "res://assets/sprites/ui/ranks_128x128/rank_04_ngoc_bich.png",    new Color(0.45f, 0.96f, 0.72f, 1f)),
		new("Kim Cương",    "res://assets/sprites/ui/ranks_128x128/rank_05_kim_cuong.png",    new Color(0.50f, 0.91f, 1.00f, 1f)),
		new("Tinh Anh",     "res://assets/sprites/ui/ranks_128x128/rank_06_tinh_anh.png",     new Color(0.84f, 0.62f, 1.00f, 1f)),
		new("Trùm Chợ Lớn", "res://assets/sprites/ui/ranks_128x128/rank_07_trum_cho_lon.png", new Color(1.00f, 0.64f, 0.32f, 1f))
	};

	/// <summary>
	/// Bật true để chạm vào huy hiệu Rank trên cuộn cờ sẽ xoay xem trước đủ 7 bậc rank mẫu.
	/// </summary>
	[Export] public bool AllowClickToPreviewRanks { get; set; } = true;

	private Control _backdrop;
	private Control _dialogFrame;
	private Button _closeBtn;

	// Left Scroll Column (Avatar + Name + EXP + Season Rank + Highest Rank)
	private Button _scrollEditBtn;
	private Label _scrollNameLabel;
	private Label _levelLabel;
	private Label _expValueLabel;
	private ProgressBar _expBar;
	private Button _currentRankBtn;
	private TextureRect _currentRankIcon;
	private Label _currentRankNameLabel;
	private Label _currentRankSubLabel;
	private Button _highestRankBtn;
	private TextureRect _highestRankIcon;
	private Label _highestRankNameLabel;

	// Right Info Column
	private LineEdit _nameInput;
	private Button _saveNameBtn;
	private Label _renameCardCountLabel;
	private Label _nameStatusLabel;
	private Label _accountIdLabel;
	private Button _copyIdBtn;
	private Label _statMatchesValue;
	private Label _statWinRateValue;
	private Label _statHeroesValue;
	private Label _statSkinsValue;

	private readonly Texture2D[] _rankTextures = new Texture2D[7];
	private int _previewCurrentRank = 3;
	private int _previewHighestRank = 6;

	private PlayerDataManager _data;
	private Tween _modalTween;
	private SceneTreeTimer _feedbackTimer;

	public override void _Ready()
	{
		Visible = false;

		for (int i = 0; i < RankTiers.Length; i++)
		{
			_rankTextures[i] = GD.Load<Texture2D>(RankTiers[i].TexturePath);
		}

		_backdrop = GetNodeOrNull<Control>("Backdrop");
		_dialogFrame = GetNodeOrNull<Control>("Center/DialogFrame");
		_closeBtn = GetNodeOrNull<Button>("Center/DialogFrame/CloseBtn");

		// Left Scroll
		_scrollEditBtn = GetNodeOrNull<Button>("Center/DialogFrame/Body/ScrollColumn/ScrollContent/AvatarCenter/AvatarFrame/EditPenBtn");
		_scrollNameLabel = GetNodeOrNull<Label>("Center/DialogFrame/Body/ScrollColumn/ScrollContent/NameLabel");
		_levelLabel = GetNodeOrNull<Label>("Center/DialogFrame/Body/ScrollColumn/ScrollContent/ExpSection/ExpHeader/LevelLabel");
		_expValueLabel = GetNodeOrNull<Label>("Center/DialogFrame/Body/ScrollColumn/ScrollContent/ExpSection/ExpHeader/ExpValueLabel");
		_expBar = GetNodeOrNull<ProgressBar>("Center/DialogFrame/Body/ScrollColumn/ScrollContent/ExpSection/ExpBar");

		_currentRankBtn = GetNodeOrNull<Button>("Center/DialogFrame/Body/ScrollColumn/ScrollContent/RanksRow/CurrentRankBtn");
		_currentRankIcon = GetNodeOrNull<TextureRect>("Center/DialogFrame/Body/ScrollColumn/ScrollContent/RanksRow/CurrentRankBtn/Card/VBox/BadgeIcon");
		_currentRankNameLabel = GetNodeOrNull<Label>("Center/DialogFrame/Body/ScrollColumn/ScrollContent/RanksRow/CurrentRankBtn/Card/VBox/RankName");
		_currentRankSubLabel = GetNodeOrNull<Label>("Center/DialogFrame/Body/ScrollColumn/ScrollContent/RanksRow/CurrentRankBtn/Card/VBox/RankSub");

		_highestRankBtn = GetNodeOrNull<Button>("Center/DialogFrame/Body/ScrollColumn/ScrollContent/RanksRow/HighestRankBtn");
		_highestRankIcon = GetNodeOrNull<TextureRect>("Center/DialogFrame/Body/ScrollColumn/ScrollContent/RanksRow/HighestRankBtn/Card/VBox/BadgeIcon");
		_highestRankNameLabel = GetNodeOrNull<Label>("Center/DialogFrame/Body/ScrollColumn/ScrollContent/RanksRow/HighestRankBtn/Card/VBox/RankName");

		// Right Info Column
		_nameInput = GetNodeOrNull<LineEdit>("Center/DialogFrame/Body/InfoColumn/RenameCard/RenameVBox/InputRow/NameInput");
		_saveNameBtn = GetNodeOrNull<Button>("Center/DialogFrame/Body/InfoColumn/RenameCard/RenameVBox/InputRow/SaveNameBtn");
		_renameCardCountLabel = GetNodeOrNull<Label>("Center/DialogFrame/Body/InfoColumn/RenameCard/RenameVBox/HeaderRow/RenameCostBadge/HBox/CountLabel");
		_nameStatusLabel = GetNodeOrNull<Label>("Center/DialogFrame/Body/InfoColumn/RenameCard/RenameVBox/IdRow/StatusLabel");
		_accountIdLabel = GetNodeOrNull<Label>("Center/DialogFrame/Body/InfoColumn/RenameCard/RenameVBox/IdRow/IdValueLabel");
		_copyIdBtn = GetNodeOrNull<Button>("Center/DialogFrame/Body/InfoColumn/RenameCard/RenameVBox/IdRow/CopyIdBtn");

		_statMatchesValue = GetNodeOrNull<Label>("Center/DialogFrame/Body/InfoColumn/StatsCard/StatsVBox/StatsGrid/MatchesStat/HBox/VBox/Value");
		_statWinRateValue = GetNodeOrNull<Label>("Center/DialogFrame/Body/InfoColumn/StatsCard/StatsVBox/StatsGrid/WinRateStat/HBox/VBox/Value");
		_statHeroesValue = GetNodeOrNull<Label>("Center/DialogFrame/Body/InfoColumn/StatsCard/StatsVBox/StatsGrid/HeroesStat/HBox/VBox/Value");
		_statSkinsValue = GetNodeOrNull<Label>("Center/DialogFrame/Body/InfoColumn/StatsCard/StatsVBox/StatsGrid/SkinsStat/HBox/VBox/Value");

		if (_dialogFrame != null)
		{
			_dialogFrame.MouseFilter = MouseFilterEnum.Stop;
			_dialogFrame.PivotOffset = _dialogFrame.CustomMinimumSize / 2f;
		}

		if (_backdrop != null) _backdrop.GuiInput += OnBackdropInput;
		if (_closeBtn != null) _closeBtn.Pressed += CloseModal;
		if (_copyIdBtn != null) _copyIdBtn.Pressed += OnCopyIdPressed;
		if (_saveNameBtn != null) _saveNameBtn.Pressed += OnSaveNamePressed;
		if (_nameInput != null) _nameInput.TextSubmitted += (_) => OnSaveNamePressed();
		if (_scrollEditBtn != null) _scrollEditBtn.Pressed += OnFocusNameInput;
		if (_currentRankBtn != null) _currentRankBtn.Pressed += OnCycleCurrentRankPreview;
		if (_highestRankBtn != null) _highestRankBtn.Pressed += OnCycleHighestRankPreview;

		_data = PlayerDataManager.Instance ?? GetNodeOrNull<PlayerDataManager>("/root/PlayerDataManager");
		if (_data != null)
		{
			_data.ProfileChanged += OnProfileDataChanged;
			_data.InventoryChanged += OnInventoryChanged;
		}

		UIButtonHelper.AttachToAllButtons(this);
		RefreshView();
	}

	public override void _ExitTree()
	{
		_modalTween?.Kill();
		if (_data != null)
		{
			_data.ProfileChanged -= OnProfileDataChanged;
			_data.InventoryChanged -= OnInventoryChanged;
		}
	}

	public void OpenModal()
	{
		_data ??= PlayerDataManager.Instance ?? GetNodeOrNull<PlayerDataManager>("/root/PlayerDataManager");
		RefreshView();

		if (_nameStatusLabel != null)
		{
			_nameStatusLabel.Text = string.Empty;
		}

		Visible = true;
		MoveToFront();

		if (_dialogFrame != null)
		{
			_dialogFrame.PivotOffset = _dialogFrame.Size / 2f;
			_dialogFrame.Scale = new Vector2(0.88f, 0.88f);
			_dialogFrame.Modulate = new Color(1, 1, 1, 0);
		}
		if (_backdrop != null)
		{
			_backdrop.Modulate = new Color(1, 1, 1, 0);
		}

		_modalTween?.Kill();
		_modalTween = CreateTween().SetParallel(true);
		if (_backdrop != null)
		{
			_modalTween.TweenProperty(_backdrop, "modulate:a", 1f, 0.18f)
				.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
		}
		if (_dialogFrame != null)
		{
			_modalTween.TweenProperty(_dialogFrame, "scale", Vector2.One, 0.24f)
				.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
			_modalTween.TweenProperty(_dialogFrame, "modulate:a", 1f, 0.18f)
				.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
		}
	}

	public void CloseModal()
	{
		if (!Visible) return;

		_modalTween?.Kill();
		_modalTween = CreateTween().SetParallel(true);
		if (_backdrop != null)
		{
			_modalTween.TweenProperty(_backdrop, "modulate:a", 0f, 0.15f);
		}
		if (_dialogFrame != null)
		{
			_modalTween.TweenProperty(_dialogFrame, "scale", new Vector2(0.92f, 0.92f), 0.15f)
				.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
			_modalTween.TweenProperty(_dialogFrame, "modulate:a", 0f, 0.15f);
		}
		_modalTween.Chain().TweenCallback(Callable.From(() =>
		{
			Visible = false;
			EmitSignal(SignalName.Closed);
		}));
	}

	private void RefreshView()
	{
		string playerName = _data?.PlayerName ?? "Nguyễn Văn A";
		string playerId = _data?.PlayerId ?? "123467";

		if (_scrollNameLabel != null) _scrollNameLabel.Text = playerName;
		if (_accountIdLabel != null) _accountIdLabel.Text = $"ID tài khoản: {playerId}";
		if (_nameInput != null) _nameInput.Text = playerName;

		int renameCards = _data?.RenameCardCount ?? 1;
		if (_renameCardCountLabel != null)
		{
			_renameCardCountLabel.Text = $"Thẻ Đổi Tên: x{renameCards}";
			_renameCardCountLabel.AddThemeColorOverride(
				"font_color",
				renameCards > 0
					? new Color(1.0f, 0.90f, 0.50f, 1f)
					: new Color(1.0f, 0.52f, 0.48f, 1f)
			);
		}
		if (_saveNameBtn != null)
		{
			_saveNameBtn.Modulate = renameCards > 0
				? Colors.White
				: new Color(0.70f, 0.72f, 0.74f, 0.85f);
		}

		// EXP & Level
		int level = _data?.PlayerLevel ?? 12;
		int exp = _data?.PlayerExp ?? 1450;
		int expMax = _data?.PlayerExpToNextLevel ?? 2000;

		if (_levelLabel != null) _levelLabel.Text = $"Cấp {level}";
		if (_expValueLabel != null) _expValueLabel.Text = $"{exp:N0} / {expMax:N0}";
		if (_expBar != null)
		{
			_expBar.MaxValue = Mathf.Max(1, expMax);
			_expBar.Value = Mathf.Clamp(exp, 0, expMax);
		}

		// Season Rank & All-Time Highest Rank
		_previewCurrentRank = Mathf.Clamp(_data?.CurrentSeasonRankIndex ?? _previewCurrentRank, 0, RankTiers.Length - 1);
		_previewHighestRank = Mathf.Clamp(_data?.HighestRankIndex ?? _previewHighestRank, 0, RankTiers.Length - 1);
		int rankPoints = _data?.CurrentRankPoints ?? 1850;
		ApplyRankVisuals(_previewCurrentRank, rankPoints, _previewHighestRank);

		// Battle & Collection Stats
		int totalMatches = _data?.TotalMatches ?? 48;
		float winRate = _data?.WinRatePercent ?? 68.8f;

		int unlockedHeroes = 0;
		int totalHeroes = 0;
		if (_data?.Characters != null)
		{
			totalHeroes = _data.Characters.Length;
			foreach (var ch in _data.Characters)
			{
				if (ch != null && ch.IsUnlocked) unlockedHeroes++;
			}
		}

		int totalSkins = GameCatalog.Skins.Length;
		int unlockedSkins = 0;
		if (_data != null)
		{
			foreach (var skin in GameCatalog.Skins)
			{
				if (_data.IsSkinUnlocked(skin.Id)) unlockedSkins++;
			}
		}
		else
		{
			unlockedSkins = 1;
		}

		if (_statMatchesValue != null) _statMatchesValue.Text = $"{totalMatches:N0} trận";
		if (_statWinRateValue != null) _statWinRateValue.Text = $"{winRate:0.#}%";
		if (_statHeroesValue != null) _statHeroesValue.Text = totalHeroes > 0 ? $"{unlockedHeroes}/{totalHeroes}" : "1/1";
		if (_statSkinsValue != null) _statSkinsValue.Text = $"{unlockedSkins}/{totalSkins}";
	}

	private void ApplyRankVisuals(int currentRankIndex, int points, int highestRankIndex)
	{
		int curIdx = Mathf.Clamp(currentRankIndex, 0, RankTiers.Length - 1);
		int maxIdx = Mathf.Clamp(highestRankIndex, 0, RankTiers.Length - 1);

		var curTier = RankTiers[curIdx];
		if (_currentRankIcon != null) _currentRankIcon.Texture = _rankTextures[curIdx];
		if (_currentRankNameLabel != null)
		{
			_currentRankNameLabel.Text = curTier.Name;
			_currentRankNameLabel.AddThemeColorOverride("font_color", curTier.AccentColor);
		}
		if (_currentRankSubLabel != null) _currentRankSubLabel.Text = $"{points:N0} điểm";

		var maxTier = RankTiers[maxIdx];
		if (_highestRankIcon != null) _highestRankIcon.Texture = _rankTextures[maxIdx];
		if (_highestRankNameLabel != null)
		{
			_highestRankNameLabel.Text = maxTier.Name;
			_highestRankNameLabel.AddThemeColorOverride("font_color", maxTier.AccentColor);
		}
	}

	private void OnCycleCurrentRankPreview()
	{
		if (!AllowClickToPreviewRanks) return;
		_previewCurrentRank = (_previewCurrentRank + 1) % RankTiers.Length;
		int samplePoints = 500 + _previewCurrentRank * 450;
		if (_data != null)
		{
			_data.SetSeasonRank(_previewCurrentRank, samplePoints);
		}
		else
		{
			ApplyRankVisuals(_previewCurrentRank, samplePoints, _previewHighestRank);
		}
	}

	private void OnCycleHighestRankPreview()
	{
		if (!AllowClickToPreviewRanks) return;
		_previewHighestRank = (_previewHighestRank + 1) % RankTiers.Length;
		if (_data != null)
		{
			_data.SetHighestRankForPreview(_previewHighestRank);
		}
		else
		{
			ApplyRankVisuals(_previewCurrentRank, _data?.CurrentRankPoints ?? 1850, _previewHighestRank);
		}
	}

	private void OnFocusNameInput()
	{
		if (_nameInput == null) return;
		_nameInput.GrabFocus();
		_nameInput.SelectAll();
	}

	private void OnSaveNamePressed()
	{
		if (_nameInput == null) return;
		string candidate = _nameInput.Text.Trim();

		if (_data != null)
		{
			if (!_data.TryRenamePlayer(candidate, out string errorMessage))
			{
				ShowNameStatus(errorMessage, new Color(1f, 0.56f, 0.52f, 1f));
				return;
			}
		}
		else
		{
			if (candidate.Length < 2)
			{
				ShowNameStatus("Tên cần từ 2 đến 18 ký tự!", new Color(1f, 0.56f, 0.52f, 1f));
				return;
			}
			if (_scrollNameLabel != null) _scrollNameLabel.Text = candidate;
		}

		ShowNameStatus("✓ Đã đổi tên thành công!", new Color(0.54f, 0.96f, 0.72f, 1f));
	}

	private void ShowNameStatus(string message, Color color)
	{
		if (_nameStatusLabel == null) return;
		_nameStatusLabel.Text = message;
		_nameStatusLabel.AddThemeColorOverride("font_color", color);
	}

	private async void OnCopyIdPressed()
	{
		string id = _data?.PlayerId ?? "123467";
		DisplayServer.ClipboardSet(id);

		if (_copyIdBtn == null) return;
		_copyIdBtn.Text = "Đã chép!";
		_feedbackTimer = GetTree().CreateTimer(1.3);
		await ToSignal(_feedbackTimer, SceneTreeTimer.SignalName.Timeout);

		if (IsInstanceValid(this) && IsInstanceValid(_copyIdBtn))
		{
			_copyIdBtn.Text = "Sao chép ID";
		}
	}

	private void OnProfileDataChanged(string playerName, int avatarStyleIndex)
	{
		RefreshView();
	}

	private void OnInventoryChanged()
	{
		RefreshView();
	}

	private void OnBackdropInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseBtn && mouseBtn.Pressed && mouseBtn.ButtonIndex == MouseButton.Left)
		{
			CloseModal();
		}
	}
}
