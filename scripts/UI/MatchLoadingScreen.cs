using System.Collections.Generic;
using Godot;
using MarketMobile.Core;
using MarketMobile.Gameplay;
using MarketMobile.Utils;

namespace MarketMobile.UI;
public partial class MatchLoadingScreen : Control
{
	private sealed class LoadingPlayerCard
	{
		public Button RootBtn;
		public Control FrontView;
		public Control BackView;
		public Label PercentLabel;
		public ProgressBar MiniProgress;
		public bool IsFlipped;
		public bool IsSelf;
		public float LoadSpeed;
		public float SimulatedPercent;
	}

	private const float MinVisualLoadSeconds = 3.2f;

	private static readonly Font OrcishFont = GD.Load<Font>("res://assets/fonts/FZ-Orcish.ttf");
	private static readonly Texture2D TexAvatar = GD.Load<Texture2D>("res://assets/sprites/icons/avatar_character.png");

	private static readonly string[] RankIconPaths =
	{
		"res://assets/sprites/ui/ranks_128x128/rank_01_dong.png",
		"res://assets/sprites/ui/ranks_128x128/rank_02_bac.png",
		"res://assets/sprites/ui/ranks_128x128/rank_03_vang.png",
		"res://assets/sprites/ui/ranks_128x128/rank_04_ngoc_bich.png",
		"res://assets/sprites/ui/ranks_128x128/rank_05_kim_cuong.png",
		"res://assets/sprites/ui/ranks_128x128/rank_06_tinh_anh.png",
		"res://assets/sprites/ui/ranks_128x128/rank_07_trum_cho_lon.png"
	};

	private static readonly string[] QuickStickerPaths =
	{
		"res://assets/sprites/stickers/packs/capybara/capybara_01.png",
		"res://assets/sprites/stickers/packs/capybara/capybara_04.png",
		"res://assets/sprites/stickers/packs/duck/duck_01.png",
		"res://assets/sprites/stickers/packs/pochacco/pochacco_01.png"
	};

	private static readonly string[] MarketTips =
	{
		"Mẹo: Chạm vào thẻ người chơi để lật xem vật phẩm và chỉ số!",
		"Mẹo: Kết hợp tướng Đỡ đòn đi trước và Xạ thủ phía sau để giữ đường hiệu quả.",
		"Mẹo: Trà Đá Hồi Sức giúp lật kèo giao tranh ngay khi máu xuống thấp.",
		"Mẹo: Bùa X2 Doanh Thu nhân đôi lượng Vàng nhận được khi giành chiến thắng.",
		"Mẹo: Cân đối Củ Cải Đỏ và Củ Cải Trắng để gọi tướng liên tục không bị hụt nhịp.",
		"Mẹo: Trong 2 VS 2, hãy quan sát đội hình đồng đội để bù đắp vai trò còn thiếu."
	};

	private TextureRect _mapBgFallback;
	private SubViewportContainer _mapBgViewportContainer;
	private SubViewport _mapBgViewport;
	private Label _mapTitleLabel;
	private VBoxContainer _blueTeamVBox;
	private Control _vsBadge;
	private VBoxContainer _redTeamVBox;
	private HBoxContainer _emoteBar;
	private Button _tipBtn;
	private ProgressBar _loadingBar;
	private Label _loadingStatusLabel;
	private Control _emoteFloatLayer;

	private PlayerDataManager _data;
	private string _targetScenePath = "res://scenes/gameplay/map_01_street.tscn";
	private bool _threadedRequested = false;
	private bool _sceneTransitioned = false;
	private float _elapsed = 0f;
	private float _realProgress = 0f;
	private float _displayedProgress = 0f;
	private int _currentTipIndex = 0;
	private bool _npcEmoteSent = false;

	private readonly List<LoadingPlayerCard> _playerCards = new();
	private LoadingPlayerCard _selfCard;
	private readonly Godot.Collections.Array _threadedProgressArray = new();

	public override void _Ready()
	{
		_mapBgFallback = GetNodeOrNull<TextureRect>("MapBgFallback");
		_mapBgViewportContainer = GetNodeOrNull<SubViewportContainer>("MapBgViewportContainer");
		_mapBgViewport = GetNodeOrNull<SubViewport>("MapBgViewportContainer/MapBgViewport");
		_mapTitleLabel = GetNodeOrNull<Label>("MainVBox/HeaderPanel/HBox/MapTitleLabel");
		_blueTeamVBox = GetNodeOrNull<VBoxContainer>("MainVBox/ArenaCenter/VersusHBox/BlueTeamVBox");
		_vsBadge = GetNodeOrNull<Control>("MainVBox/ArenaCenter/VersusHBox/VsBadge");
		_redTeamVBox = GetNodeOrNull<VBoxContainer>("MainVBox/ArenaCenter/VersusHBox/RedTeamVBox");
		_emoteBar = GetNodeOrNull<HBoxContainer>("MainVBox/FooterPanel/VBox/TopRow/EmoteBar");
		_tipBtn = GetNodeOrNull<Button>("MainVBox/FooterPanel/VBox/TopRow/TipBtn");
		_loadingBar = GetNodeOrNull<ProgressBar>("MainVBox/FooterPanel/VBox/ProgressRow/LoadingBar");
		_loadingStatusLabel = GetNodeOrNull<Label>("MainVBox/FooterPanel/VBox/ProgressRow/LoadingStatusLabel");
		_emoteFloatLayer = GetNodeOrNull<Control>("EmoteFloatLayer");

		_data = PlayerDataManager.Instance ?? GetNodeOrNull<PlayerDataManager>("/root/PlayerDataManager");
		var map = GameCatalog.FindMap(_data?.SelectedMapId);
		_targetScenePath = !string.IsNullOrEmpty(_data?.ActiveMatchScenePath)
			? _data.ActiveMatchScenePath
			: (map?.ScenePath ?? "res://scenes/gameplay/map_01_street.tscn");

		if (_mapTitleLabel != null)
		{
			var playType = _data != null ? _data.SelectedPlayType : PlayMatchType.Normal;
			var teamMode = _data != null ? _data.SelectedTeamMode : TeamMode.Solo1v1;
			_mapTitleLabel.Text = $"{map?.Name ?? "Chợ Đầu Mối"} · {GameCatalog.GetPlayTypeShortLabel(playType)} · {GameCatalog.GetTeamModeLabel(teamMode)}";
		}

		if (_tipBtn != null)
		{
			_tipBtn.Text = MarketTips[0];
			_tipBtn.Pressed += CycleNextTip;
		}

		SetupMapBackground(map);
		BuildVersusCards();
		BuildQuickEmoteBar();
		PlayIntroAnimation();
		BeginThreadedMapLoad();
	}

	public override void _Process(double delta)
	{
		if (_sceneTransitioned) return;

		float dt = (float)delta;
		_elapsed += dt;

		// Đồng đội/đối thủ thả 1 sticker vui sau ~1.5s
		if (!_npcEmoteSent && _elapsed >= 1.5f && _playerCards.Count > 1)
		{
			_npcEmoteSent = true;
			var npcCard = _playerCards[_playerCards.Count - 1];
			SpawnFloatingSticker(npcCard.RootBtn, QuickStickerPaths[GD.RandRange(0, QuickStickerPaths.Length - 1)]);
		}

		bool realLoaded = false;
		if (_threadedRequested)
		{
			var status = ResourceLoader.LoadThreadedGetStatus(_targetScenePath, _threadedProgressArray);
			if (_threadedProgressArray.Count > 0)
			{
				_realProgress = Mathf.Clamp(_threadedProgressArray[0].AsSingle(), 0f, 1f);
			}
			if (status == ResourceLoader.ThreadLoadStatus.Loaded)
			{
				_realProgress = 1f;
				realLoaded = true;
			}
			else if (status == ResourceLoader.ThreadLoadStatus.Failed || status == ResourceLoader.ThreadLoadStatus.InvalidResource)
			{
				_realProgress = 1f;
				realLoaded = true;
			}
		}
		else
		{
			_realProgress = 1f;
			realLoaded = true;
		}

		float timeProgress = Mathf.Clamp(_elapsed / MinVisualLoadSeconds, 0f, 1f);
		float targetVisual = Mathf.Min(_realProgress, timeProgress);
		_displayedProgress = Mathf.MoveToward(_displayedProgress, targetVisual, dt * 0.85f);

		int pct = Mathf.Clamp(Mathf.RoundToInt(_displayedProgress * 100f), 0, 100);
		if (_loadingBar != null) _loadingBar.Value = pct;
		if (_loadingStatusLabel != null)
		{
			_loadingStatusLabel.Text = pct >= 100 ? "Vào trận!" : $"Đang tải trận... {pct}%";
		}

		foreach (var card in _playerCards)
		{
			if (card.IsSelf)
			{
				card.SimulatedPercent = pct;
			}
			else
			{
				float npcTarget = Mathf.Clamp(timeProgress * 100f * card.LoadSpeed, 0f, 100f);
				card.SimulatedPercent = Mathf.MoveToward(card.SimulatedPercent, npcTarget, dt * 65f);
			}

			int cardPct = Mathf.RoundToInt(card.SimulatedPercent);
			if (card.PercentLabel != null) card.PercentLabel.Text = $"{cardPct}%";
			if (card.MiniProgress != null) card.MiniProgress.Value = cardPct;
		}

		if (realLoaded && _displayedProgress >= 0.999f && _elapsed >= MinVisualLoadSeconds)
		{
			EnterBattleScene();
		}
	}

	private void BeginThreadedMapLoad()
	{
		if (string.IsNullOrEmpty(_targetScenePath)) return;
		var err = ResourceLoader.LoadThreadedRequest(_targetScenePath);
		_threadedRequested = err == Error.Ok;
	}

	private void EnterBattleScene()
	{
		if (_sceneTransitioned) return;
		_sceneTransitioned = true;

		if (_threadedRequested)
		{
			var loaded = ResourceLoader.LoadThreadedGet(_targetScenePath) as PackedScene;
			if (loaded != null)
			{
				GetTree().ChangeSceneToPacked(loaded);
				return;
			}
		}

		GetTree().ChangeSceneToFile(_targetScenePath);
	}

	private void SetupMapBackground(MapMeta map)
	{
		if (_mapBgFallback != null && map != null && !string.IsNullOrEmpty(map.TexturePath))
		{
			var fallbackTex = GD.Load<Texture2D>(map.TexturePath);
			if (fallbackTex != null) _mapBgFallback.Texture = fallbackTex;
			_mapBgFallback.Modulate = map.BannerTint;
		}

		if (_mapBgViewport == null || string.IsNullOrEmpty(_targetScenePath))
		{
			if (_mapBgViewportContainer != null) _mapBgViewportContainer.Visible = false;
			return;
		}

		var packedMap = GD.Load<PackedScene>(_targetScenePath);
		var mapInstance = packedMap?.Instantiate();
		if (mapInstance == null)
		{
			if (_mapBgViewportContainer != null) _mapBgViewportContainer.Visible = false;
			return;
		}

		ConfigureBackgroundPreviewCamera(mapInstance);
		_mapBgViewport.AddChild(mapInstance);
		if (_mapBgViewportContainer != null)
		{
			_mapBgViewportContainer.Modulate = map != null ? map.BannerTint : Colors.White;
			_mapBgViewportContainer.Visible = true;
		}
		if (_mapBgFallback != null) _mapBgFallback.Visible = false;
	}

	private static void ConfigureBackgroundPreviewCamera(Node root)
	{
		if (root is BattleCamera cam)
		{
			cam.StartFocus = BattleCamera.InitialFocusMode.MapCenter;
			cam.Ready += () =>
			{
				cam.SetProcessUnhandledInput(false);
				cam.SetProcess(false);
			};
		}

		foreach (Node child in root.GetChildren())
		{
			ConfigureBackgroundPreviewCamera(child);
		}
	}

	private void BuildVersusCards()
	{
		_playerCards.Clear();
		if (_blueTeamVBox != null)
		{
			foreach (Node child in _blueTeamVBox.GetChildren()) child.QueueFree();
		}
		if (_redTeamVBox != null)
		{
			foreach (Node child in _redTeamVBox.GetChildren()) child.QueueFree();
		}

		var playType = _data != null ? _data.SelectedPlayType : PlayMatchType.Normal;
		var teamMode = _data != null ? _data.SelectedTeamMode : TeamMode.Solo1v1;
		bool isDuo = teamMode == TeamMode.Duo2v2;

		string selfName = !string.IsNullOrEmpty(_data?.PlayerName) ? _data.PlayerName : "Bạn";
		int selfLevel = _data != null ? _data.PlayerLevel : 12;
		int selfRank = _data != null ? _data.CurrentSeasonRankIndex : 3;
		int selfMatches = _data != null ? _data.TotalMatches : 48;
		int selfWinRate = _data != null ? Mathf.RoundToInt(_data.WinRatePercent) : 68;
		int[] selfChars = _data?.ActiveMatchCharacterIndices is { Length: > 0 } ? _data.ActiveMatchCharacterIndices : new[] { 0 };
		string[] selfItems = _data?.ActiveMatchItemIds ?? System.Array.Empty<string>();

		// Phe bạn (Trái)
		_selfCard = CreatePlayerCard(
			_blueTeamVBox, isBlueTeam: true, isSelf: true,
			selfName, selfLevel, selfRank, selfWinRate, selfMatches,
			selfChars, selfItems, loadSpeed: 1.0f);

		if (isDuo)
		{
			string mateName = playType == PlayMatchType.Bot ? "Bot Đồng Đội" : "Trần Minh Khoa";
			CreatePlayerCard(
				_blueTeamVBox, isBlueTeam: true, isSelf: false,
				mateName, 14, 2, 62, 74,
				new[] { 0, 1, 2 }, new[] { "item_tra_da", "item_bua_x2" }, loadSpeed: 1.08f);
		}

		// Phe đối thủ (Phải)
		string opp1Name = playType == PlayMatchType.Bot ? "Bot Tiểu Thương #1" : "Hai Lúa Miền Tây";
		CreatePlayerCard(
			_redTeamVBox, isBlueTeam: false, isSelf: false,
			opp1Name, 15, 3, 59, 91,
			new[] { 1, 2, 3 }, new[] { "item_nuoc_mia", "item_giap_noi" }, loadSpeed: 1.15f);

		if (isDuo)
		{
			string opp2Name = playType == PlayMatchType.Bot ? "Bot Tiểu Thương #2" : "Chị Tư Sạp Rau";
			CreatePlayerCard(
				_redTeamVBox, isBlueTeam: false, isSelf: false,
				opp2Name, 13, 2, 55, 63,
				new[] { 0, 3 }, new[] { "item_tra_da", "item_gio_hang" }, loadSpeed: 0.96f);
		}
	}

	private LoadingPlayerCard CreatePlayerCard(
		VBoxContainer parent,
		bool isBlueTeam,
		bool isSelf,
		string playerName,
		int level,
		int rankIndex,
		int winRatePct,
		int totalMatches,
		int[] charIndices,
		string[] itemIds,
		float loadSpeed)
	{
		var btn = new Button
		{
			CustomMinimumSize = new Vector2(610, 210),
			FocusMode = FocusModeEnum.None,
			MouseDefaultCursorShape = CursorShape.PointingHand
		};

		Color borderCol = isSelf
			? new Color(0.98f, 0.80f, 0.30f, 0.90f)
			: (isBlueTeam ? new Color(0.36f, 0.68f, 0.98f, 0.68f) : new Color(0.94f, 0.42f, 0.40f, 0.68f));
		Color bgCol = isBlueTeam
			? new Color(0.08f, 0.12f, 0.24f, 0.56f)
			: new Color(0.22f, 0.09f, 0.14f, 0.56f);

		var style = new StyleBoxFlat
		{
			BgColor = bgCol,
			BorderWidthLeft = isSelf ? 3 : 2,
			BorderWidthTop = isSelf ? 3 : 2,
			BorderWidthRight = isSelf ? 3 : 2,
			BorderWidthBottom = isSelf ? 3 : 2,
			BorderColor = borderCol,
			CornerRadiusTopLeft = 18,
			CornerRadiusTopRight = 18,
			CornerRadiusBottomRight = 18,
			CornerRadiusBottomLeft = 18
		};
		btn.AddThemeStyleboxOverride("normal", style);
		btn.AddThemeStyleboxOverride("hover", style);
		btn.AddThemeStyleboxOverride("pressed", style);

		var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
		margin.SetAnchorsPreset(LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", 18);
		margin.AddThemeConstantOverride("margin_top", 14);
		margin.AddThemeConstantOverride("margin_right", 18);
		margin.AddThemeConstantOverride("margin_bottom", 14);
		btn.AddChild(margin);

		var outerVBox = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		outerVBox.AddThemeConstantOverride("separation", 10);
		margin.AddChild(outerVBox);

		// Header thẻ: Avatar + Tên + Rank + % Load
		var headerHBox = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		headerHBox.AddThemeConstantOverride("separation", 14);
		outerVBox.AddChild(headerHBox);

		if (TexAvatar != null)
		{
			var avatarFrame = new PanelContainer
			{
				CustomMinimumSize = new Vector2(64, 64),
				MouseFilter = MouseFilterEnum.Ignore
			};
			avatarFrame.AddThemeStyleboxOverride("panel", MakeMiniTileStyle(borderCol));
			avatarFrame.AddChild(new TextureRect
			{
				Texture = TexAvatar,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				MouseFilter = MouseFilterEnum.Ignore
			});
			headerHBox.AddChild(avatarFrame);
		}

		var nameVBox = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			Alignment = BoxContainer.AlignmentMode.Center,
			MouseFilter = MouseFilterEnum.Ignore
		};
		nameVBox.AddThemeConstantOverride("separation", 2);
		headerHBox.AddChild(nameVBox);

		string displayName = isSelf ? $"{playerName} (Bạn)" : playerName;
		nameVBox.AddChild(MakeLabel(displayName, new Color(1f, 0.96f, 0.85f, 1f), 25, expandFill: true, clip: true));
		nameVBox.AddChild(MakeLabel($"Cấp {level} · Chạm để xem chi tiết", new Color(0.76f, 0.84f, 0.98f, 0.86f), 18, expandFill: true, clip: true));

		int clampedRank = Mathf.Clamp(rankIndex, 0, RankIconPaths.Length - 1);
		var rankTex = GD.Load<Texture2D>(RankIconPaths[clampedRank]);
		if (rankTex != null)
		{
			headerHBox.AddChild(new TextureRect
			{
				Texture = rankTex,
				CustomMinimumSize = new Vector2(52, 52),
				SizeFlagsVertical = SizeFlags.ShrinkCenter,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				MouseFilter = MouseFilterEnum.Ignore
			});
		}

		var pctLbl = MakeLabel("0%", new Color(0.50f, 0.96f, 0.72f, 1f), 26, expandFill: false, clip: false);
		pctLbl.CustomMinimumSize = new Vector2(68, 0);
		pctLbl.HorizontalAlignment = HorizontalAlignment.Right;
		headerHBox.AddChild(pctLbl);

		// Mặt trước: Đội hình nhân vật (không bọc hộp chữ nhật quanh chữ)
		var frontView = new HBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore
		};
		frontView.AddThemeConstantOverride("separation", 18);
		outerVBox.AddChild(frontView);

		var chars = _data?.Characters ?? PlayerDataManager.CreateDefaultCharacters();
		foreach (int ci in charIndices)
		{
			if (ci < 0 || ci >= chars.Length) continue;
			var ch = chars[ci];

			var row = new HBoxContainer
			{
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				SizeFlagsVertical = SizeFlags.ShrinkCenter,
				MouseFilter = MouseFilterEnum.Ignore
			};
			row.AddThemeConstantOverride("separation", 8);

			if (TexAvatar != null)
			{
				var iconFrame = new PanelContainer
				{
					CustomMinimumSize = new Vector2(46, 46),
					SizeFlagsVertical = SizeFlags.ShrinkCenter,
					MouseFilter = MouseFilterEnum.Ignore
				};
				iconFrame.AddThemeStyleboxOverride("panel", MakeMiniTileStyle(new Color(0.28f, 0.74f, 0.56f, 0.9f)));
				iconFrame.AddChild(new TextureRect
				{
					Texture = TexAvatar,
					Modulate = ch.Tint,
					ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
					StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
					MouseFilter = MouseFilterEnum.Ignore
				});
				row.AddChild(iconFrame);
			}
			row.AddChild(MakeLabel(ch.Name, new Color(0.98f, 0.97f, 0.92f, 0.98f), 20, expandFill: true, clip: true));
			frontView.AddChild(row);
		}

		// Mặt sau (khi chạm lật thẻ): Vật phẩm mang theo + Tỉ lệ thắng (không bọc hộp chữ nhật)
		var backView = new HBoxContainer
		{
			Visible = false,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore
		};
		backView.AddThemeConstantOverride("separation", 18);
		outerVBox.AddChild(backView);

		backView.AddChild(MakeLabel($"Thắng {winRatePct}% · {totalMatches} trận", new Color(1f, 0.90f, 0.56f, 1f), 21, expandFill: false, clip: false));

		if (itemIds == null || itemIds.Length == 0)
		{
			backView.AddChild(MakeLabel("· Không mang vật phẩm", new Color(0.84f, 0.86f, 0.94f, 0.85f), 20, expandFill: false, clip: false));
		}
		else
		{
			foreach (string id in itemIds)
			{
				var meta = GameCatalog.FindConsumable(id);
				if (meta == null) continue;

				var row = new HBoxContainer
				{
					SizeFlagsHorizontal = SizeFlags.ExpandFill,
					SizeFlagsVertical = SizeFlags.ShrinkCenter,
					MouseFilter = MouseFilterEnum.Ignore
				};
				row.AddThemeConstantOverride("separation", 8);

				var tex = !string.IsNullOrEmpty(meta.IconPath) ? GD.Load<Texture2D>(meta.IconPath) : null;
				if (tex != null)
				{
					var iconFrame = new PanelContainer
					{
						CustomMinimumSize = new Vector2(40, 40),
						SizeFlagsVertical = SizeFlags.ShrinkCenter,
						MouseFilter = MouseFilterEnum.Ignore
					};
					iconFrame.AddThemeStyleboxOverride("panel", MakeMiniTileStyle(new Color(0.88f, 0.66f, 0.28f, 0.9f)));
					iconFrame.AddChild(new TextureRect
					{
						Texture = tex,
						Modulate = meta.IconTint,
						ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
						StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
						MouseFilter = MouseFilterEnum.Ignore
					});
					row.AddChild(iconFrame);
				}
				row.AddChild(MakeLabel(meta.Name, new Color(0.98f, 0.96f, 0.90f, 0.96f), 19, expandFill: true, clip: true));
				backView.AddChild(row);
			}
		}

		// Thanh % mini cuối thẻ
		var miniBar = new ProgressBar
		{
			CustomMinimumSize = new Vector2(0, 8),
			MaxValue = 100,
			Value = 0,
			ShowPercentage = false,
			MouseFilter = MouseFilterEnum.Ignore
		};
		miniBar.AddThemeStyleboxOverride("background", new StyleBoxFlat
		{
			BgColor = new Color(0.08f, 0.09f, 0.16f, 0.85f),
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomRight = 4,
			CornerRadiusBottomLeft = 4
		});
		miniBar.AddThemeStyleboxOverride("fill", new StyleBoxFlat
		{
			BgColor = isBlueTeam ? new Color(0.24f, 0.78f, 0.64f, 1f) : new Color(0.94f, 0.44f, 0.40f, 1f),
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomRight = 4,
			CornerRadiusBottomLeft = 4
		});
		outerVBox.AddChild(miniBar);

		var model = new LoadingPlayerCard
		{
			RootBtn = btn,
			FrontView = frontView,
			BackView = backView,
			PercentLabel = pctLbl,
			MiniProgress = miniBar,
			IsSelf = isSelf,
			LoadSpeed = loadSpeed
		};

		btn.Pressed += () => FlipCard(model);

		parent?.AddChild(btn);
		_playerCards.Add(model);
		return model;
	}

	private static void FlipCard(LoadingPlayerCard card)
	{
		if (card?.RootBtn == null || !IsInstanceValid(card.RootBtn)) return;
		card.RootBtn.PivotOffset = card.RootBtn.Size * 0.5f;

		var tw = card.RootBtn.CreateTween();
		tw.TweenProperty(card.RootBtn, "scale:x", 0.04f, 0.11f)
			.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
		tw.TweenCallback(Callable.From(() =>
		{
			card.IsFlipped = !card.IsFlipped;
			if (card.FrontView != null) card.FrontView.Visible = !card.IsFlipped;
			if (card.BackView != null) card.BackView.Visible = card.IsFlipped;
		}));
		tw.TweenProperty(card.RootBtn, "scale:x", 1.0f, 0.14f)
			.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
	}

	private void BuildQuickEmoteBar()
	{
		if (_emoteBar == null) return;
		foreach (Node child in _emoteBar.GetChildren()) child.QueueFree();

		foreach (string path in QuickStickerPaths)
		{
			string stickerPath = path;
			var tex = GD.Load<Texture2D>(stickerPath);
			if (tex == null) continue;

			var btn = new Button
			{
				CustomMinimumSize = new Vector2(56, 56),
				FocusMode = FocusModeEnum.None,
				MouseDefaultCursorShape = CursorShape.PointingHand
			};
			var style = new StyleBoxFlat
			{
				BgColor = new Color(0.08f, 0.10f, 0.18f, 0.36f),
				CornerRadiusTopLeft = 28,
				CornerRadiusTopRight = 28,
				CornerRadiusBottomRight = 28,
				CornerRadiusBottomLeft = 28
			};
			btn.AddThemeStyleboxOverride("normal", style);
			btn.AddThemeStyleboxOverride("hover", style);
			btn.AddThemeStyleboxOverride("pressed", style);

			var icon = new TextureRect
			{
				Texture = tex,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				MouseFilter = MouseFilterEnum.Ignore
			};
			icon.SetAnchorsPreset(LayoutPreset.FullRect);
			icon.OffsetLeft = 6f;
			icon.OffsetTop = 6f;
			icon.OffsetRight = -6f;
			icon.OffsetBottom = -6f;
			btn.AddChild(icon);

			btn.Pressed += () =>
			{
				if (_selfCard != null) SpawnFloatingSticker(_selfCard.RootBtn, stickerPath);
			};

			_emoteBar.AddChild(btn);
			UIButtonHelper.AttachBounceEffect(btn, 0.90f);
		}
	}

	private void SpawnFloatingSticker(Control anchorCard, string stickerPath)
	{
		if (_emoteFloatLayer == null || anchorCard == null || !IsInstanceValid(anchorCard)) return;
		var tex = GD.Load<Texture2D>(stickerPath);
		if (tex == null) return;

		var rect = anchorCard.GetGlobalRect();
		var bubble = new TextureRect
		{
			Texture = tex,
			CustomMinimumSize = new Vector2(96, 96),
			Size = new Vector2(96, 96),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			PivotOffset = new Vector2(48, 48),
			Scale = new Vector2(0.4f, 0.4f),
			MouseFilter = MouseFilterEnum.Ignore
		};
		float offsetX = (float)GD.RandRange(-45.0, 45.0);
		bubble.GlobalPosition = new Vector2(rect.Position.X + rect.Size.X * 0.5f - 48f + offsetX, rect.Position.Y - 12f);
		_emoteFloatLayer.AddChild(bubble);

		var tw = bubble.CreateTween().SetParallel(true);
		tw.TweenProperty(bubble, "scale", new Vector2(1.08f, 1.08f), 0.22f)
			.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		tw.TweenProperty(bubble, "position:y", bubble.Position.Y - 95f, 1.15f)
			.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		tw.TweenProperty(bubble, "modulate:a", 0f, 0.42f).SetDelay(0.75f);
		tw.Chain().TweenCallback(Callable.From(() => bubble.QueueFree()));
	}

	private void CycleNextTip()
	{
		_currentTipIndex = (_currentTipIndex + 1) % MarketTips.Length;
		if (_tipBtn != null) _tipBtn.Text = MarketTips[_currentTipIndex];
	}

	private void PlayIntroAnimation()
	{
		if (_vsBadge != null)
		{
			_vsBadge.PivotOffset = _vsBadge.CustomMinimumSize * 0.5f;
			_vsBadge.Scale = new Vector2(0.3f, 0.3f);
			_vsBadge.Modulate = new Color(1, 1, 1, 0);
			var tw = _vsBadge.CreateTween().SetParallel(true);
			tw.TweenProperty(_vsBadge, "modulate:a", 1f, 0.28f).SetDelay(0.12f);
			tw.TweenProperty(_vsBadge, "scale", Vector2.One, 0.45f).SetDelay(0.12f)
				.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		}

		for (int i = 0; i < _playerCards.Count; i++)
		{
			var btn = _playerCards[i].RootBtn;
			if (btn == null) continue;
			btn.Modulate = new Color(1, 1, 1, 0);
			var tw = btn.CreateTween();
			tw.TweenProperty(btn, "modulate:a", 1f, 0.30f).SetDelay(0.06f * i);
		}
	}

	private static Label MakeLabel(string text, Color color, int fontSize, bool expandFill, bool clip)
	{
		var lbl = new Label
		{
			Text = text,
			SizeFlagsHorizontal = expandFill ? SizeFlags.ExpandFill : SizeFlags.ShrinkBegin,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			VerticalAlignment = VerticalAlignment.Center,
			ClipText = clip,
			MouseFilter = MouseFilterEnum.Ignore
		};
		if (OrcishFont != null) lbl.AddThemeFontOverride("font", OrcishFont);
		lbl.AddThemeFontSizeOverride("font_size", fontSize);
		lbl.AddThemeColorOverride("font_color", color);
		lbl.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.72f));
		lbl.AddThemeConstantOverride("outline_size", 4);
		return lbl;
	}

	private static StyleBoxFlat MakeMiniTileStyle(Color borderColor) => new()
	{
		BgColor = new Color(0.95f, 0.92f, 0.85f, 1f),
		BorderWidthLeft = 2,
		BorderWidthTop = 2,
		BorderWidthRight = 2,
		BorderWidthBottom = 2,
		BorderColor = borderColor,
		ContentMarginLeft = 3f,
		ContentMarginTop = 3f,
		ContentMarginRight = 3f,
		ContentMarginBottom = 3f,
		CornerRadiusTopLeft = 10,
		CornerRadiusTopRight = 10,
		CornerRadiusBottomRight = 10,
		CornerRadiusBottomLeft = 10
	};
}
