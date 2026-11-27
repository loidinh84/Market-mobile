using System.Collections.Generic;
using Godot;
using MarketMobile.Core;
using MarketMobile.Utils;

namespace MarketMobile.UI;

/// <summary>
/// Giao diện Túi đồ (tab thứ 5 của LeftSidebar).
/// Hiển thị vật phẩm, đồ tiêu thụ, tài nguyên và túi quà của người chơi.
/// </summary>
public partial class SettingsPanel : Control
{
	public enum BagCategory
	{
		All = 0,
		Recent = 1,
		Items = 2,
		Consumables = 3,
		Resources = 4,
		GiftBags = 5
	}

	private sealed class BagEntry
	{
		public string Id { get; init; }
		public BagCategory Category { get; init; }
		public string Name { get; init; }
		public string Tag { get; init; }
		public string Summary { get; init; }
		public string Description { get; init; }
		public string IconPath { get; init; }
		public Color IconTint { get; init; } = Colors.White;
		public int Quantity { get; init; } = 1;
		public string BadgeText { get; init; }
		public Color AccentColor { get; init; } = new Color(0.95f, 0.78f, 0.28f, 1f);
	}

	[Signal]
	public delegate void GoToShopRequestedEventHandler(int shopTabIndex);

	[Signal]
	public delegate void GoToEventsRequestedEventHandler();

	private static readonly Font OrcishFont = GD.Load<Font>("res://assets/fonts/FZ-Orcish.ttf");

	private PlayerDataManager _data;

	private Button[] _filterButtons;
	private Button _goShopBtn;
	private GridContainer _itemsGrid;
	private Control _emptyStateBox;
	private Label _emptyLabel;
	private Button _emptyShopBtn;

	// Right inspector panel
	private Control _detailContent;
	private Control _detailEmptyHint;
	private Label _detailTagLabel;
	private TextureRect _detailIcon;
	private Label _detailTitle;
	private Label _detailQtyLabel;
	private Label _detailSummary;
	private Label _detailDesc;
	private Button _detailActionBtn;

	// Gift Reward Celebration Modal
	private Control _giftModal;
	private Control _giftBackdrop;
	private Control _chestStage;
	private Control _glowHalo;
	private TextureRect _openingIcon;
	private Label _openingLabel;
	private PanelContainer _rewardDialog;
	private Label _bagSourceLabel;
	private HBoxContainer _rewardsRow;
	private Button _confirmRewardBtn;

	private readonly Dictionary<string, Texture2D> _iconCache = new();
	private readonly List<BagEntry> _visibleEntries = new();
	private BagCategory _currentCategory = BagCategory.All;
	private string _selectedEntryId;
	private BagEntry _selectedEntry;
	private Tween _giftAnimTween;
	private bool _canDismissGiftModal = false;

	private StyleBoxFlat _styleFilterActive;
	private StyleBoxFlat _styleFilterInactive;
	private StyleBoxFlat _styleSlotNormal;
	private StyleBoxFlat _styleSlotSelected;
	private StyleBoxFlat _styleActionPrimary;
	private StyleBoxFlat _styleActionSecondary;
	private StyleBoxFlat _styleActionGift;

	public override void _Ready()
	{
		_data = PlayerDataManager.Instance ?? GetNodeOrNull<PlayerDataManager>("/root/PlayerDataManager");

		_filterButtons = new[]
		{
			GetNodeOrNull<Button>("InventoryCard/MainVBox/FilterRow/FilterAllBtn"),
			GetNodeOrNull<Button>("InventoryCard/MainVBox/FilterRow/FilterRecentBtn"),
			GetNodeOrNull<Button>("InventoryCard/MainVBox/FilterRow/FilterItemsBtn"),
			GetNodeOrNull<Button>("InventoryCard/MainVBox/FilterRow/FilterConsumablesBtn"),
			GetNodeOrNull<Button>("InventoryCard/MainVBox/FilterRow/FilterResourcesBtn"),
			GetNodeOrNull<Button>("InventoryCard/MainVBox/FilterRow/FilterGiftBagsBtn")
		};
		_goShopBtn = GetNodeOrNull<Button>("InventoryCard/MainVBox/FilterRow/GoShopBtn");
		_itemsGrid = GetNodeOrNull<GridContainer>("InventoryCard/MainVBox/SplitHBox/LeftInset/Margin/Scroll/VBox/ItemsGrid");
		_emptyStateBox = GetNodeOrNull<Control>("InventoryCard/MainVBox/SplitHBox/LeftInset/Margin/Scroll/VBox/EmptyStateBox");
		_emptyLabel = GetNodeOrNull<Label>("InventoryCard/MainVBox/SplitHBox/LeftInset/Margin/Scroll/VBox/EmptyStateBox/EmptyLabel");
		_emptyShopBtn = GetNodeOrNull<Button>("InventoryCard/MainVBox/SplitHBox/LeftInset/Margin/Scroll/VBox/EmptyStateBox/EmptyShopBtn");

		_detailContent = GetNodeOrNull<Control>("InventoryCard/MainVBox/SplitHBox/InspectorPanel/Margin/DetailContent");
		_detailEmptyHint = GetNodeOrNull<Control>("InventoryCard/MainVBox/SplitHBox/InspectorPanel/Margin/EmptyHint");
		_detailTagLabel = GetNodeOrNull<Label>("InventoryCard/MainVBox/SplitHBox/InspectorPanel/Margin/DetailContent/TagPill/TagLabel");
		_detailIcon = GetNodeOrNull<TextureRect>("InventoryCard/MainVBox/SplitHBox/InspectorPanel/Margin/DetailContent/IconBox/IconRect");
		_detailTitle = GetNodeOrNull<Label>("InventoryCard/MainVBox/SplitHBox/InspectorPanel/Margin/DetailContent/DetailTitle");
		_detailQtyLabel = GetNodeOrNull<Label>("InventoryCard/MainVBox/SplitHBox/InspectorPanel/Margin/DetailContent/DetailQty");
		_detailSummary = GetNodeOrNull<Label>("InventoryCard/MainVBox/SplitHBox/InspectorPanel/Margin/DetailContent/DetailSummary");
		_detailDesc = GetNodeOrNull<Label>("InventoryCard/MainVBox/SplitHBox/InspectorPanel/Margin/DetailContent/DetailDesc");
		_detailActionBtn = GetNodeOrNull<Button>("InventoryCard/MainVBox/SplitHBox/InspectorPanel/Margin/DetailContent/ActionBtn");

		_giftModal = GetNodeOrNull<Control>("GiftRewardModal");
		_giftBackdrop = GetNodeOrNull<Control>("GiftRewardModal/Backdrop");
		_chestStage = GetNodeOrNull<Control>("GiftRewardModal/Center/ChestStage");
		_glowHalo = GetNodeOrNull<Control>("GiftRewardModal/Center/ChestStage/GlowHalo");
		_openingIcon = GetNodeOrNull<TextureRect>("GiftRewardModal/Center/ChestStage/OpeningIcon");
		_openingLabel = GetNodeOrNull<Label>("GiftRewardModal/Center/ChestStage/OpeningLabel");
		_rewardDialog = GetNodeOrNull<PanelContainer>("GiftRewardModal/Center/RewardDialog");
		_bagSourceLabel = GetNodeOrNull<Label>("GiftRewardModal/Center/RewardDialog/VBox/BagSourceLabel");
		_rewardsRow = GetNodeOrNull<HBoxContainer>("GiftRewardModal/Center/RewardDialog/VBox/RewardsRow");
		_confirmRewardBtn = GetNodeOrNull<Button>("GiftRewardModal/Center/RewardDialog/VBox/ConfirmBtn");

		if (_giftModal != null) _giftModal.Visible = false;
		if (_giftBackdrop != null) _giftBackdrop.GuiInput += OnGiftBackdropInput;
		if (_confirmRewardBtn != null) _confirmRewardBtn.Pressed += CloseGiftRewardModal;

		InitStyles();

		for (int i = 0; i < _filterButtons.Length; i++)
		{
			int catIndex = i;
			if (_filterButtons[i] != null)
			{
				_filterButtons[i].Pressed += () => SwitchCategory((BagCategory)catIndex);
			}
		}

		if (_goShopBtn != null) _goShopBtn.Pressed += () => EmitSignal(SignalName.GoToShopRequested, 1);
		if (_emptyShopBtn != null) _emptyShopBtn.Pressed += OnEmptyActionPressed;
		if (_detailActionBtn != null) _detailActionBtn.Pressed += OnDetailActionPressed;

		if (_data != null)
		{
			_data.InventoryChanged += OnInventoryDataChanged;
			_data.CurrenciesChanged += OnCurrenciesDataChanged;
			_data.GachaStateChanged += OnGachaStateChanged;
		}

		UIButtonHelper.AttachToAllButtons(this);
		SwitchCategory(BagCategory.All);
	}

	public override void _ExitTree()
	{
		_giftAnimTween?.Kill();
		if (_data != null)
		{
			_data.InventoryChanged -= OnInventoryDataChanged;
			_data.CurrenciesChanged -= OnCurrenciesDataChanged;
			_data.GachaStateChanged -= OnGachaStateChanged;
		}
	}

	/// <summary>
	/// Đưa popup mở quà lên overlay host dùng chung của LobbyScreen để phủ trên LeftSidebar.
	/// </summary>
	public void ConfigureModalHost(Control modalHost)
	{
		if (modalHost != null && _giftModal != null && _giftModal.GetParent() != modalHost)
		{
			_giftModal.Reparent(modalHost, keepGlobalTransform: false);
		}
	}

	public void RefreshInventory()
	{
		RebuildGrid();
	}

	private void OnInventoryDataChanged()
	{
		if (IsVisibleInTree())
		{
			RebuildGrid();
		}
	}

	private void OnCurrenciesDataChanged(int coins, int rau)
	{
		if (IsVisibleInTree())
		{
			RebuildGrid();
		}
	}

	private void OnGachaStateChanged(int tickets, int pityCount)
	{
		if (IsVisibleInTree())
		{
			RebuildGrid();
		}
	}

	private void InitStyles()
	{
		_styleFilterActive = MakeRoundedStyle(new Color(0.95f, 0.72f, 0.16f, 1f), new Color(1.0f, 0.90f, 0.45f, 1f), 2, 12, padX: 16, padY: 10);
		_styleFilterInactive = MakeRoundedStyle(new Color(0.25f, 0.18f, 0.44f, 0.95f), new Color(0.50f, 0.38f, 0.75f, 0.65f), 2, 12, padX: 16, padY: 10);

		_styleSlotNormal = MakeRoundedStyle(new Color(0.20f, 0.13f, 0.36f, 0.96f), new Color(0.45f, 0.35f, 0.68f, 0.55f), 2, 12);
		_styleSlotSelected = MakeRoundedStyle(new Color(0.28f, 0.19f, 0.48f, 1f), new Color(1.0f, 0.84f, 0.25f, 1f), 3, 12);

		_styleActionPrimary = MakeRoundedStyle(new Color(0.95f, 0.70f, 0.16f, 1f), new Color(0.55f, 0.25f, 0.05f, 1f), 2, 12);
		_styleActionSecondary = MakeRoundedStyle(new Color(0.24f, 0.50f, 0.88f, 1f), new Color(0.55f, 0.80f, 1.0f, 0.8f), 2, 12);
		_styleActionGift = MakeRoundedStyle(new Color(0.16f, 0.70f, 0.36f, 1f), new Color(0.48f, 0.96f, 0.64f, 0.9f), 2, 12);
	}

	private static StyleBoxFlat MakeRoundedStyle(Color bg, Color border, int borderWidth, int radius, int padX = 14, int padY = 8) => new()
	{
		BgColor = bg,
		BorderWidthLeft = borderWidth,
		BorderWidthTop = borderWidth,
		BorderWidthRight = borderWidth,
		BorderWidthBottom = borderWidth,
		BorderColor = border,
		CornerRadiusTopLeft = radius,
		CornerRadiusTopRight = radius,
		CornerRadiusBottomRight = radius,
		CornerRadiusBottomLeft = radius,
		ContentMarginLeft = padX,
		ContentMarginTop = padY,
		ContentMarginRight = padX,
		ContentMarginBottom = padY
	};

	private void SwitchCategory(BagCategory category)
	{
		_currentCategory = category;
		for (int i = 0; i < _filterButtons.Length; i++)
		{
			var btn = _filterButtons[i];
			if (btn == null) continue;
			bool active = i == (int)category;
			var style = active ? _styleFilterActive : _styleFilterInactive;
			btn.AddThemeStyleboxOverride("normal", style);
			btn.AddThemeStyleboxOverride("hover", style);
			btn.AddThemeStyleboxOverride("pressed", style);
			btn.AddThemeColorOverride("font_color", active ? new Color(0.20f, 0.10f, 0.04f, 1f) : new Color(0.92f, 0.90f, 0.98f, 1f));
			btn.AddThemeColorOverride("font_hover_color", active ? new Color(0.20f, 0.10f, 0.04f, 1f) : Colors.White);
			btn.AddThemeColorOverride("font_pressed_color", active ? new Color(0.20f, 0.10f, 0.04f, 1f) : Colors.White);
		}

		RebuildGrid();
	}

	private List<BagEntry> CollectEntries()
	{
		var all = new List<BagEntry>();

		// 1. Túi quà đang có
		foreach (var bag in GameCatalog.GiftBags)
		{
			int qty = _data?.GetItemQuantity(bag.Id) ?? 0;
			if (qty <= 0) continue;

			all.Add(new BagEntry
			{
				Id = bag.Id,
				Category = BagCategory.GiftBags,
				Name = bag.Name,
				Tag = bag.Tag,
				Summary = bag.Summary,
				Description = bag.Description,
				IconPath = bag.IconPath,
				IconTint = bag.IconTint,
				Quantity = qty,
				BadgeText = $"x{qty}",
				AccentColor = new Color(1.0f, 0.56f, 0.78f, 1f)
			});
		}

		// 2. Tài nguyên (Vé Rương, Vàng, Rau Sạch)
		int tickets = _data?.GachaTickets ?? 0;
		all.Add(new BagEntry
		{
			Id = "res_gacha_ticket",
			Category = BagCategory.Resources,
			Name = "Vé Rương May Mắn",
			Tag = "Tài nguyên",
			Summary = "Dùng mở Rương May Mắn",
			Description = "Sử dụng tại tab Sự kiện -> Rương May Mắn để rút thưởng Vàng, Rau và vật phẩm hiếm.",
			IconPath = "res://assets/sprites/icons/ticket.png",
			IconTint = Colors.White,
			Quantity = tickets,
			BadgeText = $"x{tickets}",
			AccentColor = new Color(1.0f, 0.78f, 0.25f, 1f)
		});

		int coins = _data?.Coins ?? 0;
		all.Add(new BagEntry
		{
			Id = "res_coins",
			Category = BagCategory.Resources,
			Name = "Vàng Chợ",
			Tag = "Tài nguyên",
			Summary = "Tiền tệ chính mua sắm & nâng cấp",
			Description = "Dùng để mua vật phẩm ở Cửa hàng, mở khóa và nâng cấp chỉ số nhân vật.",
			IconPath = "res://assets/sprites/icons/coin.png",
			IconTint = Colors.White,
			Quantity = coins,
			BadgeText = $"{coins:N0}",
			AccentColor = new Color(1.0f, 0.84f, 0.22f, 1f)
		});

		int rau = _data?.Rau ?? 0;
		all.Add(new BagEntry
		{
			Id = "res_rau",
			Category = BagCategory.Resources,
			Name = "Rau Sạch",
			Tag = "Tài nguyên",
			Summary = "Tài nguyên quý hiếm",
			Description = "Dùng để đổi vật phẩm đặc biệt, mở khóa nhân vật/trang phục và giao dịch Chợ phiên.",
			IconPath = "res://assets/sprites/icons/rau.png",
			IconTint = Colors.White,
			Quantity = rau,
			BadgeText = $"{rau:N0}",
			AccentColor = new Color(0.45f, 0.94f, 0.45f, 1f)
		});

		// 3. Vật phẩm & Đồ tiêu thụ đã sở hữu (Trang phục không nằm trong Túi đồ)
		foreach (var item in GameCatalog.Consumables)
		{
			int qty = _data?.GetItemQuantity(item.Id) ?? 0;
			if (qty <= 0) continue;

			all.Add(new BagEntry
			{
				Id = item.Id,
				Category = item.IsConsumable ? BagCategory.Consumables : BagCategory.Items,
				Name = item.Name,
				Tag = item.IsConsumable ? "Tiêu thụ" : item.Tag,
				Summary = item.Summary,
				Description = item.Description,
				IconPath = item.IconPath,
				IconTint = item.IconTint,
				Quantity = qty,
				BadgeText = $"x{qty}",
				AccentColor = item.IsConsumable
					? new Color(0.45f, 0.95f, 0.65f, 1f)
					: new Color(0.35f, 0.85f, 0.98f, 1f)
			});
		}

		if (_currentCategory == BagCategory.All)
		{
			return all;
		}

		if (_currentCategory == BagCategory.Recent)
		{
			var byId = new Dictionary<string, BagEntry>();
			foreach (var entry in all)
			{
				byId[entry.Id] = entry;
			}

			var recentList = new List<BagEntry>();
			var added = new HashSet<string>();
			if (_data?.RecentItemIds != null)
			{
				foreach (var recentId in _data.RecentItemIds)
				{
					if (byId.TryGetValue(recentId, out var entry) && added.Add(recentId))
					{
						recentList.Add(entry);
					}
				}
			}

			foreach (var entry in all)
			{
				if (entry.Id != "res_coins" && entry.Id != "res_rau" && added.Add(entry.Id))
				{
					recentList.Add(entry);
				}
			}
			return recentList;
		}

		var filtered = new List<BagEntry>();
		foreach (var entry in all)
		{
			if (entry.Category == _currentCategory)
			{
				filtered.Add(entry);
			}
		}
		return filtered;
	}

	private void RebuildGrid()
	{
		if (_itemsGrid == null) return;

		foreach (Node child in _itemsGrid.GetChildren())
		{
			child.QueueFree();
		}

		_visibleEntries.Clear();
		_visibleEntries.AddRange(CollectEntries());

		bool hasItems = _visibleEntries.Count > 0;
		_itemsGrid.Visible = hasItems;
		if (_emptyStateBox != null) _emptyStateBox.Visible = !hasItems;

		if (!hasItems)
		{
			if (_emptyLabel != null)
			{
				_emptyLabel.Text = _currentCategory switch
				{
					BagCategory.Consumables => "Bạn chưa có vật phẩm tiêu thụ nào.\nHãy ghé Cửa hàng để mua trà đá, nước mía hoặc bùa bổ trợ!",
					BagCategory.Items => "Bạn chưa có vật phẩm bổ trợ nào.\nHãy ghé Cửa hàng để trang bị thêm!",
					BagCategory.GiftBags => "Bạn chưa có túi quà nào.\nHãy mở Rương May Mắn ở tab Sự kiện để săn Túi Lộc!",
					BagCategory.Recent => "Chưa có vật phẩm nào mới nhận gần đây.",
					_ => "Túi đồ hiện đang trống."
				};
			}
			if (_emptyShopBtn != null)
			{
				_emptyShopBtn.Text = _currentCategory == BagCategory.GiftBags ? "Đi tới Sự kiện" : "Ghé Cửa hàng";
			}
			_selectedEntry = null;
			_selectedEntryId = null;
			UpdateInspector();
			return;
		}

		BagEntry nextSelected = null;
		foreach (var entry in _visibleEntries)
		{
			if (entry.Id == _selectedEntryId)
			{
				nextSelected = entry;
				break;
			}
		}
		nextSelected ??= _visibleEntries[0];
		_selectedEntry = nextSelected;
		_selectedEntryId = nextSelected.Id;

		foreach (var entry in _visibleEntries)
		{
			bool isSelected = entry.Id == _selectedEntryId;
			_itemsGrid.AddChild(BuildSlotCard(entry, isSelected));
		}

		UpdateInspector();
	}

	private Control BuildSlotCard(BagEntry entry, bool isSelected)
	{
		var btn = new Button
		{
			CustomMinimumSize = new Vector2(236, 224),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			FocusMode = FocusModeEnum.None,
			MouseDefaultCursorShape = CursorShape.PointingHand
		};

		var style = isSelected ? _styleSlotSelected : _styleSlotNormal;
		btn.AddThemeStyleboxOverride("normal", style);
		btn.AddThemeStyleboxOverride("hover", _styleSlotSelected);
		btn.AddThemeStyleboxOverride("pressed", _styleSlotSelected);
		btn.Pressed += () => SelectEntry(entry.Id);

		var margin = new MarginContainer();
		margin.SetAnchorsPreset(LayoutPreset.FullRect);
		margin.MouseFilter = MouseFilterEnum.Ignore;
		margin.AddThemeConstantOverride("margin_left", 14);
		margin.AddThemeConstantOverride("margin_top", 12);
		margin.AddThemeConstantOverride("margin_right", 14);
		margin.AddThemeConstantOverride("margin_bottom", 12);
		btn.AddChild(margin);

		var vbox = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		vbox.AddThemeConstantOverride("separation", 8);
		margin.AddChild(vbox);

		var topRow = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		vbox.AddChild(topRow);

		var tagLabel = new Label
		{
			Text = entry.Tag,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore
		};
		tagLabel.AddThemeFontOverride("font", OrcishFont);
		tagLabel.AddThemeFontSizeOverride("font_size", 18);
		tagLabel.AddThemeColorOverride("font_color", entry.AccentColor);
		topRow.AddChild(tagLabel);

		var badgeLabel = new Label
		{
			Text = entry.BadgeText ?? $"x{entry.Quantity}",
			HorizontalAlignment = HorizontalAlignment.Right,
			MouseFilter = MouseFilterEnum.Ignore
		};
		badgeLabel.AddThemeFontOverride("font", OrcishFont);
		badgeLabel.AddThemeFontSizeOverride("font_size", 20);
		badgeLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.88f, 0.35f, 1f));
		topRow.AddChild(badgeLabel);

		var iconBox = new PanelContainer
		{
			CustomMinimumSize = new Vector2(104, 104),
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
			MouseFilter = MouseFilterEnum.Ignore
		};
		iconBox.AddThemeStyleboxOverride("panel", MakeRoundedStyle(new Color(0.11f, 0.06f, 0.22f, 0.95f), entry.AccentColor * new Color(1, 1, 1, 0.45f), 2, 12));
		vbox.AddChild(iconBox);

		var iconTex = new TextureRect
		{
			Texture = LoadIcon(entry.IconPath),
			Modulate = entry.IconTint,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			MouseFilter = MouseFilterEnum.Ignore
		};
		iconBox.AddChild(iconTex);

		var nameLabel = new Label
		{
			Text = entry.Name,
			HorizontalAlignment = HorizontalAlignment.Center,
			ClipText = true,
			MouseFilter = MouseFilterEnum.Ignore
		};
		nameLabel.AddThemeFontOverride("font", OrcishFont);
		nameLabel.AddThemeFontSizeOverride("font_size", 23);
		nameLabel.AddThemeColorOverride("font_color", Colors.White);
		vbox.AddChild(nameLabel);

		UIButtonHelper.AttachBounceEffect(btn);
		return btn;
	}

	private void SelectEntry(string entryId)
	{
		_selectedEntryId = entryId;
		RebuildGrid();
	}

	private void UpdateInspector()
	{
		bool hasSelection = _selectedEntry != null;
		if (_detailContent != null) _detailContent.Visible = hasSelection;
		if (_detailEmptyHint != null) _detailEmptyHint.Visible = !hasSelection;
		if (!hasSelection) return;

		if (_detailTagLabel != null)
		{
			_detailTagLabel.Text = _selectedEntry.Tag;
			_detailTagLabel.AddThemeColorOverride("font_color", _selectedEntry.AccentColor);
		}
		if (_detailIcon != null)
		{
			_detailIcon.Texture = LoadIcon(_selectedEntry.IconPath);
			_detailIcon.Modulate = _selectedEntry.IconTint;
		}
		if (_detailTitle != null)
		{
			_detailTitle.Text = _selectedEntry.Name;
		}
		if (_detailQtyLabel != null)
		{
			_detailQtyLabel.Text = (_selectedEntry.Id == "res_coins" || _selectedEntry.Id == "res_rau")
				? $"Hiện có: {_selectedEntry.Quantity:N0}"
				: $"Số lượng sở hữu: x{_selectedEntry.Quantity}";
			_detailQtyLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.88f, 0.38f, 1f));
		}
		if (_detailSummary != null)
		{
			_detailSummary.Text = _selectedEntry.Summary;
		}
		if (_detailDesc != null)
		{
			_detailDesc.Text = _selectedEntry.Description;
		}

		if (_detailActionBtn != null)
		{
			_detailActionBtn.Disabled = false;
			if (_selectedEntry.Category == BagCategory.GiftBags)
			{
				_detailActionBtn.Text = "Mở Túi Quà";
				ApplyBtnStyle(_detailActionBtn, _styleActionGift, Colors.White);
			}
			else if (_selectedEntry.Id == "res_gacha_ticket")
			{
				_detailActionBtn.Text = "Mở Rương May Mắn";
				ApplyBtnStyle(_detailActionBtn, _styleActionPrimary, new Color(0.20f, 0.10f, 0.04f, 1f));
			}
			else if (_selectedEntry.Category == BagCategory.Resources)
			{
				_detailActionBtn.Text = "Đổi thêm ở Cửa hàng";
				ApplyBtnStyle(_detailActionBtn, _styleActionSecondary, Colors.White);
			}
			else
			{
				_detailActionBtn.Text = "Mua thêm ở Cửa hàng";
				ApplyBtnStyle(_detailActionBtn, _styleActionSecondary, Colors.White);
			}
		}
	}

	private static void ApplyBtnStyle(Button btn, StyleBoxFlat style, Color textColor)
	{
		btn.AddThemeStyleboxOverride("normal", style);
		btn.AddThemeStyleboxOverride("hover", style);
		btn.AddThemeStyleboxOverride("pressed", style);
		btn.AddThemeStyleboxOverride("disabled", style);
		btn.AddThemeColorOverride("font_color", textColor);
		btn.AddThemeColorOverride("font_hover_color", textColor);
		btn.AddThemeColorOverride("font_pressed_color", textColor);
		btn.AddThemeColorOverride("font_disabled_color", textColor);
	}

	private void OnDetailActionPressed()
	{
		if (_selectedEntry == null) return;

		if (_selectedEntry.Category == BagCategory.GiftBags)
		{
			var openedBagName = _selectedEntry.Name;
			var openedIconPath = _selectedEntry.IconPath;
			var openedIconTint = _selectedEntry.IconTint;

			if (_data != null && _data.TryOpenGiftBag(_selectedEntry.Id, out var rewards, out _))
			{
				RebuildGrid();
				PlayGiftRewardAnimation(openedBagName, openedIconPath, openedIconTint, rewards);
			}
		}
		else if (_selectedEntry.Id == "res_gacha_ticket")
		{
			EmitSignal(SignalName.GoToEventsRequested);
		}
		else if (_selectedEntry.Category == BagCategory.Resources)
		{
			EmitSignal(SignalName.GoToShopRequested, 3);
		}
		else
		{
			EmitSignal(SignalName.GoToShopRequested, 1);
		}
	}

	private void PlayGiftRewardAnimation(string bagName, string iconPath, Color iconTint, List<PlayerDataManager.GiftRewardEntry> rewards)
	{
		if (_giftModal == null) return;

		_giftAnimTween?.Kill();
		_canDismissGiftModal = false;
		_giftModal.Visible = true;
		_giftModal.MoveToFront();

		if (_giftBackdrop != null) _giftBackdrop.Modulate = new Color(1, 1, 1, 0);
		if (_chestStage != null) _chestStage.Visible = true;
		if (_rewardDialog != null) _rewardDialog.Visible = false;

		if (_openingIcon != null)
		{
			_openingIcon.Texture = LoadIcon(iconPath);
			_openingIcon.Modulate = iconTint;
			_openingIcon.PivotOffset = _openingIcon.CustomMinimumSize / 2f;
			_openingIcon.Scale = new Vector2(0.45f, 0.45f);
			_openingIcon.RotationDegrees = 0f;
		}
		if (_glowHalo != null)
		{
			_glowHalo.PivotOffset = _glowHalo.CustomMinimumSize / 2f;
			_glowHalo.Scale = new Vector2(0.55f, 0.55f);
			_glowHalo.Modulate = new Color(1, 1, 1, 0.4f);
		}
		if (_openingLabel != null)
		{
			_openingLabel.Text = $"Đang mở {bagName}...";
			_openingLabel.Modulate = Colors.White;
		}

		if (_bagSourceLabel != null) _bagSourceLabel.Text = $"Phần thưởng từ {bagName}";

		var cardList = new List<Control>();
		if (_rewardsRow != null)
		{
			foreach (Node child in _rewardsRow.GetChildren())
			{
				child.QueueFree();
			}

			foreach (var r in rewards)
			{
				var card = BuildGiftRewardCard(r);
				card.Modulate = new Color(1, 1, 1, 0);
				card.Scale = new Vector2(0.4f, 0.4f);
				_rewardsRow.AddChild(card);
				cardList.Add(card);
			}
		}

		// Bước 1: Fade nền + rung lắc túi quà như rương kho báu
		_giftAnimTween = CreateTween();
		if (_giftBackdrop != null)
		{
			_giftAnimTween.TweenProperty(_giftBackdrop, "modulate:a", 1f, 0.16f);
		}

		if (_openingIcon != null)
		{
			_giftAnimTween.Parallel().TweenProperty(_openingIcon, "scale", new Vector2(1.08f, 1.08f), 0.18f)
				.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
			if (_glowHalo != null)
			{
				_giftAnimTween.Parallel().TweenProperty(_glowHalo, "scale", new Vector2(1.10f, 1.10f), 0.18f)
					.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
			}

			float[] angles = { -11f, 12f, -15f, 15f, -9f, 7f, 0f };
			for (int i = 0; i < angles.Length; i++)
			{
				var squash = (i % 2 == 0) ? new Vector2(1.10f, 0.92f) : new Vector2(0.92f, 1.12f);
				if (i == angles.Length - 1) squash = Vector2.One;

				_giftAnimTween.TweenProperty(_openingIcon, "rotation_degrees", angles[i], 0.065f);
				_giftAnimTween.Parallel().TweenProperty(_openingIcon, "scale", squash, 0.065f);
				if (_glowHalo != null)
				{
					float haloScale = 1.10f + 0.05f * (i + 1);
					_giftAnimTween.Parallel().TweenProperty(_glowHalo, "scale", new Vector2(haloScale, haloScale), 0.065f);
				}
			}

			// Bước 2: Bung sáng mở túi
			_giftAnimTween.TweenProperty(_openingIcon, "scale", new Vector2(1.42f, 1.42f), 0.14f)
				.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
			_giftAnimTween.Parallel().TweenProperty(_openingIcon, "modulate:a", 0f, 0.14f);
			if (_openingLabel != null)
			{
				_giftAnimTween.Parallel().TweenProperty(_openingLabel, "modulate:a", 0f, 0.12f);
			}
			if (_glowHalo != null)
			{
				_giftAnimTween.Parallel().TweenProperty(_glowHalo, "scale", new Vector2(2.1f, 2.1f), 0.16f);
				_giftAnimTween.Parallel().TweenProperty(_glowHalo, "modulate:a", 0f, 0.16f);
			}
		}

		// Bước 3: Hiện bảng phần thưởng + bung từng thẻ quà
		_giftAnimTween.TweenCallback(Callable.From(() =>
		{
			if (_chestStage != null) _chestStage.Visible = false;
			if (_rewardDialog != null)
			{
				_rewardDialog.Visible = true;
				_rewardDialog.PivotOffset = _rewardDialog.Size / 2f;
				_rewardDialog.Scale = new Vector2(0.84f, 0.84f);
				_rewardDialog.Modulate = new Color(1, 1, 1, 0);

				var revealTween = CreateTween().SetParallel(true);
				revealTween.TweenProperty(_rewardDialog, "scale", Vector2.One, 0.24f)
					.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
				revealTween.TweenProperty(_rewardDialog, "modulate:a", 1f, 0.18f);

				for (int i = 0; i < cardList.Count; i++)
				{
					var card = cardList[i];
					float delay = 0.08f + i * 0.11f;
					card.PivotOffset = card.CustomMinimumSize / 2f;
					revealTween.TweenProperty(card, "scale", Vector2.One, 0.28f)
						.SetDelay(delay).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
					revealTween.TweenProperty(card, "modulate:a", 1f, 0.18f)
						.SetDelay(delay);
				}

				revealTween.Chain().TweenCallback(Callable.From(() => _canDismissGiftModal = true));
			}
			else
			{
				_canDismissGiftModal = true;
			}
		}));
	}

	private Control BuildGiftRewardCard(PlayerDataManager.GiftRewardEntry reward)
	{
		var panel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(198, 224),
			MouseFilter = MouseFilterEnum.Ignore
		};
		panel.AddThemeStyleboxOverride("panel", MakeRoundedStyle(
			new Color(0.16f, 0.09f, 0.30f, 0.98f),
			reward.AccentColor,
			borderWidth: 2,
			radius: 14,
			padX: 12,
			padY: 14));

		var vbox = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		vbox.AddThemeConstantOverride("separation", 10);
		vbox.Alignment = BoxContainer.AlignmentMode.Center;
		panel.AddChild(vbox);

		var iconWrap = new PanelContainer
		{
			CustomMinimumSize = new Vector2(104, 104),
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
			MouseFilter = MouseFilterEnum.Ignore
		};
		iconWrap.AddThemeStyleboxOverride("panel", MakeRoundedStyle(
			new Color(0.09f, 0.04f, 0.18f, 0.95f),
			reward.AccentColor * new Color(1, 1, 1, 0.55f),
			borderWidth: 2,
			radius: 12,
			padX: 8,
			padY: 8));
		vbox.AddChild(iconWrap);

		var icon = new TextureRect
		{
			Texture = LoadIcon(reward.IconPath),
			Modulate = reward.IconTint,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			MouseFilter = MouseFilterEnum.Ignore
		};
		iconWrap.AddChild(icon);

		var amountLabel = new Label
		{
			Text = reward.AmountText,
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore
		};
		amountLabel.AddThemeFontOverride("font", OrcishFont);
		amountLabel.AddThemeFontSizeOverride("font_size", 30);
		amountLabel.AddThemeColorOverride("font_color", reward.AccentColor);
		vbox.AddChild(amountLabel);

		var nameLabel = new Label
		{
			Text = reward.Name,
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			MouseFilter = MouseFilterEnum.Ignore
		};
		nameLabel.AddThemeFontOverride("font", OrcishFont);
		nameLabel.AddThemeFontSizeOverride("font_size", 22);
		nameLabel.AddThemeColorOverride("font_color", Colors.White);
		vbox.AddChild(nameLabel);

		return panel;
	}

	private void OnGiftBackdropInput(InputEvent @event)
	{
		if (!_canDismissGiftModal) return;
		bool isClick = @event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left;
		bool isTouch = @event is InputEventScreenTouch st && st.Pressed;
		if (isClick || isTouch)
		{
			_giftBackdrop?.AcceptEvent();
			CloseGiftRewardModal();
		}
	}

	private void CloseGiftRewardModal()
	{
		if (_giftModal == null || !_giftModal.Visible) return;
		_canDismissGiftModal = false;

		_giftAnimTween?.Kill();
		_giftAnimTween = CreateTween().SetParallel(true);
		if (_giftBackdrop != null)
		{
			_giftAnimTween.TweenProperty(_giftBackdrop, "modulate:a", 0f, 0.16f);
		}
		if (_rewardDialog != null && _rewardDialog.Visible)
		{
			_rewardDialog.PivotOffset = _rewardDialog.Size / 2f;
			_giftAnimTween.TweenProperty(_rewardDialog, "scale", new Vector2(0.88f, 0.88f), 0.15f);
			_giftAnimTween.TweenProperty(_rewardDialog, "modulate:a", 0f, 0.15f);
		}
		_giftAnimTween.Chain().TweenCallback(Callable.From(() => _giftModal.Visible = false));
	}

	private void OnEmptyActionPressed()
	{
		if (_currentCategory == BagCategory.GiftBags)
		{
			EmitSignal(SignalName.GoToEventsRequested);
		}
		else
		{
			EmitSignal(SignalName.GoToShopRequested, 1);
		}
	}

	private Texture2D LoadIcon(string path)
	{
		if (string.IsNullOrEmpty(path)) return null;
		if (_iconCache.TryGetValue(path, out var cached)) return cached;
		var tex = ResourceLoader.Load<Texture2D>(path);
		_iconCache[path] = tex;
		return tex;
	}
}
