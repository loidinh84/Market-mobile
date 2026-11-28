using System.Collections.Generic;
using Godot;
using MarketMobile.Core;
using MarketMobile.Utils;

namespace MarketMobile.UI;

/// <summary>
/// Popup Xác nhận Trận đấu (Phần 2):
/// - Chạy hiệu ứng mở 2 cánh cửa từ giữa màn hình sang 2 bên bằng Tween.
/// - Hiển thị trạng thái sẵn sàng của người chơi, thanh đếm ngược 10s, nút Sẵn sàng / Từ chối.
/// - Phát signal AllPlayersReady để Phần 3 (chọn nhân vật & vật phẩm) tiếp nhận.
/// </summary>
public partial class MatchFoundOverlay : Control
{
	[Signal]
	public delegate void AllPlayersReadyEventHandler(int playType, int teamMode, string mapId);

	[Signal]
	public delegate void MatchDeclinedEventHandler(bool selfTimedOut);

	private sealed class PlayerSlotWidget
	{
		public PanelContainer Card;
		public Label StatusLabel;
		public bool IsReady;
		public float AutoReadyAt;
	}

	private const float AcceptDurationSeconds = 10f;

	private static readonly Font OrcishFont = GD.Load<Font>("res://assets/fonts/FZ-Orcish.ttf");
	private static readonly Texture2D TexAvatar = GD.Load<Texture2D>("res://assets/sprites/icons/avatar_character.png");

	private ColorRect _dimBackdrop;
	private PanelContainer _centerCard;
	private Label _subLabel;
	private HBoxContainer _playersHBox;
	private ProgressBar _timerProgress;
	private Label _timerLabel;
	private Button _readyBtn;
	private Control _doorLayer;
	private Panel _leftDoor;
	private Panel _rightDoor;

	private readonly List<PlayerSlotWidget> _slots = new();
	private PlayMatchType _playType;
	private TeamMode _teamMode;
	private string _mapId;
	private bool _isActive = false;
	private bool _selfReady = false;
	private float _remainingSeconds = AcceptDurationSeconds;
	private float _elapsed = 0f;
	private Tween _doorTween;

	public override void _Ready()
	{
		_dimBackdrop = GetNodeOrNull<ColorRect>("DimBackdrop");
		_centerCard = GetNodeOrNull<PanelContainer>("Center/CenterCard");
		_subLabel = GetNodeOrNull<Label>("Center/CenterCard/VBox/HeaderPanel/VBox/SubLabel");
		_playersHBox = GetNodeOrNull<HBoxContainer>("Center/CenterCard/VBox/BodyMargin/BodyVBox/PlayersHBox");
		_timerProgress = GetNodeOrNull<ProgressBar>("Center/CenterCard/VBox/BodyMargin/BodyVBox/TimerRow/TimerProgress");
		_timerLabel = GetNodeOrNull<Label>("Center/CenterCard/VBox/BodyMargin/BodyVBox/TimerRow/TimerLabel");
		_readyBtn = GetNodeOrNull<Button>("Center/CenterCard/VBox/BodyMargin/BodyVBox/ButtonsRow/ReadyBtn");
		_doorLayer = GetNodeOrNull<Control>("DoorLayer");
		_leftDoor = GetNodeOrNull<Panel>("DoorLayer/LeftDoor");
		_rightDoor = GetNodeOrNull<Panel>("DoorLayer/RightDoor");

		if (_readyBtn != null) _readyBtn.Pressed += OnReadyPressed;

		UIButtonHelper.AttachToAllButtons(this);
		Visible = false;
	}

	public override void _ExitTree()
	{
		_doorTween?.Kill();
	}

	public override void _Process(double delta)
	{
		if (!_isActive) return;

		float dt = (float)delta;
		_elapsed += dt;
		_remainingSeconds = Mathf.Max(0f, _remainingSeconds - dt);

		if (_timerProgress != null) _timerProgress.Value = _remainingSeconds;
		if (_timerLabel != null) _timerLabel.Text = $"{Mathf.CeilToInt(_remainingSeconds)}s";

		bool allReady = true;
		for (int i = 0; i < _slots.Count; i++)
		{
			var slot = _slots[i];
			if (i > 0 && !slot.IsReady && _elapsed >= slot.AutoReadyAt)
			{
				SetSlotReady(slot, true);
			}
			if (!slot.IsReady) allReady = false;
		}

		if (allReady)
		{
			FinishAllReady();
			return;
		}

		if (_remainingSeconds <= 0f)
		{
			HandleTimeoutDecline();
		}
	}

	public void OpenForMatch(PlayMatchType playType, TeamMode teamMode, string mapId)
	{
		_playType = playType;
		_teamMode = teamMode;
		_mapId = string.IsNullOrEmpty(mapId) ? "map_cho_dau_moi" : mapId;
		_remainingSeconds = AcceptDurationSeconds;
		_elapsed = 0f;
		_selfReady = false;
		_isActive = true;

		var map = GameCatalog.FindMap(_mapId);
		if (_subLabel != null)
		{
			_subLabel.Text = $"{GameCatalog.GetPlayTypeShortLabel(_playType)} · {GameCatalog.GetTeamModeLabel(_teamMode)} · {map?.Name}";
		}

		if (_timerProgress != null)
		{
			_timerProgress.MaxValue = AcceptDurationSeconds;
			_timerProgress.Value = AcceptDurationSeconds;
		}
		if (_timerLabel != null) _timerLabel.Text = $"{Mathf.CeilToInt(AcceptDurationSeconds)}s";

		if (_readyBtn != null)
		{
			_readyBtn.Disabled = false;
			_readyBtn.Text = "Sẵn sàng";
			_readyBtn.Modulate = Colors.White;
		}

		BuildPlayerSlots();

		Visible = true;
		CallDeferred(CanvasItem.MethodName.MoveToFront);
		PlayDoorOpenAnimation();
	}

	private void PlayDoorOpenAnimation()
	{
		_doorTween?.Kill();

		if (_dimBackdrop != null) _dimBackdrop.Modulate = new Color(1, 1, 1, 0);
		if (_centerCard != null)
		{
			_centerCard.PivotOffset = _centerCard.CustomMinimumSize * 0.5f;
			_centerCard.Scale = new Vector2(0.86f, 0.86f);
			_centerCard.Modulate = new Color(1, 1, 1, 0);
		}
		if (_doorLayer != null) _doorLayer.Visible = true;
		if (_leftDoor != null)
		{
			_leftDoor.AnchorLeft = 0f;
			_leftDoor.AnchorRight = 0.5f;
		}
		if (_rightDoor != null)
		{
			_rightDoor.AnchorLeft = 0.5f;
			_rightDoor.AnchorRight = 1.0f;
		}

		_doorTween = CreateTween().SetParallel(true);
		if (_dimBackdrop != null)
		{
			_doorTween.TweenProperty(_dimBackdrop, "modulate:a", 1f, 0.36f);
		}
		if (_leftDoor != null)
		{
			_doorTween.TweenProperty(_leftDoor, "anchor_left", -0.55f, 0.85f)
				.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
			_doorTween.TweenProperty(_leftDoor, "anchor_right", -0.05f, 0.85f)
				.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		}
		if (_rightDoor != null)
		{
			_doorTween.TweenProperty(_rightDoor, "anchor_left", 1.05f, 0.85f)
				.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
			_doorTween.TweenProperty(_rightDoor, "anchor_right", 1.55f, 0.85f)
				.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		}
		if (_centerCard != null)
		{
			_doorTween.TweenProperty(_centerCard, "modulate:a", 1f, 0.38f).SetDelay(0.24f);
			_doorTween.TweenProperty(_centerCard, "scale", Vector2.One, 0.52f).SetDelay(0.24f)
				.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		}
	}

	private void BuildPlayerSlots()
	{
		_slots.Clear();
		if (_playersHBox == null) return;
		foreach (Node child in _playersHBox.GetChildren())
		{
			child.QueueFree();
		}

		int totalPlayers = _teamMode == TeamMode.Duo2v2 ? 4 : 2;
		string[] demoNames = _playType == PlayMatchType.Bot
			? new[] { "Bạn", "Bot #1", "Bot #2", "Bot #3" }
			: new[] { "Bạn", "Đối thủ 1", "Đồng đội", "Đối thủ 2" };

		for (int i = 0; i < totalPlayers; i++)
		{
			var card = new PanelContainer
			{
				CustomMinimumSize = new Vector2(totalPlayers == 4 ? 168 : 230, 175),
				SizeFlagsVertical = SizeFlags.ShrinkCenter
			};
			card.AddThemeStyleboxOverride("panel", MakeSlotStyle(false));

			var margin = new MarginContainer();
			margin.AddThemeConstantOverride("margin_left", 12);
			margin.AddThemeConstantOverride("margin_top", 14);
			margin.AddThemeConstantOverride("margin_right", 12);
			margin.AddThemeConstantOverride("margin_bottom", 12);
			card.AddChild(margin);

			var vbox = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
			vbox.AddThemeConstantOverride("separation", 8);
			margin.AddChild(vbox);

			if (TexAvatar != null)
			{
				vbox.AddChild(new TextureRect
				{
					Texture = TexAvatar,
					CustomMinimumSize = new Vector2(68, 68),
					SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
					ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
					StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
					MouseFilter = MouseFilterEnum.Ignore
				});
			}

			var nameLbl = new Label
			{
				Text = i < demoNames.Length ? demoNames[i] : $"Người chơi {i + 1}",
				HorizontalAlignment = HorizontalAlignment.Center,
				ClipText = true
			};
			if (OrcishFont != null) nameLbl.AddThemeFontOverride("font", OrcishFont);
			nameLbl.AddThemeFontSizeOverride("font_size", 24);
			nameLbl.AddThemeColorOverride("font_color", new Color(0.22f, 0.16f, 0.12f, 1f));
			vbox.AddChild(nameLbl);

			var statusLbl = new Label
			{
				Text = "Đang chờ...",
				HorizontalAlignment = HorizontalAlignment.Center
			};
			if (OrcishFont != null) statusLbl.AddThemeFontOverride("font", OrcishFont);
			statusLbl.AddThemeFontSizeOverride("font_size", 22);
			statusLbl.AddThemeColorOverride("font_color", new Color(0.56f, 0.48f, 0.40f, 1f));
			vbox.AddChild(statusLbl);

			_playersHBox.AddChild(card);

			_slots.Add(new PlayerSlotWidget
			{
				Card = card,
				StatusLabel = statusLbl,
				IsReady = false,
				AutoReadyAt = _playType == PlayMatchType.Bot ? 0.35f + i * 0.25f : 0.7f + i * 0.55f
			});
		}
	}

	private void OnReadyPressed()
	{
		if (!_isActive || _selfReady) return;
		_selfReady = true;

		if (_slots.Count > 0) SetSlotReady(_slots[0], true);

		if (_readyBtn != null)
		{
			_readyBtn.Disabled = true;
			_readyBtn.Text = "Đã sẵn sàng";
			_readyBtn.Modulate = new Color(0.86f, 0.96f, 0.90f, 0.9f);
		}
	}

	private void HandleTimeoutDecline()
	{
		if (!_isActive) return;
		_isActive = false;
		bool selfTimedOut = !_selfReady;
		CloseAnimated(() => EmitSignal(SignalName.MatchDeclined, selfTimedOut));
	}

	private void FinishAllReady()
	{
		if (!_isActive) return;
		_isActive = false;
		CloseAnimated(() => EmitSignal(SignalName.AllPlayersReady, (int)_playType, (int)_teamMode, _mapId));
	}

	private void CloseAnimated(System.Action onClosed)
	{
		_doorTween?.Kill();
		_doorTween = CreateTween().SetParallel(true);
		if (_dimBackdrop != null) _doorTween.TweenProperty(_dimBackdrop, "modulate:a", 0f, 0.18f);
		if (_centerCard != null)
		{
			_doorTween.TweenProperty(_centerCard, "modulate:a", 0f, 0.16f);
			_doorTween.TweenProperty(_centerCard, "scale", new Vector2(0.92f, 0.92f), 0.18f);
		}
		_doorTween.Chain().TweenCallback(Callable.From(() =>
		{
			Visible = false;
			onClosed?.Invoke();
		}));
	}

	private static void SetSlotReady(PlayerSlotWidget slot, bool ready)
	{
		slot.IsReady = ready;
		if (slot.Card != null) slot.Card.AddThemeStyleboxOverride("panel", MakeSlotStyle(ready));
		if (slot.StatusLabel != null)
		{
			slot.StatusLabel.Text = ready ? "Sẵn sàng!" : "Đang chờ...";
			slot.StatusLabel.AddThemeColorOverride("font_color", ready
				? new Color(0.12f, 0.56f, 0.34f, 1f)
				: new Color(0.56f, 0.48f, 0.40f, 1f));
		}
	}

	private static StyleBoxFlat MakeSlotStyle(bool ready)
	{
		return new StyleBoxFlat
		{
			BgColor = ready ? new Color(0.89f, 0.98f, 0.92f, 1f) : new Color(0.99f, 0.97f, 0.93f, 1f),
			BorderWidthLeft = 3,
			BorderWidthTop = 3,
			BorderWidthRight = 3,
			BorderWidthBottom = 3,
			BorderColor = ready ? new Color(0.16f, 0.65f, 0.42f, 1f) : new Color(0.76f, 0.70f, 0.60f, 0.85f),
			CornerRadiusTopLeft = 14,
			CornerRadiusTopRight = 14,
			CornerRadiusBottomRight = 14,
			CornerRadiusBottomLeft = 14
		};
	}
}
