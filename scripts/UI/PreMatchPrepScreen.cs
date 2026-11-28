using System.Collections.Generic;
using Godot;
using MarketMobile.Core;
using MarketMobile.Gameplay;
using MarketMobile.Utils;

namespace MarketMobile.UI;

/// <summary>
/// Màn hình Chuẩn bị trước trận (PvZ-inspired, gọn tối đa cho mobile):
/// - Nền hiển thị trực tiếp bản đồ (map) đã chọn.
/// - Cột trái: 5 ô nhân vật (3 ô trống mở sẵn với +, 2 ô khóa).
/// - Giữa trên: Kho chuẩn bị thu gọn nằm chính giữa, chia 2 bên Nhân vật & Vật phẩm (thẻ chỉ gồm ảnh + tên).
/// - Giữa dưới: Bảng Đồng đội rõ đầy đủ thông tin (Avatar, tên, hạng/cấp, trạng thái khóa,
///   các tướng kèm tên và các vật phẩm kèm tên đồng đội đang chọn).
/// - Cột phải: 4 ô vật phẩm (2 ô trống mở sẵn với +, 2 ô khóa).
/// - Kéo thả hiển thị Card nổi phía trên ngón tay, phát sáng ô hợp lệ, nảy mượt khi thả.
/// - Đếm ngược 60s: khóa hết thì vào trận ngay; hết giờ chưa chọn tự chọn tướng gần nhất.
/// </summary>
public partial class PreMatchPrepScreen : Control
{
	private const float PrepDurationSeconds = 60f;
	private const int TotalCharSlots = 5;
	private const int UnlockedCharSlots = 3;
	private const int TotalItemSlots = 4;
	private const int UnlockedItemSlots = 2;
	private const int MinWarehouseCharCells = 6;

	private static readonly Font OrcishFont = GD.Load<Font>("res://assets/fonts/FZ-Orcish.ttf");
	private static readonly Texture2D TexAvatar = GD.Load<Texture2D>("res://assets/sprites/icons/avatar_character.png");
	private static readonly Texture2D TexLock = GD.Load<Texture2D>("res://assets/sprites/icons/locked.png");
	private static readonly Texture2D TexCheck = GD.Load<Texture2D>("res://assets/sprites/icons/check.svg");
	private static readonly Texture2D TexCloseX = GD.Load<Texture2D>("res://assets/sprites/icons/close_x.svg");

	private TextureRect _mapBgFallback;
	private SubViewportContainer _mapBgViewportContainer;
	private SubViewport _mapBgViewport;
	private Label _subLabel;
	private Label _timerLabel;
	private VBoxContainer _charSlotsVBox;
	private PanelContainer _centerWarehouse;
	private ScrollContainer _charsScroll;
	private GridContainer _charsGrid;
	private ScrollContainer _itemsScroll;
	private GridContainer _itemsGrid;
	private VBoxContainer _itemSlotsVBox;
	private PanelContainer _teammatePanel;
	private HBoxContainer _teammatesHBox;
	private Label _teamStatusLabel;
	private Button _lockInBtn;

	private PlayerDataManager _data;
	private PlayMatchType _playType = PlayMatchType.Normal;
	private TeamMode _teamMode = TeamMode.Solo1v1;
	private string _mapId = "map_cho_dau_moi";

	private readonly int[] _charSlots = new int[UnlockedCharSlots];
	private readonly string[] _itemSlots = new string[UnlockedItemSlots];
	private readonly List<Button> _charSlotButtons = new();
	private readonly List<Button> _itemSlotButtons = new();

	// Trạng thái hiệu ứng kéo thả & nảy
	private string _activeDragKind;
	private Control _activeDragSource;
	private string _hoveredSlotKind;
	private int _hoveredSlotIndex = -1;
	private string _justPlacedSlotKind;
	private int _justPlacedSlotIndex = -1;

	private bool _isActive = false;
	private bool _selfLocked = false;
	private bool _othersLocked = false;
	private int _teammateStage = 0; // 0: đang chuẩn bị, 1: 1 tướng, 2: 2 tướng + 1 vật phẩm, 3: đủ & đã khóa
	private float _remainingSeconds = PrepDurationSeconds;
	private float _elapsed = 0f;
	private float _othersLockAt = 4.2f;

	public override void _Ready()
	{
		_mapBgFallback = GetNodeOrNull<TextureRect>("MapBgFallback");
		_mapBgViewportContainer = GetNodeOrNull<SubViewportContainer>("MapBgViewportContainer");
		_mapBgViewport = GetNodeOrNull<SubViewport>("MapBgViewportContainer/MapBgViewport");
		_subLabel = GetNodeOrNull<Label>("MainVBox/HeaderPanel/HBox/SubLabel");
		_timerLabel = GetNodeOrNull<Label>("MainVBox/HeaderPanel/HBox/TimerLabel");
		_charSlotsVBox = GetNodeOrNull<VBoxContainer>("MainVBox/BodyMargin/ThreeColsHBox/LeftSlotsPanel/VBox/CharSlotsVBox");
		_centerWarehouse = GetNodeOrNull<PanelContainer>("MainVBox/BodyMargin/ThreeColsHBox/CenterHost/CenterVBox/CenterWarehouse");
		_charsScroll = GetNodeOrNull<ScrollContainer>("MainVBox/BodyMargin/ThreeColsHBox/CenterHost/CenterVBox/CenterWarehouse/InnerBoard/SplitHBox/CharsCol/CharsScroll");
		_charsGrid = GetNodeOrNull<GridContainer>("MainVBox/BodyMargin/ThreeColsHBox/CenterHost/CenterVBox/CenterWarehouse/InnerBoard/SplitHBox/CharsCol/CharsScroll/CharsGrid");
		_itemsScroll = GetNodeOrNull<ScrollContainer>("MainVBox/BodyMargin/ThreeColsHBox/CenterHost/CenterVBox/CenterWarehouse/InnerBoard/SplitHBox/ItemsCol/ItemsScroll");
		_itemsGrid = GetNodeOrNull<GridContainer>("MainVBox/BodyMargin/ThreeColsHBox/CenterHost/CenterVBox/CenterWarehouse/InnerBoard/SplitHBox/ItemsCol/ItemsScroll/ItemsGrid");
		_itemSlotsVBox = GetNodeOrNull<VBoxContainer>("MainVBox/BodyMargin/ThreeColsHBox/RightSlotsPanel/VBox/ItemSlotsVBox");
		_teammatePanel = GetNodeOrNull<PanelContainer>("MainVBox/BodyMargin/ThreeColsHBox/CenterHost/CenterVBox/TeammatePanel");
		_teammatesHBox = GetNodeOrNull<HBoxContainer>("MainVBox/BodyMargin/ThreeColsHBox/CenterHost/CenterVBox/TeammatePanel/VBox/TeammatesHBox");
		_teamStatusLabel = GetNodeOrNull<Label>("MainVBox/FooterPanel/FooterRow/TeamStatusLabel");
		_lockInBtn = GetNodeOrNull<Button>("MainVBox/FooterPanel/FooterRow/LockInBtn");

		if (_lockInBtn != null) _lockInBtn.Pressed += OnLockInPressed;

		// Cho phép kéo thẻ từ ô 2 bên trả lại bất kỳ vùng nào trong kho giữa để gỡ ra
		AttachWarehouseDropTarget(_centerWarehouse);
		AttachWarehouseDropTarget(GetNodeOrNull<Control>("MainVBox/BodyMargin/ThreeColsHBox/CenterHost/CenterVBox/CenterWarehouse/InnerBoard"));
		AttachWarehouseDropTarget(_charsScroll);
		AttachWarehouseDropTarget(_itemsScroll);

		_data = PlayerDataManager.Instance ?? GetNodeOrNull<PlayerDataManager>("/root/PlayerDataManager");
		if (_data != null)
		{
			_playType = _data.SelectedPlayType;
			_teamMode = _data.SelectedTeamMode;
			_mapId = _data.SelectedMapId;
		}

		SetupMapBackground();
		InitializePrep();
		UIButtonHelper.AttachBounceEffect(_lockInBtn, 0.96f);
	}

	public override void _Notification(int what)
	{
		if (what == NotificationDragEnd)
		{
			EndActiveDragVisuals();
		}
	}

	public override void _Process(double delta)
	{
		if (!_isActive) return;

		float dt = (float)delta;
		_elapsed += dt;
		_remainingSeconds = Mathf.Max(0f, _remainingSeconds - dt);

		if (_timerLabel != null)
		{
			int sec = Mathf.CeilToInt(_remainingSeconds);
			_timerLabel.Text = $"{sec}s";
			_timerLabel.AddThemeColorOverride("font_color", sec <= 10
				? new Color(1f, 0.38f, 0.34f, 1f)
				: new Color(1f, 0.88f, 0.40f, 1f));
		}

		int nextStage = _elapsed >= _othersLockAt ? 3 : (_elapsed >= 2.4f ? 2 : (_elapsed >= 1.1f ? 1 : 0));
		if (nextStage != _teammateStage)
		{
			_teammateStage = nextStage;
			if (_teammateStage >= 3) _othersLocked = true;
			RebuildTeammatesPanel();
			UpdateTeamStatusLabel();
		}

		if (_selfLocked && _othersLocked)
		{
			LaunchMatch();
			return;
		}

		if (_remainingSeconds <= 0f)
		{
			// Hết 60s chưa chọn nhân vật nào -> tự động chọn tướng gần nhất của tài khoản
			if (!HasAnySelectedCharacter())
			{
				_charSlots[0] = GetFallbackAccountCharacterIndex();
			}
			_selfLocked = true;
			LaunchMatch();
		}
	}

	private void SetupMapBackground()
	{
		var map = GameCatalog.FindMap(_mapId);
		if (_mapBgFallback != null && map != null && !string.IsNullOrEmpty(map.TexturePath))
		{
			var fallbackTex = GD.Load<Texture2D>(map.TexturePath);
			if (fallbackTex != null) _mapBgFallback.Texture = fallbackTex;
			_mapBgFallback.Modulate = map.BannerTint;
		}

		if (_mapBgViewport == null || map == null || string.IsNullOrEmpty(map.ScenePath))
		{
			if (_mapBgViewportContainer != null) _mapBgViewportContainer.Visible = false;
			return;
		}

		var packedMap = GD.Load<PackedScene>(map.ScenePath);
		if (packedMap == null)
		{
			if (_mapBgViewportContainer != null) _mapBgViewportContainer.Visible = false;
			return;
		}

		var mapInstance = packedMap.Instantiate();
		if (mapInstance == null)
		{
			if (_mapBgViewportContainer != null) _mapBgViewportContainer.Visible = false;
			return;
		}

		// Cấu hình camera bản đồ nền căn giữa map và tắt input kéo camera
		ConfigureBackgroundPreviewCamera(mapInstance);
		_mapBgViewport.AddChild(mapInstance);
		if (_mapBgViewportContainer != null)
		{
			_mapBgViewportContainer.Modulate = map.BannerTint;
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

	private void InitializePrep()
	{
		for (int i = 0; i < _charSlots.Length; i++) _charSlots[i] = -1;
		for (int i = 0; i < _itemSlots.Length; i++) _itemSlots[i] = null;

		_remainingSeconds = PrepDurationSeconds;
		_elapsed = 0f;
		_selfLocked = false;
		_othersLocked = false;
		_teammateStage = 0;
		_othersLockAt = _playType == PlayMatchType.Bot ? 2.6f : 4.2f;
		_isActive = true;

		var map = GameCatalog.FindMap(_mapId);
		if (_subLabel != null)
		{
			_subLabel.Text = $"· {GameCatalog.GetPlayTypeShortLabel(_playType)} · {GameCatalog.GetTeamModeLabel(_teamMode)} · {map?.Name}";
		}
		if (_timerLabel != null)
		{
			_timerLabel.Text = $"{Mathf.CeilToInt(PrepDurationSeconds)}s";
			_timerLabel.AddThemeColorOverride("font_color", new Color(1f, 0.88f, 0.40f, 1f));
		}

		RefreshAllPrepVisuals();
		RebuildTeammatesPanel();
		UpdateTeamStatusLabel();
	}

	private int GetFallbackAccountCharacterIndex()
	{
		if (_data == null || _data.Characters == null || _data.Characters.Length == 0) return 0;
		int idx = _data.SelectedCharacterIndex;
		if (idx >= 0 && idx < _data.Characters.Length && _data.Characters[idx].IsUnlocked) return idx;
		return 0;
	}

	private bool HasAnySelectedCharacter()
	{
		for (int i = 0; i < _charSlots.Length; i++)
		{
			if (_charSlots[i] >= 0) return true;
		}
		return false;
	}

	private bool IsCharacterInAnySlot(int charIndex)
	{
		for (int i = 0; i < _charSlots.Length; i++)
		{
			if (_charSlots[i] == charIndex) return true;
		}
		return false;
	}

	private bool IsItemInAnySlot(string itemId)
	{
		for (int i = 0; i < _itemSlots.Length; i++)
		{
			if (_itemSlots[i] == itemId) return true;
		}
		return false;
	}

	private void RebuildCharSlots()
	{
		_charSlotButtons.Clear();
		if (_charSlotsVBox == null) return;
		foreach (Node child in _charSlotsVBox.GetChildren()) child.QueueFree();

		var chars = _data?.Characters ?? PlayerDataManager.CreateDefaultCharacters();
		for (int i = 0; i < TotalCharSlots; i++)
		{
			int slotIdx = i;
			bool isLockedSlot = i >= UnlockedCharSlots;
			int assignedChar = !isLockedSlot ? _charSlots[slotIdx] : -1;

			var slotBtn = new Button
			{
				SizeFlagsVertical = SizeFlags.ExpandFill,
				FocusMode = FocusModeEnum.None,
				Disabled = isLockedSlot || _selfLocked,
				MouseDefaultCursorShape = (!isLockedSlot && assignedChar >= 0) ? CursorShape.PointingHand : CursorShape.Arrow
			};
			ApplySlotStyle(slotBtn, assignedChar >= 0, isLockedSlot, highlighted: false, hovered: false);

			if (!isLockedSlot && !_selfLocked)
			{
				slotBtn.SetDragForwarding(
					Callable.From<Vector2, Variant>(_ => StartSlotCharDrag(slotBtn, slotIdx)),
					Callable.From<Vector2, Variant, bool>((_, data) => CheckCanDropOnSlot("char", slotIdx, data)),
					Callable.From<Vector2, Variant>((_, data) => OnCharDroppedToSlot(slotIdx, data))
				);
				slotBtn.MouseExited += () => ClearHoveredSlot("char", slotIdx);
			}

			if (isLockedSlot)
			{
				AddCenteredLockIcon(slotBtn, 30);
			}
			else if (assignedChar >= 0 && assignedChar < chars.Length)
			{
				var ch = chars[assignedChar];
				BuildCompactSlotContent(slotBtn, TexAvatar, ch.Tint, ch.Name, isCharacter: true);

				slotBtn.Pressed += () =>
				{
					if (_selfLocked) return;
					_charSlots[slotIdx] = -1;
					RefreshAllPrepVisuals();
				};
			}
			else
			{
				AddEmptyPlusGlyph(slotBtn);
			}

			_charSlotsVBox.AddChild(slotBtn);
			_charSlotButtons.Add(slotBtn);

			if (_justPlacedSlotKind == "char" && _justPlacedSlotIndex == slotIdx)
			{
				PlaySlotSnapBounce(slotBtn);
			}
		}
	}

	private void RebuildItemSlots()
	{
		_itemSlotButtons.Clear();
		if (_itemSlotsVBox == null) return;
		foreach (Node child in _itemSlotsVBox.GetChildren()) child.QueueFree();

		for (int i = 0; i < TotalItemSlots; i++)
		{
			int slotIdx = i;
			bool isLockedSlot = i >= UnlockedItemSlots;
			string assignedItemId = !isLockedSlot ? _itemSlots[slotIdx] : null;
			var meta = !string.IsNullOrEmpty(assignedItemId) ? GameCatalog.FindConsumable(assignedItemId) : null;

			var slotBtn = new Button
			{
				SizeFlagsVertical = SizeFlags.ExpandFill,
				FocusMode = FocusModeEnum.None,
				Disabled = isLockedSlot || _selfLocked,
				MouseDefaultCursorShape = (!isLockedSlot && meta != null) ? CursorShape.PointingHand : CursorShape.Arrow
			};
			ApplySlotStyle(slotBtn, meta != null, isLockedSlot, highlighted: false, hovered: false);

			if (!isLockedSlot && !_selfLocked)
			{
				slotBtn.SetDragForwarding(
					Callable.From<Vector2, Variant>(_ => StartSlotItemDrag(slotBtn, slotIdx)),
					Callable.From<Vector2, Variant, bool>((_, data) => CheckCanDropOnSlot("item", slotIdx, data)),
					Callable.From<Vector2, Variant>((_, data) => OnItemDroppedToSlot(slotIdx, data))
				);
				slotBtn.MouseExited += () => ClearHoveredSlot("item", slotIdx);
			}

			if (isLockedSlot)
			{
				AddCenteredLockIcon(slotBtn, 30);
			}
			else if (meta != null)
			{
				Texture2D tex = !string.IsNullOrEmpty(meta.IconPath) ? GD.Load<Texture2D>(meta.IconPath) : null;
				BuildCompactSlotContent(slotBtn, tex, meta.IconTint, meta.Name, isCharacter: false);

				slotBtn.Pressed += () =>
				{
					if (_selfLocked) return;
					_itemSlots[slotIdx] = null;
					RefreshAllPrepVisuals();
				};
			}
			else
			{
				AddEmptyPlusGlyph(slotBtn);
			}

			_itemSlotsVBox.AddChild(slotBtn);
			_itemSlotButtons.Add(slotBtn);

			if (_justPlacedSlotKind == "item" && _justPlacedSlotIndex == slotIdx)
			{
				PlaySlotSnapBounce(slotBtn);
			}
		}
	}

	private void BuildCompactSlotContent(Button slotBtn, Texture2D iconTex, Color tint, string nameText, bool isCharacter)
	{
		var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
		margin.SetAnchorsPreset(LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", 8);
		margin.AddThemeConstantOverride("margin_top", 6);
		margin.AddThemeConstantOverride("margin_right", 8);
		margin.AddThemeConstantOverride("margin_bottom", 6);
		slotBtn.AddChild(margin);

		var vbox = new VBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center,
			MouseFilter = MouseFilterEnum.Ignore
		};
		vbox.AddThemeConstantOverride("separation", 4);
		margin.AddChild(vbox);

		var iconBox = new PanelContainer
		{
			CustomMinimumSize = new Vector2(54, 54),
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
			MouseFilter = MouseFilterEnum.Ignore
		};
		iconBox.AddThemeStyleboxOverride("panel", MakeMiniTileStyle(isCharacter
			? new Color(0.24f, 0.56f, 0.78f, 1f)
			: new Color(0.84f, 0.60f, 0.24f, 1f)));
		if (iconTex != null)
		{
			iconBox.AddChild(new TextureRect
			{
				Texture = iconTex,
				Modulate = tint,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				MouseFilter = MouseFilterEnum.Ignore
			});
		}
		vbox.AddChild(iconBox);

		var nameLbl = MakeTextLabel(nameText, new Color(0.18f, 0.13f, 0.10f, 1f), 17, expandFill: true, clip: true);
		nameLbl.HorizontalAlignment = HorizontalAlignment.Center;
		vbox.AddChild(nameLbl);

		if (!_selfLocked && TexCloseX != null)
		{
			var closeBadge = new TextureRect
			{
				Texture = TexCloseX,
				Modulate = new Color(0.55f, 0.42f, 0.35f, 0.72f),
				CustomMinimumSize = new Vector2(16, 16),
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				MouseFilter = MouseFilterEnum.Ignore
			};
			closeBadge.SetAnchorsPreset(LayoutPreset.TopRight);
			closeBadge.OffsetLeft = -22f;
			closeBadge.OffsetTop = 6f;
			closeBadge.OffsetRight = -6f;
			closeBadge.OffsetBottom = 22f;
			slotBtn.AddChild(closeBadge);
		}
	}

	private static void AddEmptyPlusGlyph(Control host)
	{
		var plus = new Label
		{
			Text = "+",
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore
		};
		plus.SetAnchorsPreset(LayoutPreset.FullRect);
		if (OrcishFont != null) plus.AddThemeFontOverride("font", OrcishFont);
		plus.AddThemeFontSizeOverride("font_size", 32);
		plus.AddThemeColorOverride("font_color", new Color(0.68f, 0.66f, 0.86f, 0.58f));
		host.AddChild(plus);
	}

	private static void AddCenteredLockIcon(Control host, float size)
	{
		if (TexLock == null) return;
		var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
		center.SetAnchorsPreset(LayoutPreset.FullRect);
		host.AddChild(center);

		center.AddChild(new TextureRect
		{
			Texture = TexLock,
			Modulate = new Color(0.76f, 0.74f, 0.88f, 0.58f),
			CustomMinimumSize = new Vector2(size, size),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			MouseFilter = MouseFilterEnum.Ignore
		});
	}

	private void RebuildWarehouseCharacters()
	{
		if (_charsGrid == null) return;
		foreach (Node child in _charsGrid.GetChildren()) child.QueueFree();

		var chars = _data?.Characters ?? PlayerDataManager.CreateDefaultCharacters();
		if (_charsScroll != null)
		{
			_charsScroll.VerticalScrollMode = chars.Length > 6
				? ScrollContainer.ScrollMode.ShowNever
				: ScrollContainer.ScrollMode.Disabled;
		}

		for (int i = 0; i < chars.Length; i++)
		{
			int idx = i;
			var ch = chars[i];
			bool equipped = IsCharacterInAnySlot(idx);
			bool usable = ch.IsUnlocked && !equipped && !_selfLocked;

			var card = new Button
			{
				CustomMinimumSize = new Vector2(0, 134),
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				FocusMode = FocusModeEnum.None,
				Disabled = !ch.IsUnlocked || _selfLocked,
				MouseDefaultCursorShape = usable ? CursorShape.PointingHand : CursorShape.Arrow
			};
			ApplyWarehouseCardStyle(card, equipped, !ch.IsUnlocked, isCharacter: true);

			card.SetDragForwarding(
				usable
					? Callable.From<Vector2, Variant>(_ => BeginCardDrag(
						card, "char", idx, string.Empty, -1,
						TexAvatar, ch.Tint, ch.Name, isCharacter: true))
					: Callable.From<Vector2, Variant>(_ => default),
				Callable.From<Vector2, Variant, bool>((_, data) => CanDropBackToWarehouse(data)),
				Callable.From<Vector2, Variant>((_, data) => OnDropBackToWarehouse(data))
			);

			BuildMinimalCardContent(card, TexAvatar, ch.Tint, ch.Name, null, equipped, !ch.IsUnlocked, isCharacter: true);

			card.Pressed += () =>
			{
				if (_selfLocked || !ch.IsUnlocked) return;
				if (IsCharacterInAnySlot(idx))
				{
					RemoveCharFromSlots(idx);
				}
				else
				{
					int placed = PlaceCharInFirstEmptySlot(idx);
					if (placed >= 0) MarkJustPlaced("char", placed);
				}
				RefreshAllPrepVisuals();
			};

			_charsGrid.AddChild(card);
		}

		for (int pad = chars.Length; pad < MinWarehouseCharCells; pad++)
		{
			var emptyCell = new PanelContainer
			{
				CustomMinimumSize = new Vector2(0, 134),
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				MouseFilter = MouseFilterEnum.Ignore
			};
			emptyCell.AddThemeStyleboxOverride("panel", MakeEmptyWarehouseCellStyle());
			_charsGrid.AddChild(emptyCell);
		}
	}

	private void RebuildWarehouseItems()
	{
		if (_itemsGrid == null) return;
		foreach (Node child in _itemsGrid.GetChildren()) child.QueueFree();

		int visibleItems = 0;
		foreach (var item in GameCatalog.Consumables)
		{
			if (item.Id == PlayerDataManager.RenameCardItemId) continue;
			visibleItems++;

			string itemId = item.Id;
			int owned = _data != null ? _data.GetItemQuantity(itemId) : 0;
			bool equipped = IsItemInAnySlot(itemId);
			bool usable = !equipped && !_selfLocked;
			Texture2D tex = !string.IsNullOrEmpty(item.IconPath) ? GD.Load<Texture2D>(item.IconPath) : null;

			var card = new Button
			{
				CustomMinimumSize = new Vector2(0, 134),
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				FocusMode = FocusModeEnum.None,
				Disabled = _selfLocked,
				MouseDefaultCursorShape = usable ? CursorShape.PointingHand : CursorShape.Arrow
			};
			ApplyWarehouseCardStyle(card, equipped, false, isCharacter: false);

			card.SetDragForwarding(
				usable
					? Callable.From<Vector2, Variant>(_ => BeginCardDrag(
						card, "item", -1, itemId, -1,
						tex, item.IconTint, item.Name, isCharacter: false))
					: Callable.From<Vector2, Variant>(_ => default),
				Callable.From<Vector2, Variant, bool>((_, data) => CanDropBackToWarehouse(data)),
				Callable.From<Vector2, Variant>((_, data) => OnDropBackToWarehouse(data))
			);

			string qtyBadge = owned > 0 ? $"x{owned}" : null;
			BuildMinimalCardContent(card, tex, item.IconTint, item.Name, qtyBadge, equipped, false, isCharacter: false);

			card.Pressed += () =>
			{
				if (_selfLocked) return;
				if (IsItemInAnySlot(itemId))
				{
					RemoveItemFromSlots(itemId);
				}
				else
				{
					int placed = PlaceItemInFirstEmptySlot(itemId);
					if (placed >= 0) MarkJustPlaced("item", placed);
				}
				RefreshAllPrepVisuals();
			};

			_itemsGrid.AddChild(card);
		}

		if (_itemsScroll != null)
		{
			_itemsScroll.VerticalScrollMode = visibleItems > 6
				? ScrollContainer.ScrollMode.ShowNever
				: ScrollContainer.ScrollMode.Disabled;
		}
	}

	private static void BuildMinimalCardContent(
		Control host,
		Texture2D iconTex,
		Color iconTint,
		string title,
		string qtyBadge,
		bool equipped,
		bool locked,
		bool isCharacter)
	{
		var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
		margin.SetAnchorsPreset(LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", 6);
		margin.AddThemeConstantOverride("margin_top", 6);
		margin.AddThemeConstantOverride("margin_right", 6);
		margin.AddThemeConstantOverride("margin_bottom", 6);
		host.AddChild(margin);

		var vbox = new VBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center,
			MouseFilter = MouseFilterEnum.Ignore
		};
		vbox.AddThemeConstantOverride("separation", 4);
		margin.AddChild(vbox);

		var portraitBox = new PanelContainer
		{
			CustomMinimumSize = new Vector2(66, 66),
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
			MouseFilter = MouseFilterEnum.Ignore
		};
		Color frameBorder = locked
			? new Color(0.55f, 0.52f, 0.48f, 0.65f)
			: (isCharacter ? new Color(0.28f, 0.44f, 0.72f, 0.9f) : new Color(0.84f, 0.58f, 0.22f, 0.9f));
		portraitBox.AddThemeStyleboxOverride("panel", MakeMiniTileStyle(frameBorder));

		if (iconTex != null)
		{
			portraitBox.AddChild(new TextureRect
			{
				Texture = iconTex,
				Modulate = locked
					? new Color(0.45f, 0.45f, 0.45f, 0.55f)
					: (equipped ? new Color(iconTint.R, iconTint.G, iconTint.B, 0.40f) : iconTint),
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				MouseFilter = MouseFilterEnum.Ignore
			});
		}
		vbox.AddChild(portraitBox);

		var nameLbl = MakeTextLabel(title, equipped ? new Color(0.48f, 0.44f, 0.39f, 0.72f) : new Color(0.18f, 0.13f, 0.10f, 1f), 20, expandFill: true, clip: true);
		nameLbl.HorizontalAlignment = HorizontalAlignment.Center;
		vbox.AddChild(nameLbl);

		if (locked && TexLock != null)
		{
			var lockIcon = new TextureRect
			{
				Texture = TexLock,
				Modulate = new Color(0.38f, 0.28f, 0.24f, 0.88f),
				CustomMinimumSize = new Vector2(20, 20),
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				MouseFilter = MouseFilterEnum.Ignore
			};
			lockIcon.SetAnchorsPreset(LayoutPreset.TopRight);
			lockIcon.OffsetLeft = -25f;
			lockIcon.OffsetTop = 5f;
			lockIcon.OffsetRight = -5f;
			lockIcon.OffsetBottom = 25f;
			host.AddChild(lockIcon);
		}
		else if (equipped && TexCheck != null)
		{
			var checkIcon = new TextureRect
			{
				Texture = TexCheck,
				Modulate = new Color(0.14f, 0.62f, 0.38f, 1f),
				CustomMinimumSize = new Vector2(20, 20),
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				MouseFilter = MouseFilterEnum.Ignore
			};
			checkIcon.SetAnchorsPreset(LayoutPreset.TopRight);
			checkIcon.OffsetLeft = -25f;
			checkIcon.OffsetTop = 5f;
			checkIcon.OffsetRight = -5f;
			checkIcon.OffsetBottom = 25f;
			host.AddChild(checkIcon);
		}
		else if (!string.IsNullOrEmpty(qtyBadge))
		{
			var qtyLbl = MakeTextLabel(qtyBadge, new Color(0.22f, 0.44f, 0.66f, 0.95f), 15, expandFill: false, clip: false);
			qtyLbl.HorizontalAlignment = HorizontalAlignment.Right;
			qtyLbl.SetAnchorsPreset(LayoutPreset.TopRight);
			qtyLbl.OffsetLeft = -42f;
			qtyLbl.OffsetTop = 4f;
			qtyLbl.OffsetRight = -6f;
			qtyLbl.OffsetBottom = 22f;
			host.AddChild(qtyLbl);
		}
	}

	private void RebuildTeammatesPanel()
	{
		if (_teammatePanel == null || _teammatesHBox == null) return;
		foreach (Node child in _teammatesHBox.GetChildren()) child.QueueFree();

		if (_teamMode != TeamMode.Duo2v2)
		{
			var soloRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
			soloRow.AddThemeConstantOverride("separation", 14);
			_teammatesHBox.AddChild(soloRow);

			soloRow.AddChild(MakeOutlinedLabel("Đấu đơn 1 VS 1", new Color(0.90f, 0.94f, 1f, 0.96f), 22, expandFill: false, clip: false));
			string oppStatus = _othersLocked ? "Đối thủ đã khóa chọn" : "Đối thủ đang chọn đội hình...";
			Color oppCol = _othersLocked ? new Color(0.45f, 0.94f, 0.66f, 1f) : new Color(1f, 0.86f, 0.48f, 1f);
			soloRow.AddChild(MakeOutlinedLabel($"· {oppStatus}", oppCol, 22, expandFill: false, clip: false));
			return;
		}

		// Bố cục đầy đủ thông tin:
		// [Avatar]  Tên đồng đội  · Cấp · Hạng      [Trạng thái khóa]
		//           Nhân vật: [Avatar + Tên] [Avatar + Tên] [Avatar + Tên]
		//           Vật phẩm: [Icon + Tên]   [Icon + Tên]
		var mateCard = new HBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter
		};
		mateCard.AddThemeConstantOverride("separation", 18);
		_teammatesHBox.AddChild(mateCard);

		if (TexAvatar != null)
		{
			var avatarFrame = new PanelContainer
			{
				CustomMinimumSize = new Vector2(86, 86),
				SizeFlagsVertical = SizeFlags.ShrinkBegin
			};
			avatarFrame.AddThemeStyleboxOverride("panel", MakeMiniTileStyle(new Color(0.42f, 0.64f, 0.96f, 1f)));
			avatarFrame.AddChild(new TextureRect
			{
				Texture = TexAvatar,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
			});
			mateCard.AddChild(avatarFrame);
		}

		var infoVBox = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		infoVBox.AddThemeConstantOverride("separation", 10);
		mateCard.AddChild(infoVBox);

		// Dòng 1: Tên + Hạng/Cấp + Badge trạng thái rõ ràng
		var headerRow = new HBoxContainer();
		headerRow.AddThemeConstantOverride("separation", 12);
		infoVBox.AddChild(headerRow);

		string mateName = _playType == PlayMatchType.Bot ? "Bot Đồng Đội" : "Trần Minh Khoa";
		string mateMeta = _playType == PlayMatchType.Bot
			? $"· {GameCatalog.GetBotDifficultyLabel(_data != null ? _data.SelectedBotDifficulty : BotDifficulty.Medium)}"
			: "· Lv.14 · Hạng Vàng";

		headerRow.AddChild(MakeOutlinedLabel(mateName, new Color(1f, 0.96f, 0.84f, 1f), 26, expandFill: false, clip: false));
		headerRow.AddChild(MakeOutlinedLabel(mateMeta, new Color(0.84f, 0.90f, 1f, 0.94f), 21, expandFill: false, clip: false));

		var spacer = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		headerRow.AddChild(spacer);

		bool mateLocked = _teammateStage >= 3;
		Color statusCol = mateLocked ? new Color(0.42f, 0.96f, 0.66f, 1f) : new Color(1f, 0.84f, 0.36f, 1f);
		headerRow.AddChild(MakeOutlinedLabel(mateLocked ? "Đã khóa chọn" : "Đang chọn...", statusCol, 22, expandFill: false, clip: false));

		// Dòng 2: Các nhân vật đồng đội chọn (Avatar + Tên, không bọc hộp chữ nhật quanh chữ)
		var chars = _data?.Characters ?? PlayerDataManager.CreateDefaultCharacters();
		int[] mateChars = { 0, Mathf.Min(1, chars.Length - 1), Mathf.Min(2, chars.Length - 1) };
		int revealedChars = _teammateStage switch { 1 => 1, 2 => 2, >= 3 => 3, _ => 0 };

		var charsRow = new HBoxContainer();
		charsRow.AddThemeConstantOverride("separation", 18);
		infoVBox.AddChild(charsRow);

		var charTag = MakeOutlinedLabel("Nhân vật:", new Color(0.76f, 0.88f, 1f, 0.95f), 21, expandFill: false, clip: false);
		charTag.CustomMinimumSize = new Vector2(96, 0);
		charsRow.AddChild(charTag);

		for (int i = 0; i < UnlockedCharSlots; i++)
		{
			bool filled = i < revealedChars;
			var row = new HBoxContainer
			{
				CustomMinimumSize = new Vector2(210, 48),
				SizeFlagsVertical = SizeFlags.ShrinkCenter
			};
			row.AddThemeConstantOverride("separation", 10);

			if (filled)
			{
				var ch = chars[mateChars[i]];
				if (TexAvatar != null)
				{
					var iconBox = new PanelContainer
					{
						CustomMinimumSize = new Vector2(44, 44),
						SizeFlagsVertical = SizeFlags.ShrinkCenter
					};
					iconBox.AddThemeStyleboxOverride("panel", MakeMiniTileStyle(new Color(0.24f, 0.74f, 0.52f, 0.95f)));
					iconBox.AddChild(new TextureRect
					{
						Texture = TexAvatar,
						Modulate = ch.Tint,
						ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
						StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
					});
					row.AddChild(iconBox);
				}
				row.AddChild(MakeOutlinedLabel(ch.Name, new Color(0.98f, 0.97f, 0.92f, 0.98f), 20, expandFill: true, clip: true));
			}
			else
			{
				row.AddChild(MakeOutlinedLabel("· Chưa chọn", new Color(0.74f, 0.78f, 0.90f, 0.65f), 19, expandFill: true, clip: false));
			}
			charsRow.AddChild(row);
		}

		// Dòng 3: Các vật phẩm đồng đội mang (Icon + Tên, không bọc hộp chữ nhật quanh chữ)
		string[] mateItems = { "item_tra_da", "item_bua_x2" };
		int revealedItems = _teammateStage switch { 2 => 1, >= 3 => 2, _ => 0 };

		var itemsRow = new HBoxContainer();
		itemsRow.AddThemeConstantOverride("separation", 18);
		infoVBox.AddChild(itemsRow);

		var itemTag = MakeOutlinedLabel("Vật phẩm:", new Color(0.98f, 0.88f, 0.64f, 0.95f), 21, expandFill: false, clip: false);
		itemTag.CustomMinimumSize = new Vector2(96, 0);
		itemsRow.AddChild(itemTag);

		for (int i = 0; i < UnlockedItemSlots; i++)
		{
			bool filled = i < revealedItems;
			var row = new HBoxContainer
			{
				CustomMinimumSize = new Vector2(240, 42),
				SizeFlagsVertical = SizeFlags.ShrinkCenter
			};
			row.AddThemeConstantOverride("separation", 10);

			if (filled)
			{
				var meta = GameCatalog.FindConsumable(mateItems[i]);
				var tex = meta != null && !string.IsNullOrEmpty(meta.IconPath) ? GD.Load<Texture2D>(meta.IconPath) : null;
				if (tex != null)
				{
					var iconBox = new PanelContainer
					{
						CustomMinimumSize = new Vector2(38, 38),
						SizeFlagsVertical = SizeFlags.ShrinkCenter
					};
					iconBox.AddThemeStyleboxOverride("panel", MakeMiniTileStyle(new Color(0.86f, 0.64f, 0.26f, 0.95f)));
					iconBox.AddChild(new TextureRect
					{
						Texture = tex,
						Modulate = meta.IconTint,
						ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
						StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
					});
					row.AddChild(iconBox);
				}
				row.AddChild(MakeOutlinedLabel(meta?.Name ?? "", new Color(0.98f, 0.96f, 0.90f, 0.96f), 19, expandFill: true, clip: true));
			}
			else
			{
				row.AddChild(MakeOutlinedLabel("· Chưa chọn", new Color(0.74f, 0.78f, 0.90f, 0.65f), 19, expandFill: true, clip: false));
			}
			itemsRow.AddChild(row);
		}
	}

	private Variant StartSlotCharDrag(Control source, int slotIndex)
	{
		if (_selfLocked || slotIndex < 0 || slotIndex >= _charSlots.Length || _charSlots[slotIndex] < 0)
			return default;

		int charIdx = _charSlots[slotIndex];
		var chars = _data?.Characters ?? PlayerDataManager.CreateDefaultCharacters();
		if (charIdx < 0 || charIdx >= chars.Length) return default;
		var ch = chars[charIdx];
		return BeginCardDrag(source, "char", charIdx, string.Empty, slotIndex, TexAvatar, ch.Tint, ch.Name, isCharacter: true);
	}

	private Variant StartSlotItemDrag(Control source, int slotIndex)
	{
		if (_selfLocked || slotIndex < 0 || slotIndex >= _itemSlots.Length || string.IsNullOrEmpty(_itemSlots[slotIndex]))
			return default;

		string itemId = _itemSlots[slotIndex];
		var meta = GameCatalog.FindConsumable(itemId);
		if (meta == null) return default;
		Texture2D tex = !string.IsNullOrEmpty(meta.IconPath) ? GD.Load<Texture2D>(meta.IconPath) : null;
		return BeginCardDrag(source, "item", -1, itemId, slotIndex, tex, meta.IconTint, meta.Name, isCharacter: false);
	}

	private Variant BeginCardDrag(
		Control source,
		string kind,
		int charIndex,
		string itemId,
		int fromSlot,
		Texture2D previewTex,
		Color tint,
		string title,
		bool isCharacter)
	{
		_activeDragKind = kind;
		_activeDragSource = source;
		_hoveredSlotKind = null;
		_hoveredSlotIndex = -1;

		if (IsInstanceValid(source))
		{
			source.Modulate = new Color(1f, 1f, 1f, 0.36f);
		}

		var preview = BuildFloatingCardPreview(previewTex, tint, title, isCharacter);
		source.SetDragPreview(preview);
		UpdateDropTargetHighlights();

		return new Godot.Collections.Dictionary
		{
			{ "kind", kind },
			{ "char_index", charIndex },
			{ "item_id", itemId ?? string.Empty },
			{ "from_slot", fromSlot }
		};
	}

	/// <summary>
	/// Card gọn nổi lên phía trên ngón tay khi cầm kéo trên điện thoại (avatar/icon + tên, bóng đổ, nghiêng nhẹ & nảy mượt).
	/// </summary>
	private static Control BuildFloatingCardPreview(Texture2D iconTex, Color iconTint, string title, bool isCharacter)
	{
		var anchor = new Control { MouseFilter = MouseFilterEnum.Ignore };

		const float cardW = 140f;
		const float cardH = 136f;

		var card = new PanelContainer
		{
			CustomMinimumSize = new Vector2(cardW, cardH),
			Size = new Vector2(cardW, cardH),
			Position = new Vector2(-cardW * 0.5f, -cardH * 0.74f),
			PivotOffset = new Vector2(cardW * 0.5f, cardH * 0.5f),
			RotationDegrees = -3.5f,
			Scale = new Vector2(0.84f, 0.84f),
			MouseFilter = MouseFilterEnum.Ignore
		};

		card.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(1.0f, 0.985f, 0.94f, 0.98f),
			BorderWidthLeft = 4,
			BorderWidthTop = 4,
			BorderWidthRight = 4,
			BorderWidthBottom = 5,
			BorderColor = isCharacter ? new Color(0.96f, 0.74f, 0.22f, 1f) : new Color(0.18f, 0.74f, 0.56f, 1f),
			CornerRadiusTopLeft = 14,
			CornerRadiusTopRight = 14,
			CornerRadiusBottomRight = 14,
			CornerRadiusBottomLeft = 14,
			ShadowColor = new Color(0f, 0f, 0f, 0.36f),
			ShadowSize = 12,
			ShadowOffset = new Vector2(0, 8)
		});

		var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
		margin.AddThemeConstantOverride("margin_left", 10);
		margin.AddThemeConstantOverride("margin_top", 8);
		margin.AddThemeConstantOverride("margin_right", 10);
		margin.AddThemeConstantOverride("margin_bottom", 8);
		card.AddChild(margin);

		var vbox = new VBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center,
			MouseFilter = MouseFilterEnum.Ignore
		};
		vbox.AddThemeConstantOverride("separation", 5);
		margin.AddChild(vbox);

		var portraitBox = new PanelContainer
		{
			CustomMinimumSize = new Vector2(64, 64),
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
			MouseFilter = MouseFilterEnum.Ignore
		};
		portraitBox.AddThemeStyleboxOverride("panel", MakeMiniTileStyle(isCharacter
			? new Color(0.28f, 0.44f, 0.74f, 1f)
			: new Color(0.84f, 0.58f, 0.22f, 1f)));
		if (iconTex != null)
		{
			portraitBox.AddChild(new TextureRect
			{
				Texture = iconTex,
				Modulate = iconTint,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				MouseFilter = MouseFilterEnum.Ignore
			});
		}
		vbox.AddChild(portraitBox);

		var nameLbl = MakeTextLabel(title, new Color(0.18f, 0.12f, 0.09f, 1f), 19, expandFill: true, clip: true);
		nameLbl.HorizontalAlignment = HorizontalAlignment.Center;
		vbox.AddChild(nameLbl);

		anchor.AddChild(card);

		card.Ready += () =>
		{
			var tw = card.CreateTween();
			tw.TweenProperty(card, "scale", new Vector2(1.06f, 1.06f), 0.14f)
				.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		};

		return anchor;
	}

	private bool CheckCanDropOnSlot(string expectedKind, int slotIdx, Variant data)
	{
		bool canDrop = CanDropKind(expectedKind, data);
		if (canDrop && (_hoveredSlotKind != expectedKind || _hoveredSlotIndex != slotIdx))
		{
			_hoveredSlotKind = expectedKind;
			_hoveredSlotIndex = slotIdx;
			UpdateDropTargetHighlights();
		}
		return canDrop;
	}

	private void ClearHoveredSlot(string kind, int slotIdx)
	{
		if (_hoveredSlotKind == kind && _hoveredSlotIndex == slotIdx)
		{
			_hoveredSlotKind = null;
			_hoveredSlotIndex = -1;
			UpdateDropTargetHighlights();
		}
	}

	private void EndActiveDragVisuals()
	{
		if (IsInstanceValid(_activeDragSource))
		{
			_activeDragSource.Modulate = Colors.White;
		}
		_activeDragKind = null;
		_activeDragSource = null;
		_hoveredSlotKind = null;
		_hoveredSlotIndex = -1;
		UpdateDropTargetHighlights();
	}

	private void UpdateDropTargetHighlights()
	{
		bool draggingChar = _activeDragKind == "char";
		bool draggingItem = _activeDragKind == "item";

		for (int i = 0; i < _charSlotButtons.Count; i++)
		{
			var btn = _charSlotButtons[i];
			if (!IsInstanceValid(btn)) continue;
			bool locked = i >= UnlockedCharSlots;
			bool occupied = !locked && _charSlots[i] >= 0;
			bool highlight = draggingChar && !locked;
			bool hovered = highlight && _hoveredSlotKind == "char" && _hoveredSlotIndex == i;
			ApplySlotStyle(btn, occupied, locked, highlight, hovered);
			AnimateSlotHoverScale(btn, hovered ? 1.05f : 1.0f);
		}

		for (int i = 0; i < _itemSlotButtons.Count; i++)
		{
			var btn = _itemSlotButtons[i];
			if (!IsInstanceValid(btn)) continue;
			bool locked = i >= UnlockedItemSlots;
			bool occupied = !locked && !string.IsNullOrEmpty(_itemSlots[i]);
			bool highlight = draggingItem && !locked;
			bool hovered = highlight && _hoveredSlotKind == "item" && _hoveredSlotIndex == i;
			ApplySlotStyle(btn, occupied, locked, highlight, hovered);
			AnimateSlotHoverScale(btn, hovered ? 1.05f : 1.0f);
		}
	}

	private static void AnimateSlotHoverScale(Control slotBtn, float targetScale)
	{
		slotBtn.PivotOffset = slotBtn.Size * 0.5f;
		var tw = slotBtn.CreateTween();
		tw.TweenProperty(slotBtn, "scale", new Vector2(targetScale, targetScale), 0.10f)
			.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
	}

	private void AttachWarehouseDropTarget(Control target)
	{
		if (target == null) return;
		target.SetDragForwarding(
			Callable.From<Vector2, Variant>(_ => default),
			Callable.From<Vector2, Variant, bool>((_, data) => CanDropBackToWarehouse(data)),
			Callable.From<Vector2, Variant>((_, data) => OnDropBackToWarehouse(data))
		);
	}

	private static void PlaySlotSnapBounce(Control slotBtn)
	{
		Callable.From(() =>
		{
			if (!IsInstanceValid(slotBtn)) return;
			slotBtn.PivotOffset = slotBtn.Size * 0.5f;
			slotBtn.Scale = new Vector2(0.86f, 0.86f);
			var tw = slotBtn.CreateTween();
			tw.TweenProperty(slotBtn, "scale", Vector2.One, 0.22f)
				.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		}).CallDeferred();
	}

	private void MarkJustPlaced(string kind, int slotIdx)
	{
		_justPlacedSlotKind = kind;
		_justPlacedSlotIndex = slotIdx;
	}

	private bool CanDropKind(string expectedKind, Variant data)
	{
		if (_selfLocked || data.VariantType != Variant.Type.Dictionary) return false;
		var dict = data.AsGodotDictionary();
		return dict.ContainsKey("kind") && dict["kind"].AsString() == expectedKind;
	}

	private bool CanDropBackToWarehouse(Variant data)
	{
		if (_selfLocked || data.VariantType != Variant.Type.Dictionary) return false;
		var dict = data.AsGodotDictionary();
		return dict.ContainsKey("from_slot") && dict["from_slot"].AsInt32() >= 0;
	}

	private void OnDropBackToWarehouse(Variant data)
	{
		if (!CanDropBackToWarehouse(data)) return;
		var dict = data.AsGodotDictionary();
		string kind = dict["kind"].AsString();
		int fromSlot = dict["from_slot"].AsInt32();

		if (kind == "char" && fromSlot >= 0 && fromSlot < _charSlots.Length)
		{
			_charSlots[fromSlot] = -1;
		}
		else if (kind == "item" && fromSlot >= 0 && fromSlot < _itemSlots.Length)
		{
			_itemSlots[fromSlot] = null;
		}
		EndActiveDragVisuals();
		RefreshAllPrepVisuals();
	}

	private void OnCharDroppedToSlot(int targetSlot, Variant data)
	{
		if (!CanDropKind("char", data) || targetSlot < 0 || targetSlot >= _charSlots.Length) return;
		var payload = data.AsGodotDictionary();
		int charIdx = payload["char_index"].AsInt32();
		int fromSlot = payload["from_slot"].AsInt32();
		if (charIdx < 0) return;

		if (fromSlot >= 0 && fromSlot < _charSlots.Length && fromSlot != targetSlot)
		{
			int displaced = _charSlots[targetSlot];
			_charSlots[targetSlot] = charIdx;
			_charSlots[fromSlot] = displaced;
		}
		else
		{
			RemoveCharFromSlots(charIdx);
			_charSlots[targetSlot] = charIdx;
		}
		MarkJustPlaced("char", targetSlot);
		EndActiveDragVisuals();
		RefreshAllPrepVisuals();
	}

	private void OnItemDroppedToSlot(int targetSlot, Variant data)
	{
		if (!CanDropKind("item", data) || targetSlot < 0 || targetSlot >= _itemSlots.Length) return;
		var payload = data.AsGodotDictionary();
		string itemId = payload["item_id"].AsString();
		int fromSlot = payload["from_slot"].AsInt32();
		if (string.IsNullOrEmpty(itemId)) return;

		if (fromSlot >= 0 && fromSlot < _itemSlots.Length && fromSlot != targetSlot)
		{
			string displaced = _itemSlots[targetSlot];
			_itemSlots[targetSlot] = itemId;
			_itemSlots[fromSlot] = displaced;
		}
		else
		{
			RemoveItemFromSlots(itemId);
			_itemSlots[targetSlot] = itemId;
		}
		MarkJustPlaced("item", targetSlot);
		EndActiveDragVisuals();
		RefreshAllPrepVisuals();
	}

	private int PlaceCharInFirstEmptySlot(int charIndex)
	{
		for (int i = 0; i < _charSlots.Length; i++)
		{
			if (_charSlots[i] < 0)
			{
				_charSlots[i] = charIndex;
				return i;
			}
		}
		return -1;
	}

	private void RemoveCharFromSlots(int charIndex)
	{
		for (int i = 0; i < _charSlots.Length; i++)
		{
			if (_charSlots[i] == charIndex) _charSlots[i] = -1;
		}
	}

	private int PlaceItemInFirstEmptySlot(string itemId)
	{
		for (int i = 0; i < _itemSlots.Length; i++)
		{
			if (string.IsNullOrEmpty(_itemSlots[i]))
			{
				_itemSlots[i] = itemId;
				return i;
			}
		}
		return -1;
	}

	private void RemoveItemFromSlots(string itemId)
	{
		for (int i = 0; i < _itemSlots.Length; i++)
		{
			if (_itemSlots[i] == itemId) _itemSlots[i] = null;
		}
	}

	private void RefreshAllPrepVisuals()
	{
		RebuildCharSlots();
		RebuildItemSlots();
		RebuildWarehouseCharacters();
		RebuildWarehouseItems();
		_justPlacedSlotKind = null;
		_justPlacedSlotIndex = -1;
	}

	private void OnLockInPressed()
	{
		if (!_isActive || _selfLocked) return;
		if (!HasAnySelectedCharacter())
		{
			_charSlots[0] = GetFallbackAccountCharacterIndex();
		}

		_selfLocked = true;

		if (_lockInBtn != null)
		{
			_lockInBtn.Disabled = true;
			_lockInBtn.Text = "Đã khóa";
			_lockInBtn.Modulate = new Color(0.86f, 0.96f, 0.90f, 0.9f);
		}
		RefreshAllPrepVisuals();
		UpdateTeamStatusLabel();
	}

	private void UpdateTeamStatusLabel()
	{
		if (_teamStatusLabel == null) return;
		if (_selfLocked && _othersLocked)
		{
			_teamStatusLabel.Text = "Đang vào trận...";
		}
		else if (_selfLocked)
		{
			_teamStatusLabel.Text = "Đã khóa · Chờ người chơi khác";
		}
		else if (_othersLocked)
		{
			_teamStatusLabel.Text = "Người chơi khác đã khóa";
		}
		else
		{
			_teamStatusLabel.Text = "Chờ khóa chọn";
		}
	}

	private void LaunchMatch()
	{
		if (!_isActive) return;
		_isActive = false;

		int primaryChar = GetFallbackAccountCharacterIndex();
		var chosenChars = new List<int>();
		for (int i = 0; i < _charSlots.Length; i++)
		{
			if (_charSlots[i] >= 0) chosenChars.Add(_charSlots[i]);
		}
		if (chosenChars.Count == 0) chosenChars.Add(primaryChar);
		else primaryChar = chosenChars[0];
		_data?.SetSelectedCharacter(primaryChar);

		var chosenItems = new List<string>();
		for (int i = 0; i < _itemSlots.Length; i++)
		{
			if (!string.IsNullOrEmpty(_itemSlots[i])) chosenItems.Add(_itemSlots[i]);
		}

		var map = GameCatalog.FindMap(_mapId);
		string scenePath = map != null && !string.IsNullOrEmpty(map.ScenePath)
			? map.ScenePath
			: "res://scenes/gameplay/map_01_street.tscn";

		if (_data != null)
		{
			_data.ActiveMatchCharacterIndices = chosenChars.ToArray();
			_data.ActiveMatchItemIds = chosenItems.ToArray();
			_data.ActiveMatchScenePath = scenePath;
		}

		GD.Print($"[PreMatchPrepScreen] Chuyển sang màn hình tải trận: {scenePath} | Chars=[{string.Join(",", chosenChars)}] | Items=[{string.Join(",", chosenItems)}]");
		GetTree().ChangeSceneToFile("res://scenes/ui/match_loading_screen.tscn");
	}

	private static Label MakeOutlinedLabel(string text, Color color, int fontSize, bool expandFill, bool clip)
	{
		var lbl = MakeTextLabel(text, color, fontSize, expandFill, clip);
		lbl.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.75f));
		lbl.AddThemeConstantOverride("outline_size", 4);
		return lbl;
	}

	private static Label MakeTextLabel(string text, Color color, int fontSize, bool expandFill, bool clip)
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
		return lbl;
	}

	private static StyleBoxFlat MakeEmptyWarehouseCellStyle() => new()
	{
		BgColor = new Color(0.20f, 0.14f, 0.10f, 0.24f),
		BorderWidthLeft = 2,
		BorderWidthTop = 2,
		BorderWidthRight = 2,
		BorderWidthBottom = 2,
		BorderColor = new Color(0.56f, 0.44f, 0.32f, 0.36f),
		CornerRadiusTopLeft = 12,
		CornerRadiusTopRight = 12,
		CornerRadiusBottomRight = 12,
		CornerRadiusBottomLeft = 12
	};

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

	private static void ApplySlotStyle(Button btn, bool occupied, bool isLockedSlot, bool highlighted, bool hovered)
	{
		Color bgColor;
		Color borderColor;
		int borderWidth;

		if (isLockedSlot)
		{
			bgColor = new Color(0.10f, 0.10f, 0.18f, 0.56f);
			borderColor = new Color(0.50f, 0.50f, 0.68f, 0.42f);
			borderWidth = 2;
		}
		else if (hovered)
		{
			bgColor = occupied ? new Color(1.0f, 0.98f, 0.88f, 0.95f) : new Color(0.20f, 0.46f, 0.44f, 0.72f);
			borderColor = new Color(1.0f, 0.84f, 0.28f, 1f);
			borderWidth = 3;
		}
		else if (highlighted)
		{
			bgColor = occupied ? new Color(0.97f, 0.95f, 0.88f, 0.92f) : new Color(0.18f, 0.36f, 0.44f, 0.64f);
			borderColor = new Color(0.34f, 0.88f, 0.64f, 0.94f);
			borderWidth = 3;
		}
		else if (occupied)
		{
			bgColor = new Color(0.96f, 0.94f, 0.87f, 0.90f);
			borderColor = new Color(0.18f, 0.66f, 0.44f, 0.95f);
			borderWidth = 2;
		}
		else
		{
			bgColor = new Color(0.14f, 0.14f, 0.24f, 0.52f);
			borderColor = new Color(0.76f, 0.76f, 0.92f, 0.45f);
			borderWidth = 2;
		}

		var style = new StyleBoxFlat
		{
			BgColor = bgColor,
			BorderWidthLeft = borderWidth,
			BorderWidthTop = borderWidth,
			BorderWidthRight = borderWidth,
			BorderWidthBottom = borderWidth,
			BorderColor = borderColor,
			CornerRadiusTopLeft = 12,
			CornerRadiusTopRight = 12,
			CornerRadiusBottomRight = 12,
			CornerRadiusBottomLeft = 12
		};
		btn.AddThemeStyleboxOverride("normal", style);
		btn.AddThemeStyleboxOverride("hover", style);
		btn.AddThemeStyleboxOverride("pressed", style);
		btn.AddThemeStyleboxOverride("disabled", style);
	}

	private static void ApplyWarehouseCardStyle(Button btn, bool equipped, bool locked, bool isCharacter)
	{
		var style = new StyleBoxFlat
		{
			BgColor = locked
				? new Color(0.84f, 0.81f, 0.76f, 0.80f)
				: (equipped ? new Color(0.86f, 0.83f, 0.77f, 0.72f) : new Color(0.99f, 0.97f, 0.93f, 1f)),
			BorderWidthLeft = 2,
			BorderWidthTop = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 3,
			BorderColor = equipped
				? new Color(0.15f, 0.62f, 0.44f, 0.7f)
				: (locked ? new Color(0.64f, 0.60f, 0.54f, 0.65f) : (isCharacter ? new Color(0.42f, 0.54f, 0.78f, 0.85f) : new Color(0.80f, 0.60f, 0.34f, 0.85f))),
			CornerRadiusTopLeft = 12,
			CornerRadiusTopRight = 12,
			CornerRadiusBottomRight = 12,
			CornerRadiusBottomLeft = 12
		};
		btn.AddThemeStyleboxOverride("normal", style);
		btn.AddThemeStyleboxOverride("hover", style);
		btn.AddThemeStyleboxOverride("pressed", style);
		btn.AddThemeStyleboxOverride("disabled", style);
	}
}
