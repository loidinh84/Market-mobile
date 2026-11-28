using System;
using System.Collections.Generic;
using Godot;
using MarketMobile.Core;
using MarketMobile.Utils;

namespace MarketMobile.UI;

public partial class ShopScreen : Control
{
	private enum ShopTab
	{
		Characters = 0,
		Items = 1,
		Skins = 2,
		Exchange = 3,
		Market = 4
	}

	private enum ShopEntryKind
	{
		Character,
		ConsumableItem,
		Skin,
		ExchangePack,
		MarketTrade
	}

	private sealed class ShopEntry
	{
		public string Id { get; init; }
		public ShopTab Tab { get; init; }
		public ShopEntryKind Kind { get; init; }
		public string Name { get; init; }
		public string Tag { get; init; }
		public string Summary { get; init; }
		public string Description { get; init; }
		public int Price { get; init; }
		public CurrencyType Currency { get; init; } = CurrencyType.Gold;
		public int AltPrice { get; init; } = 0;
		public CurrencyType AltCurrency { get; init; } = CurrencyType.Rau;
		public string IconPath { get; init; }
		public Color IconTint { get; init; } = Colors.White;
		public int CharacterId { get; init; } = -1;
		public int GainCoins { get; init; } = 0;
		public int GainRau { get; init; } = 0;
		public bool IsStarterGift { get; init; } = false;
		public MarketTradeListing MarketListing { get; init; }
	}

	// Danh mục vật phẩm và trang phục dùng chung được định nghĩa tại GameCatalog.cs

	[ExportGroup("Card Prefabs")]
	[Export] private PackedScene _characterCardScene;
	[Export] private PackedScene _skinCardScene;
	[Export] private PackedScene _compactCardScene;
	[Export] private PackedScene _marketCardScene;
	[Export] private PackedScene _priceButtonScene;

	private PlayerDataManager _data;
	private CoinBar _coinBar;
	private CoinBar _rauBar;

	private Button[] _tabButtons;
	private Control _marketToolbar;
	private Button _postTradeBtn;
	private GridContainer _cardsGrid;
	private ScrollContainer _scrollContainer;

	private StyleBox _styleTabActive;
	private StyleBox _styleTabInactive;
	private StyleBoxFlat _styleBadgeOwned;
	private StyleBoxFlat _styleBadgeQty;

	private static readonly Color BtnBgGold = new(0.84f, 0.54f, 0.10f, 1f);
	private static readonly Color BtnBorderGold = new(1f, 0.92f, 0.58f, 0.98f);
	private static readonly Color BtnBgRau = new(0.13f, 0.62f, 0.36f, 1f);
	private static readonly Color BtnBorderRau = new(0.68f, 1f, 0.82f, 0.98f);
	private static readonly Color BtnBgSelect = new(0.22f, 0.49f, 0.85f, 1f);
	private static readonly Color BtnBorderSelect = new(0.72f, 0.90f, 1f, 0.95f);
	private static readonly Color BtnBgExchange = new(0.86f, 0.52f, 0.14f, 1f);
	private static readonly Color BtnBorderExchange = new(1f, 0.90f, 0.60f, 0.95f);
	private static readonly Color BtnBgCancelRed = new(0.72f, 0.22f, 0.24f, 1f);
	private static readonly Color BtnBorderCancelRed = new(1f, 0.72f, 0.72f, 0.90f);
	private static readonly Color BtnBgEquipped = new(0.27f, 0.22f, 0.40f, 0.95f);
	private static readonly Color BtnBorderEquipped = new(0.45f, 0.38f, 0.65f, 0.75f);

	private Texture2D _goldIconTexture;
	private Texture2D _rauIconTexture;
	private Texture2D _lockIconTexture;
	private readonly Dictionary<string, Texture2D> _iconCache = new();

	private Control _toastBanner;
	private Label _toastLabel;
	private Tween _toastTween;

	// Buy / Trade Confirm Modal
	private Control _buyModal;
	private Control _buyDialogBox;
	private Control _buyBackdrop;
	private Label _buyModalTitle;
	private Button _buyCloseBtn;
	private TextureRect _buyPreviewTexture;
	private Label _buyItemName;
	private Label _buyItemTag;
	private Label _buyItemDesc;
	private Control _buyQuantityRow;
	private Button _buyQtyMinusBtn;
	private Label _buyQtyValueLabel;
	private Button _buyQtyPlusBtn;
	private Label _buyPriceTitle;
	private TextureRect _buyPriceCurrencyIcon;
	private Label _buyPriceLabel;
	private Label _buyBalanceLabel;
	private Button _buyCancelBtn;
	private Button _buyConfirmBtn;
	private Tween _buyModalTween;

	// Insufficient Funds / Items Modal
	private Control _failModal;
	private Control _failDialogBox;
	private Control _failBackdrop;
	private Label _failTitleLabel;
	private Button _failCloseBtn;
	private Label _failMessageLabel;
	private Label _failCostLabel;
	private Label _failCostValue;
	private Label _failCurrentValue;
	private Label _failMissingValue;
	private Button _failCloseModalBtn;
	private Button _failGoExchangeBtn;
	private Tween _failModalTween;
	private ShopTab _failRedirectTab = ShopTab.Exchange;

	// Post Trade Modal (Chợ phiên)
	private Control _postModal;
	private Control _postDialogBox;
	private Control _postBackdrop;
	private Button _postCloseBtn;
	private OptionButton _offerItemOption;
	private Button _offerMinusBtn;
	private Label _offerQtyLabel;
	private Button _offerPlusBtn;
	private OptionButton _wantOption;
	private Button _wantMinusBtn;
	private Label _wantAmountLabel;
	private Button _wantPlusBtn;
	private Button _postCancelBtn;
	private Button _postPublishBtn;
	private Tween _postModalTween;

	private readonly List<string> _offerItemIds = new();
	private readonly List<(TradeAssetType Type, string ItemId, string Label)> _wantTargets = new();
	private int _postOfferQty = 1;
	private int _postWantAmount = 200;

	private readonly Dictionary<Control, Vector2> _modalBaseScales = new();
	private ShopTab _currentTab = ShopTab.Characters;
	private ShopEntry _pendingEntry;
	private int _pendingQuantity = 1;
	private CurrencyType _pendingUnlockCurrency = CurrencyType.Gold;
	private int _pendingUnlockPrice = 0;
	private LeftSidebar _shellSidebar;

	/// <summary>
	/// Cấu hình ShopScreen khi được nhúng trong Persistent Shell (lobby_screen):
	/// ẩn sidebar cục bộ, ẩn background trùng và đưa toast/modal lên overlay host dùng chung.
	/// </summary>
	public void ConfigureEmbeddedInShell(LeftSidebar shellSidebar, Control modalHost)
	{
		_shellSidebar = shellSidebar;
		MouseFilter = MouseFilterEnum.Ignore;

		var embeddedSidebar = GetNodeOrNull<LeftSidebar>("LeftSidebar");
		embeddedSidebar?.DisableAsEmbedded();

		var dupBg = GetNodeOrNull<Control>("Background");
		if (dupBg != null) dupBg.Visible = false;

		if (modalHost != null)
		{
			if (_toastBanner != null && _toastBanner.GetParent() != modalHost)
				_toastBanner.Reparent(modalHost, keepGlobalTransform: false);

			if (_buyModal != null && _buyModal.GetParent() != modalHost)
				_buyModal.Reparent(modalHost, keepGlobalTransform: false);

			if (_failModal != null && _failModal.GetParent() != modalHost)
				_failModal.Reparent(modalHost, keepGlobalTransform: false);

			if (_postModal != null && _postModal.GetParent() != modalHost)
				_postModal.Reparent(modalHost, keepGlobalTransform: false);
		}
	}

	/// <summary>
	/// Đồng bộ tab đích (nếu có PendingShopTabIndex) và làm mới dữ liệu khi chuyển sang tab Cửa hàng.
	/// </summary>
	public void OnActivatedInShell()
	{
		if (_data != null)
		{
			_coinBar?.SetCoins(_data.Coins, animate: false);
			_rauBar?.SetCoins(_data.Rau, animate: false);
		}

		int targetTab = _data != null ? _data.ConsumePendingShopTabIndex((int)_currentTab) : (int)_currentTab;
		if (targetTab < 0 || targetTab >= _tabButtons.Length) targetTab = 0;

		SwitchTab((ShopTab)targetTab, animate: false);
	}

	/// <summary>
	/// Ẩn nhanh modal/toast khi rời tab Cửa hàng.
	/// </summary>
	public void OnDeactivatedInShell()
	{
		_toastTween?.Kill();
		_buyModalTween?.Kill();
		_failModalTween?.Kill();
		_postModalTween?.Kill();
		if (_toastBanner != null) _toastBanner.Visible = false;
		if (_buyModal != null) _buyModal.Visible = false;
		if (_failModal != null) _failModal.Visible = false;
		if (_postModal != null) _postModal.Visible = false;
	}

	private static readonly Color ColorActiveText = Colors.White;
	private static readonly Color ColorInactiveText = new(0.18f, 0.12f, 0.32f, 1f);
	private static readonly Color ColorGoldText = new(1f, 0.85f, 0.2f, 1f);
	private static readonly Color ColorRauText = new(0.45f, 0.95f, 0.55f, 1f);
	private static readonly Color ColorErrorText = new(1f, 0.42f, 0.42f, 1f);
	private static readonly Color ColorMutedText = new(0.80f, 0.75f, 0.92f, 1f);

	public override void _Ready()
	{
		_data = PlayerDataManager.Instance ?? GetNodeOrNull<PlayerDataManager>("/root/PlayerDataManager");

		_goldIconTexture = GD.Load<Texture2D>("res://assets/sprites/icons/coin.png");
		_rauIconTexture = GD.Load<Texture2D>("res://assets/sprites/icons/rau.png");
		_lockIconTexture = GD.Load<Texture2D>("res://assets/sprites/icons/locked.png");

		_characterCardScene ??= GD.Load<PackedScene>("res://prefabs/ui/shop_character_card.tscn");
		_skinCardScene ??= GD.Load<PackedScene>("res://prefabs/ui/shop_skin_card.tscn");
		_compactCardScene ??= GD.Load<PackedScene>("res://prefabs/ui/shop_compact_card.tscn");
		_marketCardScene ??= GD.Load<PackedScene>("res://prefabs/ui/shop_market_card.tscn");
		_priceButtonScene ??= GD.Load<PackedScene>("res://prefabs/ui/shop_price_button.tscn");

		InitializeStyles();
		BindNodes();
		BindSignals();

		if (_data != null)
		{
			_coinBar?.SetCoins(_data.Coins, animate: false);
			_rauBar?.SetCoins(_data.Rau, animate: false);
			_data.CurrenciesChanged += OnSharedCurrenciesChanged;
			_data.InventoryChanged += OnInventoryChanged;
			_data.SelectedCharacterChanged += OnSelectedCharacterChanged;
		}

		int initialTab = _data != null ? _data.ConsumePendingShopTabIndex(0) : 0;
		if (initialTab < 0 || initialTab >= _tabButtons.Length) initialTab = 0;

		UIButtonHelper.AttachToAllButtons(this);
		SwitchTab((ShopTab)initialTab, animate: false);
	}

	public override void _ExitTree()
	{
		if (_data != null)
		{
			_data.CurrenciesChanged -= OnSharedCurrenciesChanged;
			_data.InventoryChanged -= OnInventoryChanged;
			_data.SelectedCharacterChanged -= OnSelectedCharacterChanged;
		}
	}

	private void InitializeStyles()
	{
		_styleBadgeOwned = CreatePillStyle(new Color(0.12f, 0.46f, 0.26f, 0.94f), new Color(0.38f, 0.90f, 0.58f, 0.90f));
		_styleBadgeQty = CreatePillStyle(new Color(0.14f, 0.36f, 0.58f, 0.94f), new Color(0.40f, 0.78f, 1.0f, 0.90f));
	}

	private static StyleBoxFlat CreatePillStyle(Color bg, Color border) => new()
	{
		BgColor = bg,
		BorderColor = border,
		BorderWidthLeft = 1,
		BorderWidthTop = 1,
		BorderWidthRight = 1,
		BorderWidthBottom = 1,
		CornerRadiusTopLeft = 6,
		CornerRadiusTopRight = 6,
		CornerRadiusBottomRight = 6,
		CornerRadiusBottomLeft = 6,
		ContentMarginLeft = 12,
		ContentMarginTop = 4,
		ContentMarginRight = 12,
		ContentMarginBottom = 4
	};

	private void BindNodes()
	{
		_coinBar = GetNodeOrNull<CoinBar>("CurrenciesHBox/CoinBar");
		_rauBar = GetNodeOrNull<CoinBar>("CurrenciesHBox/RauBar");

		_tabButtons = new[]
		{
			GetNodeOrNull<Button>("ShopPanel/MainVBox/TabsHeader/TabsMargin/SubTabsHBox/TabChars"),
			GetNodeOrNull<Button>("ShopPanel/MainVBox/TabsHeader/TabsMargin/SubTabsHBox/TabItems"),
			GetNodeOrNull<Button>("ShopPanel/MainVBox/TabsHeader/TabsMargin/SubTabsHBox/TabSkins"),
			GetNodeOrNull<Button>("ShopPanel/MainVBox/TabsHeader/TabsMargin/SubTabsHBox/TabExchange"),
			GetNodeOrNull<Button>("ShopPanel/MainVBox/TabsHeader/TabsMargin/SubTabsHBox/TabMarket")
		};

		if (_tabButtons[0] != null) _styleTabActive = _tabButtons[0].GetThemeStylebox("normal");
		if (_tabButtons[1] != null) _styleTabInactive = _tabButtons[1].GetThemeStylebox("normal");

		_marketToolbar = GetNodeOrNull<Control>("ShopPanel/MainVBox/BodyMargin/BodyVBox/MarketToolbar");
		_postTradeBtn = GetNodeOrNull<Button>("ShopPanel/MainVBox/BodyMargin/BodyVBox/MarketToolbar/PostTradeBtn");
		_scrollContainer = GetNodeOrNull<ScrollContainer>("ShopPanel/MainVBox/BodyMargin/BodyVBox/ScrollContainer");
		_cardsGrid = GetNodeOrNull<GridContainer>("ShopPanel/MainVBox/BodyMargin/BodyVBox/ScrollContainer/CardsMargin/CardsGrid");

		_toastBanner = GetNodeOrNull<Control>("ToastBanner");
		_toastLabel = GetNodeOrNull<Label>("ToastBanner/ToastLabel");
		if (_toastBanner != null) _toastBanner.Visible = false;

		// Buy / Trade Confirm Modal
		_buyModal = GetNodeOrNull<Control>("BuyConfirmModal");
		_buyBackdrop = GetNodeOrNull<Control>("BuyConfirmModal/Backdrop");
		_buyDialogBox = GetNodeOrNull<Control>("BuyConfirmModal/Center/DialogBox");
		_buyModalTitle = GetNodeOrNull<Label>("BuyConfirmModal/Center/DialogBox/DialogVBox/HeaderHBox/TitleLabel");
		_buyCloseBtn = GetNodeOrNull<Button>("BuyConfirmModal/Center/DialogBox/DialogVBox/HeaderHBox/CloseBtn");
		_buyPreviewTexture = GetNodeOrNull<TextureRect>("BuyConfirmModal/Center/DialogBox/DialogVBox/PreviewHBox/PreviewBox/ItemTexture");
		_buyItemName = GetNodeOrNull<Label>("BuyConfirmModal/Center/DialogBox/DialogVBox/PreviewHBox/InfoVBox/ItemName");
		_buyItemTag = GetNodeOrNull<Label>("BuyConfirmModal/Center/DialogBox/DialogVBox/PreviewHBox/InfoVBox/ItemTag");
		_buyItemDesc = GetNodeOrNull<Label>("BuyConfirmModal/Center/DialogBox/DialogVBox/PreviewHBox/InfoVBox/ItemDesc");
		_buyQuantityRow = GetNodeOrNull<Control>("BuyConfirmModal/Center/DialogBox/DialogVBox/QuantityRow");
		_buyQtyMinusBtn = GetNodeOrNull<Button>("BuyConfirmModal/Center/DialogBox/DialogVBox/QuantityRow/MinusBtn");
		_buyQtyValueLabel = GetNodeOrNull<Label>("BuyConfirmModal/Center/DialogBox/DialogVBox/QuantityRow/QtyValueLabel");
		_buyQtyPlusBtn = GetNodeOrNull<Button>("BuyConfirmModal/Center/DialogBox/DialogVBox/QuantityRow/PlusBtn");
		_buyPriceTitle = GetNodeOrNull<Label>("BuyConfirmModal/Center/DialogBox/DialogVBox/PriceHBox/PriceTitle");
		_buyPriceCurrencyIcon = GetNodeOrNull<TextureRect>("BuyConfirmModal/Center/DialogBox/DialogVBox/PriceHBox/CurrencyIcon");
		_buyPriceLabel = GetNodeOrNull<Label>("BuyConfirmModal/Center/DialogBox/DialogVBox/PriceHBox/PriceLabel");
		_buyBalanceLabel = GetNodeOrNull<Label>("BuyConfirmModal/Center/DialogBox/DialogVBox/BalanceLabel");
		_buyCancelBtn = GetNodeOrNull<Button>("BuyConfirmModal/Center/DialogBox/DialogVBox/ActionsHBox/CancelBtn");
		_buyConfirmBtn = GetNodeOrNull<Button>("BuyConfirmModal/Center/DialogBox/DialogVBox/ActionsHBox/BuyBtn");
		if (_buyModal != null) _buyModal.Visible = false;

		// Insufficient Modal
		_failModal = GetNodeOrNull<Control>("InsufficientFundsModal");
		_failBackdrop = GetNodeOrNull<Control>("InsufficientFundsModal/Backdrop");
		_failDialogBox = GetNodeOrNull<Control>("InsufficientFundsModal/Center/DialogBox");
		_failTitleLabel = GetNodeOrNull<Label>("InsufficientFundsModal/Center/DialogBox/DialogVBox/HeaderHBox/TitleLabel");
		_failCloseBtn = GetNodeOrNull<Button>("InsufficientFundsModal/Center/DialogBox/DialogVBox/HeaderHBox/CloseBtn");
		_failMessageLabel = GetNodeOrNull<Label>("InsufficientFundsModal/Center/DialogBox/DialogVBox/MessageLabel");
		_failCostLabel = GetNodeOrNull<Label>("InsufficientFundsModal/Center/DialogBox/DialogVBox/DetailBox/DetailVBox/CostRow/Label");
		_failCostValue = GetNodeOrNull<Label>("InsufficientFundsModal/Center/DialogBox/DialogVBox/DetailBox/DetailVBox/CostRow/Value");
		_failCurrentValue = GetNodeOrNull<Label>("InsufficientFundsModal/Center/DialogBox/DialogVBox/DetailBox/DetailVBox/CurrentRow/Value");
		_failMissingValue = GetNodeOrNull<Label>("InsufficientFundsModal/Center/DialogBox/DialogVBox/DetailBox/DetailVBox/MissingRow/Value");
		_failCloseModalBtn = GetNodeOrNull<Button>("InsufficientFundsModal/Center/DialogBox/DialogVBox/ActionsHBox/CloseModalBtn");
		_failGoExchangeBtn = GetNodeOrNull<Button>("InsufficientFundsModal/Center/DialogBox/DialogVBox/ActionsHBox/GoToExchangeBtn");
		if (_failModal != null) _failModal.Visible = false;

		// Post Trade Modal
		_postModal = GetNodeOrNull<Control>("PostTradeModal");
		_postBackdrop = GetNodeOrNull<Control>("PostTradeModal/Backdrop");
		_postDialogBox = GetNodeOrNull<Control>("PostTradeModal/Center/DialogBox");
		_postCloseBtn = GetNodeOrNull<Button>("PostTradeModal/Center/DialogBox/DialogVBox/HeaderHBox/CloseBtn");
		_offerItemOption = GetNodeOrNull<OptionButton>("PostTradeModal/Center/DialogBox/DialogVBox/OfferBox/OfferVBox/OfferRow/OfferItemOption");
		_offerMinusBtn = GetNodeOrNull<Button>("PostTradeModal/Center/DialogBox/DialogVBox/OfferBox/OfferVBox/OfferRow/OfferMinusBtn");
		_offerQtyLabel = GetNodeOrNull<Label>("PostTradeModal/Center/DialogBox/DialogVBox/OfferBox/OfferVBox/OfferRow/OfferQtyLabel");
		_offerPlusBtn = GetNodeOrNull<Button>("PostTradeModal/Center/DialogBox/DialogVBox/OfferBox/OfferVBox/OfferRow/OfferPlusBtn");
		_wantOption = GetNodeOrNull<OptionButton>("PostTradeModal/Center/DialogBox/DialogVBox/WantBox/WantVBox/WantRow/WantOption");
		_wantMinusBtn = GetNodeOrNull<Button>("PostTradeModal/Center/DialogBox/DialogVBox/WantBox/WantVBox/WantRow/WantMinusBtn");
		_wantAmountLabel = GetNodeOrNull<Label>("PostTradeModal/Center/DialogBox/DialogVBox/WantBox/WantVBox/WantRow/WantAmountLabel");
		_wantPlusBtn = GetNodeOrNull<Button>("PostTradeModal/Center/DialogBox/DialogVBox/WantBox/WantVBox/WantRow/WantPlusBtn");
		_postCancelBtn = GetNodeOrNull<Button>("PostTradeModal/Center/DialogBox/DialogVBox/ActionsHBox/CancelBtn");
		_postPublishBtn = GetNodeOrNull<Button>("PostTradeModal/Center/DialogBox/DialogVBox/ActionsHBox/PublishBtn");
		if (_postModal != null) _postModal.Visible = false;

		ConfigureOptionDropdown(_offerItemOption);
		ConfigureOptionDropdown(_wantOption);
	}

	private static void ConfigureOptionDropdown(OptionButton option)
	{
		if (option == null) return;

		option.AddThemeConstantOverride("arrow_margin", 16);
		option.AddThemeConstantOverride("h_separation", 12);

		var popup = option.GetPopup();
		if (popup == null) return;

		popup.TransparentBg = true;

		var panelStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.14f, 0.09f, 0.28f, 0.98f),
			BorderColor = new Color(0.58f, 0.46f, 0.90f, 0.95f),
			BorderWidthLeft = 2,
			BorderWidthTop = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 2,
			CornerRadiusTopLeft = 12,
			CornerRadiusTopRight = 12,
			CornerRadiusBottomRight = 12,
			CornerRadiusBottomLeft = 12,
			ContentMarginLeft = 8,
			ContentMarginTop = 8,
			ContentMarginRight = 8,
			ContentMarginBottom = 8,
			ShadowColor = new Color(0f, 0f, 0f, 0.45f),
			ShadowSize = 8
		};

		var hoverStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.32f, 0.22f, 0.58f, 0.96f),
			BorderColor = new Color(0.72f, 0.60f, 1.0f, 0.90f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomRight = 8,
			CornerRadiusBottomLeft = 8,
			ContentMarginLeft = 12,
			ContentMarginTop = 6,
			ContentMarginRight = 12,
			ContentMarginBottom = 6
		};

		popup.AddThemeStyleboxOverride("panel", panelStyle);
		popup.AddThemeStyleboxOverride("hover", hoverStyle);
		popup.AddThemeColorOverride("font_color", Colors.White);
		popup.AddThemeColorOverride("font_hover_color", new Color(1f, 0.90f, 0.45f, 1f));
		popup.AddThemeFontSizeOverride("font_size", 26);
		popup.AddThemeConstantOverride("v_separation", 12);
		popup.AddThemeConstantOverride("item_start_padding", 14);
		popup.AddThemeConstantOverride("item_end_padding", 14);
		popup.MaxSize = new Vector2I(540, 360);

		option.Pressed += () =>
		{
			popup.MinSize = new Vector2I(Mathf.Max(220, Mathf.RoundToInt(option.Size.X)), 0);
		};
	}

	private static void StripDropdownRadioCircles(OptionButton option)
	{
		var popup = option?.GetPopup();
		if (popup == null) return;

		for (int i = 0; i < popup.ItemCount; i++)
		{
			popup.SetItemAsRadioCheckable(i, false);
		}
	}

	private void BindSignals()
	{
		for (int i = 0; i < _tabButtons.Length; i++)
		{
			int tabIdx = i;
			if (_tabButtons[i] != null)
			{
				_tabButtons[i].Pressed += () => SwitchTab((ShopTab)tabIdx);
			}
		}

		if (_coinBar != null) _coinBar.AddCoinPressed += () => SwitchTab(ShopTab.Exchange);
		if (_rauBar != null) _rauBar.AddCoinPressed += () => SwitchTab(ShopTab.Exchange);
		if (_postTradeBtn != null) _postTradeBtn.Pressed += OpenPostTradeModal;

		if (_buyCloseBtn != null) _buyCloseBtn.Pressed += CloseBuyModal;
		if (_buyCancelBtn != null) _buyCancelBtn.Pressed += CloseBuyModal;
		if (_buyConfirmBtn != null) _buyConfirmBtn.Pressed += ConfirmPendingPurchase;
		if (_buyQtyMinusBtn != null) _buyQtyMinusBtn.Pressed += () => ChangePendingQuantity(-1);
		if (_buyQtyPlusBtn != null) _buyQtyPlusBtn.Pressed += () => ChangePendingQuantity(1);
		ConfigureModalOutsideClick(_buyModal, _buyBackdrop, _buyDialogBox, CloseBuyModal);

		if (_failCloseBtn != null) _failCloseBtn.Pressed += CloseFailModal;
		if (_failCloseModalBtn != null) _failCloseModalBtn.Pressed += CloseFailModal;
		if (_failGoExchangeBtn != null) _failGoExchangeBtn.Pressed += OnGoToRedirectTabClicked;
		ConfigureModalOutsideClick(_failModal, _failBackdrop, _failDialogBox, CloseFailModal);

		if (_postCloseBtn != null) _postCloseBtn.Pressed += ClosePostTradeModal;
		if (_postCancelBtn != null) _postCancelBtn.Pressed += ClosePostTradeModal;
		if (_postPublishBtn != null) _postPublishBtn.Pressed += PublishPlayerTrade;
		if (_offerItemOption != null) _offerItemOption.ItemSelected += (_) => ResetPostOfferQty();
		if (_offerMinusBtn != null) _offerMinusBtn.Pressed += () => AdjustPostOfferQty(-1);
		if (_offerPlusBtn != null) _offerPlusBtn.Pressed += () => AdjustPostOfferQty(1);
		if (_wantOption != null) _wantOption.ItemSelected += (_) => ResetPostWantAmount();
		if (_wantMinusBtn != null) _wantMinusBtn.Pressed += () => AdjustPostWantAmount(-1);
		if (_wantPlusBtn != null) _wantPlusBtn.Pressed += () => AdjustPostWantAmount(1);
		ConfigureModalOutsideClick(_postModal, _postBackdrop, _postDialogBox, ClosePostTradeModal);
	}

	private static void ConfigureModalOutsideClick(Control modalRoot, Control backdrop, Control dialogBox, Action onDismiss)
	{
		if (modalRoot == null) return;

		var center = modalRoot.GetNodeOrNull<Control>("Center");
		if (center != null) center.MouseFilter = MouseFilterEnum.Ignore;
		if (dialogBox != null) dialogBox.MouseFilter = MouseFilterEnum.Stop;

		if (backdrop == null) return;
		backdrop.MouseFilter = MouseFilterEnum.Stop;
		backdrop.GuiInput += (InputEvent @event) =>
		{
			bool isLeftClick = @event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left;
			bool isTouch = @event is InputEventScreenTouch st && st.Pressed;
			if (isLeftClick || isTouch)
			{
				backdrop.AcceptEvent();
				onDismiss?.Invoke();
			}
		};
	}

	private void SwitchTab(ShopTab tab, bool animate = true)
	{
		_currentTab = tab;

		for (int i = 0; i < _tabButtons.Length; i++)
		{
			var btn = _tabButtons[i];
			if (btn == null) continue;

			bool isActive = i == (int)tab;
			var style = isActive ? _styleTabActive : _styleTabInactive;
			var color = isActive ? ColorActiveText : ColorInactiveText;

			if (style != null)
			{
				btn.AddThemeStyleboxOverride("normal", style);
				btn.AddThemeStyleboxOverride("pressed", style);
				btn.AddThemeStyleboxOverride("hover", style);
				btn.AddThemeStyleboxOverride("hover_pressed", style);
			}
			btn.AddThemeColorOverride("font_color", color);
			btn.AddThemeColorOverride("font_hover_color", color);
			btn.AddThemeColorOverride("font_pressed_color", color);
			btn.AddThemeColorOverride("font_focus_color", color);
			btn.AddThemeFontSizeOverride("font_size", isActive ? 38 : 35);
		}

		if (_marketToolbar != null)
		{
			_marketToolbar.Visible = tab == ShopTab.Market;
		}

		if (_scrollContainer != null)
		{
			_scrollContainer.ScrollVertical = 0;
		}

		RebuildGrid(animate);
	}

	private List<ShopEntry> BuildCatalogForTab(ShopTab tab)
	{
		var list = new List<ShopEntry>();
		switch (tab)
		{
			case ShopTab.Characters:
				var characters = _data?.Characters ?? PlayerDataManager.CreateDefaultCharacters();
				foreach (var ch in characters)
				{
					list.Add(new ShopEntry
					{
						Id = $"char_{ch.Id}",
						Tab = ShopTab.Characters,
						Kind = ShopEntryKind.Character,
						Name = ch.Name,
						Tag = ch.Role,
						Summary = $"ST {ch.Damage} · Máu {ch.Health} · Thủ {ch.Defense}%",
						Description = $"Vai trò {ch.Role}. Mở khóa để chọn ra trận và nâng cấp chỉ số tại tab Nhân vật.",
						Price = ch.Price,
						Currency = ch.Currency,
						AltPrice = ch.AltPrice,
						AltCurrency = ch.AltCurrency,
						IconPath = "res://assets/sprites/icons/avatar_character.png",
						IconTint = ch.Tint,
						CharacterId = ch.Id
					});
				}
				break;

			case ShopTab.Items:
				foreach (var item in GameCatalog.Consumables)
				{
					list.Add(new ShopEntry
					{
						Id = item.Id,
						Tab = ShopTab.Items,
						Kind = ShopEntryKind.ConsumableItem,
						Name = item.Name,
						Tag = item.Tag,
						Summary = item.Summary,
						Description = item.Description,
						Price = item.Price,
						Currency = item.Currency,
						IconPath = item.IconPath,
						IconTint = item.IconTint
					});
				}
				break;

			case ShopTab.Skins:
				foreach (var skin in GameCatalog.Skins)
				{
					list.Add(new ShopEntry
					{
						Id = skin.Id,
						Tab = ShopTab.Skins,
						Kind = ShopEntryKind.Skin,
						Name = skin.Name,
						Tag = skin.Tag,
						Summary = skin.Summary,
						Description = skin.Description,
						Price = skin.Price,
						Currency = skin.Currency,
						IconPath = skin.IconPath,
						IconTint = skin.IconTint
					});
				}
				break;

			case ShopTab.Exchange:
				list.AddRange(new[]
				{
					new ShopEntry
					{
						Id = "ex_gold_small", Tab = ShopTab.Exchange, Kind = ShopEntryKind.ExchangePack,
						Name = "Túi Vàng Chợ Sáng", Tag = "Đổi Rau lấy Vàng",
						Summary = "+1,500 Vàng",
						Description = "Quy đổi 100 Rau thành 1,500 Vàng để mua nhân vật hoặc nâng cấp chỉ số.",
						Price = 100, Currency = CurrencyType.Rau,
						IconPath = "res://assets/sprites/icons/coin.png", IconTint = Colors.White, GainCoins = 1500
					},
					new ShopEntry
					{
						Id = "ex_gold_big", Tab = ShopTab.Exchange, Kind = ShopEntryKind.ExchangePack,
						Name = "Rương Đại Gia Chợ", Tag = "Ưu đãi +6%",
						Summary = "+8,000 Vàng",
						Description = "Quy đổi 500 Rau lấy 8,000 Vàng, tiết kiệm hơn khi cần nâng cấp nhiều chỉ số.",
						Price = 500, Currency = CurrencyType.Rau,
						IconPath = "res://assets/sprites/icons/coin.png", IconTint = new Color(1f, 0.92f, 0.6f, 1f), GainCoins = 8000
					},
					new ShopEntry
					{
						Id = "ex_rau_small", Tab = ShopTab.Exchange, Kind = ShopEntryKind.ExchangePack,
						Name = "Rổ Rau Sạch Tươi", Tag = "Đổi Vàng lấy Rau",
						Summary = "+150 Rau",
						Description = "Dùng 2,000 Vàng thu mua 150 Rau để mở khóa Cậu Năm Shipper hoặc trang phục.",
						Price = 2000, Currency = CurrencyType.Gold,
						IconPath = "res://assets/sprites/icons/rau.png", IconTint = Colors.White, GainRau = 150
					},
					new ShopEntry
					{
						Id = "ex_rau_big", Tab = ShopTab.Exchange, Kind = ShopEntryKind.ExchangePack,
						Name = "Vườn Rau Siêu Cấp", Tag = "Đổi Vàng lấy Rau",
						Summary = "+400 Rau",
						Description = "Đầu tư 5,000 Vàng nhận ngay 400 Rau sạch cho quầy hàng.",
						Price = 5000, Currency = CurrencyType.Gold,
						IconPath = "res://assets/sprites/icons/rau.png", IconTint = new Color(0.85f, 1f, 0.85f, 1f), GainRau = 400
					},
					new ShopEntry
					{
						Id = "ex_starter_gift", Tab = ShopTab.Exchange, Kind = ShopEntryKind.ExchangePack,
						Name = "Quà Mở Sạp Chợ", Tag = "Miễn phí",
						Summary = "+1,000 Vàng · +100 Rau",
						Description = "Phần quà hỗ trợ tiểu thương mới khai trương, nhận 1 lần cho tài khoản.",
						Price = 0, Currency = CurrencyType.Gold,
						IconPath = "res://assets/sprites/ui/buttons/tab_events.png", IconTint = Colors.White,
						GainCoins = 1000, GainRau = 100, IsStarterGift = true
					}
				});
				break;

			case ShopTab.Market:
				if (_data != null)
				{
					foreach (var ownedListing in _data.PlayerMarketListings)
					{
						list.Add(WrapMarketEntry(ownedListing));
					}
				}
				foreach (var communityListing in CreateCommunityListings())
				{
					list.Add(WrapMarketEntry(communityListing));
				}
				break;
		}
		return list;
	}

	private ShopEntry WrapMarketEntry(MarketTradeListing listing)
	{
		string giveText = FormatTradeAsset(listing.GiveType, listing.GiveItemId, listing.GiveAmount);
		string receiveText = FormatTradeAsset(listing.ReceiveType, listing.ReceiveItemId, listing.ReceiveAmount);
		string iconPath = GetTradeAssetIconPath(listing.ReceiveType, listing.ReceiveItemId);
		Color tint = GetTradeAssetTint(listing.ReceiveType, listing.ReceiveItemId);

		return new ShopEntry
		{
			Id = listing.Id,
			Tab = ShopTab.Market,
			Kind = ShopEntryKind.MarketTrade,
			Name = listing.SellerName,
			Tag = listing.IsPlayerOwned ? "Của bạn" : "Trao đổi",
			Summary = $"Đưa {giveText} · Nhận {receiveText}",
			Description = listing.IsPlayerOwned
				? $"Sạp bạn đang treo đổi {giveText} lấy {receiveText}. Bấm Thu hồi để lấy lại vật phẩm."
				: $"Bạn đưa {giveText} để đổi lấy {receiveText} từ {listing.SellerName}.",
			IconPath = iconPath,
			IconTint = tint,
			MarketListing = listing
		};
	}

	private static MarketTradeListing[] CreateCommunityListings() => new[]
	{
		new MarketTradeListing
		{
			Id = "npc_co_tu", SellerName = "Sạp Cô Tư", IsPlayerOwned = false,
			GiveType = TradeAssetType.Item, GiveItemId = "item_tra_da", GiveAmount = 2,
			ReceiveType = TradeAssetType.Item, ReceiveItemId = "item_gio_hang", ReceiveAmount = 1
		},
		new MarketTradeListing
		{
			Id = "npc_anh_hai", SellerName = "Sạp Anh Hai", IsPlayerOwned = false,
			GiveType = TradeAssetType.Item, GiveItemId = "item_bua_x2", GiveAmount = 1,
			ReceiveType = TradeAssetType.Item, ReceiveItemId = "item_nuoc_mia", ReceiveAmount = 2
		},
		new MarketTradeListing
		{
			Id = "npc_chu_sau", SellerName = "Sạp Chú Sáu", IsPlayerOwned = false,
			GiveType = TradeAssetType.Item, GiveItemId = "item_tra_da", GiveAmount = 3,
			ReceiveType = TradeAssetType.Rau, ReceiveAmount = 140
		},
		new MarketTradeListing
		{
			Id = "npc_be_na", SellerName = "Sạp Bé Na", IsPlayerOwned = false,
			GiveType = TradeAssetType.Item, GiveItemId = "item_hat_giong", GiveAmount = 2,
			ReceiveType = TradeAssetType.Item, ReceiveItemId = "item_giap_noi", ReceiveAmount = 1
		},
		new MarketTradeListing
		{
			Id = "npc_lao_bay", SellerName = "Sạp Lão Bảy", IsPlayerOwned = false,
			GiveType = TradeAssetType.Item, GiveItemId = "item_gio_hang", GiveAmount = 1,
			ReceiveType = TradeAssetType.Gold, ReceiveAmount = 420
		}
	};

	private void ConfigureGridForTab(ShopTab tab)
	{
		if (_cardsGrid == null) return;

		switch (tab)
		{
			case ShopTab.Characters:
				_cardsGrid.Columns = 4;
				_cardsGrid.AddThemeConstantOverride("h_separation", 18);
				_cardsGrid.AddThemeConstantOverride("v_separation", 18);
				break;
			case ShopTab.Items:
			case ShopTab.Skins:
			case ShopTab.Exchange:
				_cardsGrid.Columns = 5;
				_cardsGrid.AddThemeConstantOverride("h_separation", 16);
				_cardsGrid.AddThemeConstantOverride("v_separation", 16);
				break;
			case ShopTab.Market:
				_cardsGrid.Columns = 4;
				_cardsGrid.AddThemeConstantOverride("h_separation", 18);
				_cardsGrid.AddThemeConstantOverride("v_separation", 18);
				break;
		}
	}

	private void RebuildGrid(bool animate)
	{
		if (_cardsGrid == null) return;

		ConfigureGridForTab(_currentTab);

		foreach (Node child in _cardsGrid.GetChildren())
		{
			child.QueueFree();
		}

		var entries = BuildCatalogForTab(_currentTab);
		foreach (var entry in entries)
		{
			Control card = entry.Kind switch
			{
				ShopEntryKind.Character => CreateCharacterHeroCard(entry),
				ShopEntryKind.Skin => CreateSkinPortraitCard(entry),
				ShopEntryKind.MarketTrade => CreateMarketTradeCard(entry),
				_ => CreateCompactTileCard(entry)
			};
			_cardsGrid.AddChild(card);
		}

		if (animate)
		{
			_cardsGrid.Modulate = new Color(1, 1, 1, 0);
			var tween = CreateTween();
			tween.TweenProperty(_cardsGrid, "modulate:a", 1.0f, 0.16f)
				.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
		}
		else
		{
			_cardsGrid.Modulate = Colors.White;
		}
	}

	/// <summary>
	/// Card tướng full ảnh toàn thẻ (4 cột) tạo từ prefab shop_character_card.tscn:
	/// - Bấm vào ảnh/thẻ → chuyển sang trang Nhân vật để xem preview kèm thông số ở InfoPanel.
	/// - Bấm vào nút Vàng hoặc Rau bên dưới tên → mới mở modal mua.
	/// </summary>
	private Control CreateCharacterHeroCard(ShopEntry entry)
	{
		GetOwnershipState(entry, out bool isOwned, out bool isEquipped, out _, out _);

		var card = _characterCardScene.Instantiate<PanelContainer>();
		ApplyCardBorderHighlight(card, isOwned, isEquipped);

		if (card.FindChild("HeroImage", true, false) is TextureRect heroImage)
		{
			heroImage.Texture = LoadIcon(entry.IconPath);
			heroImage.Modulate = isOwned ? entry.IconTint : entry.IconTint * new Color(0.76f, 0.76f, 0.86f, 0.94f);
		}

		if (card.FindChild("RoleLabel", true, false) is Label roleLabel)
		{
			roleLabel.Text = entry.Tag;
		}

		ApplyTopStatusNodes(card, isOwned, isEquipped, isOwned ? "Đã sở hữu" : "Đã mở");

		if (isOwned && card.FindChild("BottomScrim", true, false) is PanelContainer bottomScrim)
		{
			if (bottomScrim.GetThemeStylebox("panel") is StyleBoxFlat scrimBase)
			{
				var copy = (StyleBoxFlat)scrimBase.Duplicate();
				copy.BorderColor = new Color(0.36f, 0.78f, 1.0f, 0.80f);
				bottomScrim.AddThemeStyleboxOverride("panel", copy);
			}
		}

		if (card.FindChild("NameLabel", true, false) is Label nameLabel)
		{
			nameLabel.Text = entry.Name;
		}

		if (card.FindChild("ActionsRow", true, false) is Control actionsRow)
		{
			ClearChildrenImmediate(actionsRow);
			if (isOwned)
			{
				actionsRow.AddChild(InstantiateActionButton(
					"Đã sở hữu",
					null,
					BtnBgEquipped,
					BtnBorderEquipped,
					true,
					null));
			}
			else
			{
				actionsRow.AddChild(InstantiateActionButton(
					$"{entry.Price:N0}",
					GetCurrencyIcon(entry.Currency),
					entry.Currency == CurrencyType.Rau ? BtnBgRau : BtnBgGold,
					entry.Currency == CurrencyType.Rau ? BtnBorderRau : BtnBorderGold,
					false,
					() => OpenCharacterUnlockModal(entry, entry.Currency, entry.Price)));

				if (entry.AltPrice > 0)
				{
					actionsRow.AddChild(InstantiateActionButton(
						$"{entry.AltPrice:N0}",
						GetCurrencyIcon(entry.AltCurrency),
						entry.AltCurrency == CurrencyType.Rau ? BtnBgRau : BtnBgGold,
						entry.AltCurrency == CurrencyType.Rau ? BtnBorderRau : BtnBorderGold,
						false,
						() => OpenCharacterUnlockModal(entry, entry.AltCurrency, entry.AltPrice)));
				}
			}
		}

		// Bấm vào card (ngoài nút mua/chọn) → mở preview bên character_screen.tscn
		card.GuiInput += (InputEvent @event) =>
		{
			bool isLeftClick = @event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left;
			bool isTouch = @event is InputEventScreenTouch st && st.Pressed;
			if (isLeftClick || isTouch)
			{
				card.AcceptEvent();
				NavigateToCharacterPreview(entry.CharacterId);
			}
		};

		return card;
	}

	/// <summary>
	/// Card chữ nhật dọc (5 cột) cho tab Trang phục tạo từ prefab shop_skin_card.tscn.
	/// </summary>
	private Control CreateSkinPortraitCard(ShopEntry entry)
	{
		GetOwnershipState(entry, out bool isOwned, out bool isEquipped, out _, out _);

		var card = _skinCardScene.Instantiate<PanelContainer>();
		ApplyCardBorderHighlight(card, isOwned, isEquipped);

		if (card.FindChild("GlowTint", true, false) is ColorRect glowTint)
		{
			glowTint.Color = new Color(entry.IconTint.R, entry.IconTint.G, entry.IconTint.B, isOwned ? 0.16f : 0.08f);
		}

		if (card.FindChild("SkinImage", true, false) is TextureRect skinImage)
		{
			skinImage.Texture = LoadIcon(entry.IconPath);
			skinImage.Modulate = isOwned ? entry.IconTint : entry.IconTint * new Color(0.68f, 0.68f, 0.78f, 0.90f);
		}

		if (card.FindChild("TagLabel", true, false) is Label tagLabel)
		{
			tagLabel.Text = entry.Tag;
		}

		ApplyTopStatusNodes(card, isOwned, isEquipped, string.Empty, showStatusBadge: false);

		if (isEquipped && card.FindChild("Divider", true, false) is ColorRect divider)
		{
			divider.Color = new Color(0.36f, 0.78f, 1f, 0.85f);
		}

		if (card.FindChild("NameLabel", true, false) is Label nameLabel)
		{
			nameLabel.Text = entry.Name;
		}

		if (card.FindChild("SummaryLabel", true, false) is Label summaryLabel)
		{
			summaryLabel.Text = entry.Summary;
		}

		bool btnDisabled = false;
		if (card.FindChild("ActionsRow", true, false) is Control actionsRow)
		{
			ClearChildrenImmediate(actionsRow);
			actionsRow.AddChild(BuildEntryActionPill(entry, isOwned, isEquipped, false, out btnDisabled));
		}

		AttachCardClick(card, entry, btnDisabled);
		return card;
	}

	/// <summary>
	/// Card nhỏ gọn 5 cột cho Vật phẩm và Đổi Vàng/Rau tạo từ prefab shop_compact_card.tscn.
	/// </summary>
	private Control CreateCompactTileCard(ShopEntry entry)
	{
		GetOwnershipState(entry, out bool isOwned, out bool isEquipped, out bool isGiftClaimed, out int itemQty);

		var card = _compactCardScene.Instantiate<PanelContainer>();
		ApplyCardBorderHighlight(card, isGiftClaimed, false);

		bool isExchangeConversion = entry.Kind == ShopEntryKind.ExchangePack && !entry.IsStarterGift;
		var fromIcon = card.FindChild("FromIcon", true, false) as TextureRect;
		var arrowIcon = card.FindChild("ArrowIcon", true, false) as TextureRect;
		var toIcon = card.FindChild("ToIcon", true, false) as TextureRect;
		var tagLabel = card.FindChild("TagLabel", true, false) as Label;

		if (isExchangeConversion && fromIcon != null && arrowIcon != null && toIcon != null)
		{
			fromIcon.Texture = GetCurrencyIcon(entry.Currency);
			toIcon.Texture = entry.GainRau > 0 ? _rauIconTexture : _goldIconTexture;
			fromIcon.Visible = true;
			arrowIcon.Visible = true;
			toIcon.Visible = true;
			if (tagLabel != null) tagLabel.Visible = false;
		}
		else if (tagLabel != null)
		{
			tagLabel.Visible = true;
			tagLabel.Text = entry.Tag;
		}

		var statusBadge = card.FindChild("StatusBadge", true, false) as PanelContainer;
		var statusLabel = card.FindChild("StatusLabel", true, false) as Label;
		if (statusBadge != null && statusLabel != null)
		{
			if (isGiftClaimed)
			{
				statusBadge.Visible = true;
				statusBadge.AddThemeStyleboxOverride("panel", _styleBadgeOwned);
				statusLabel.Text = "Đã nhận";
				statusLabel.AddThemeColorOverride("font_color", new Color(0.75f, 1f, 0.85f, 1f));
			}
			else if (itemQty > 0)
			{
				statusBadge.Visible = true;
				statusBadge.AddThemeStyleboxOverride("panel", _styleBadgeQty);
				statusLabel.Text = $"x{itemQty}";
				statusLabel.AddThemeColorOverride("font_color", new Color(0.78f, 0.94f, 1f, 1f));
			}
			else if (entry.Id == "ex_gold_big")
			{
				statusBadge.Visible = true;
				statusBadge.AddThemeStyleboxOverride("panel", _styleBadgeOwned);
				statusLabel.Text = "Ưu đãi +6%";
				statusLabel.AddThemeColorOverride("font_color", new Color(0.75f, 1f, 0.85f, 1f));
			}
			else
			{
				statusBadge.Visible = false;
			}
		}

		if (card.FindChild("ItemImage", true, false) is TextureRect itemImage)
		{
			itemImage.Texture = LoadIcon(entry.IconPath);
			itemImage.Modulate = entry.IconTint;
		}

		if (card.FindChild("NameLabel", true, false) is Label nameLabel)
		{
			nameLabel.Text = entry.Name;
		}

		if (card.FindChild("SummaryLabel", true, false) is Label summaryLabel)
		{
			summaryLabel.Text = entry.Summary;
			if (entry.Kind == ShopEntryKind.ExchangePack)
			{
				summaryLabel.AddThemeColorOverride("font_color",
					entry.GainRau > 0 && entry.GainCoins == 0 ? ColorRauText : ColorGoldText);
			}
		}

		bool btnDisabled = false;
		if (card.FindChild("ActionsRow", true, false) is Control actionsRow)
		{
			ClearChildrenImmediate(actionsRow);
			actionsRow.AddChild(BuildEntryActionPill(entry, isOwned, isEquipped, isGiftClaimed, out btnDisabled));
		}

		AttachCardClick(card, entry, btnDisabled);
		return card;
	}

	/// <summary>
	/// Card trao đổi người chơi trong tab Chợ phiên tạo từ prefab shop_market_card.tscn.
	/// </summary>
	private Control CreateMarketTradeCard(ShopEntry entry)
	{
		var listing = entry.MarketListing;
		bool isMine = listing.IsPlayerOwned;

		var card = _marketCardScene.Instantiate<PanelContainer>();
		ApplyCardBorderHighlight(card, false, isMine);

		if (card.FindChild("SellerBadge", true, false) is PanelContainer sellerBadge && isMine)
		{
			sellerBadge.AddThemeStyleboxOverride("panel", _styleBadgeQty);
		}
		if (card.FindChild("SellerLabel", true, false) is Label sellerLabel)
		{
			sellerLabel.Text = listing.SellerName;
		}

		if (card.FindChild("StatusBadge", true, false) is PanelContainer statusBadge && isMine)
		{
			statusBadge.AddThemeStyleboxOverride("panel", _styleBadgeOwned);
		}
		if (card.FindChild("StatusLabel", true, false) is Label statusLabel)
		{
			statusLabel.Text = isMine ? "Đang treo" : "Trao đổi";
		}

		if (card.FindChild("GiveRoleLabel", true, false) is Label giveRoleLabel)
		{
			giveRoleLabel.Text = isMine ? "Đang gửi" : "Bạn đưa";
		}
		if (card.FindChild("GiveIcon", true, false) is TextureRect giveIcon)
		{
			giveIcon.Texture = LoadIcon(GetTradeAssetIconPath(listing.GiveType, listing.GiveItemId));
			giveIcon.Modulate = GetTradeAssetTint(listing.GiveType, listing.GiveItemId);
		}
		if (card.FindChild("GiveAmountLabel", true, false) is Label giveAmountLabel)
		{
			giveAmountLabel.Text = FormatTradeAsset(listing.GiveType, listing.GiveItemId, listing.GiveAmount);
		}
		if (card.FindChild("GiveStockLabel", true, false) is Label giveStockLabel)
		{
			giveStockLabel.Visible = !isMine;
			if (!isMine)
			{
				int owned = GetTradeAssetOwned(listing.GiveType, listing.GiveItemId);
				giveStockLabel.Text = $"Có: {owned:N0}";
				giveStockLabel.AddThemeColorOverride("font_color", owned >= listing.GiveAmount ? ColorRauText : ColorErrorText);
			}
		}

		if (card.FindChild("ReceiveRoleLabel", true, false) is Label receiveRoleLabel)
		{
			receiveRoleLabel.Text = isMine ? "Sẽ nhận" : "Bạn nhận";
		}
		if (card.FindChild("ReceiveIcon", true, false) is TextureRect receiveIcon)
		{
			receiveIcon.Texture = LoadIcon(GetTradeAssetIconPath(listing.ReceiveType, listing.ReceiveItemId));
			receiveIcon.Modulate = GetTradeAssetTint(listing.ReceiveType, listing.ReceiveItemId);
		}
		if (card.FindChild("ReceiveAmountLabel", true, false) is Label receiveAmountLabel)
		{
			receiveAmountLabel.Text = FormatTradeAsset(listing.ReceiveType, listing.ReceiveItemId, listing.ReceiveAmount);
		}

		if (card.FindChild("ActionsRow", true, false) is Control actionsRow)
		{
			ClearChildrenImmediate(actionsRow);
			actionsRow.AddChild(InstantiateActionButton(
				isMine ? "Thu hồi" : "Trao đổi",
				null,
				isMine ? BtnBgCancelRed : BtnBgExchange,
				isMine ? BtnBorderCancelRed : BtnBorderExchange,
				false,
				() => OnEntryClicked(entry)));
		}

		AttachCardClick(card, entry, false);
		return card;
	}

	private static void ClearChildrenImmediate(Node container)
	{
		for (int i = container.GetChildCount() - 1; i >= 0; i--)
		{
			var child = container.GetChild(i);
			container.RemoveChild(child);
			child.QueueFree();
		}
	}

	private static void ApplyCardBorderHighlight(PanelContainer card, bool isOwned, bool isEquipped)
	{
		// Chỉ dùng viền xanh lá nhẹ để đánh dấu đã sở hữu; không dùng viền xanh đậm kiểu "hover" nữa
		if (card == null || !isOwned) return;

		if (card.GetThemeStylebox("panel") is StyleBoxFlat baseStyle)
		{
			var highlighted = (StyleBoxFlat)baseStyle.Duplicate();
			highlighted.BorderColor = new Color(0.28f, 0.82f, 0.48f, 0.45f);  // 45% alpha - mờ nhẹ
			highlighted.BorderWidthLeft = 3;
			highlighted.BorderWidthTop = 3;
			highlighted.BorderWidthRight = 3;
			highlighted.BorderWidthBottom = 3;
			card.AddThemeStyleboxOverride("panel", highlighted);
		}
	}

	private void ApplyTopStatusNodes(Control card, bool isOwned, bool isEquipped, string ownedText, bool showStatusBadge = true)
	{
		var statusBadge = card.FindChild("StatusBadge", true, false) as PanelContainer;
		var statusLabel = card.FindChild("StatusLabel", true, false) as Label;
		var lockIcon = card.FindChild("LockIcon", true, false) as TextureRect;

		bool showBadge = showStatusBadge && (isOwned || isEquipped);
		if (statusBadge != null) statusBadge.Visible = showBadge;
		if (statusLabel != null && showBadge) statusLabel.Text = ownedText;
		if (lockIcon != null)
		{
			lockIcon.Visible = !isOwned && !isEquipped && _lockIconTexture != null;
			if (_lockIconTexture != null) lockIcon.Texture = _lockIconTexture;
		}
	}

	private Control BuildEntryActionPill(ShopEntry entry, bool isOwned, bool isEquipped, bool isGiftClaimed, out bool disabled)
	{
		disabled = false;

		if (entry.Kind == ShopEntryKind.Skin && isOwned)
		{
			disabled = isEquipped;
			return InstantiateActionButton(
				isEquipped ? "Đã sở hữu" : "Trang bị",
				null,
				isEquipped ? BtnBgEquipped : BtnBgSelect,
				isEquipped ? BtnBorderEquipped : BtnBorderSelect,
				disabled,
				() => OnEntryClicked(entry));
		}

		if (isGiftClaimed)
		{
			disabled = true;
			return InstantiateActionButton("Đã nhận", null, BtnBgEquipped, BtnBorderEquipped, true, null);
		}

		if (entry.Price == 0)
		{
			return InstantiateActionButton("Nhận miễn phí", null, BtnBgRau, BtnBorderRau, false, () => OnEntryClicked(entry));
		}

		return InstantiateActionButton(
			$"{entry.Price:N0}",
			GetCurrencyIcon(entry.Currency),
			entry.Currency == CurrencyType.Rau ? BtnBgRau : BtnBgGold,
			entry.Currency == CurrencyType.Rau ? BtnBorderRau : BtnBorderGold,
			false,
			() => OnEntryClicked(entry));
	}

	private Control InstantiateActionButton(
		string text,
		Texture2D currencyIcon,
		Color bgColor,
		Color borderColor,
		bool disabled,
		Action onPressed)
	{
		var pill = _priceButtonScene.Instantiate<PanelContainer>();
		pill.MouseDefaultCursorShape = disabled ? CursorShape.Arrow : CursorShape.PointingHand;

		if (pill.GetThemeStylebox("panel") is StyleBoxFlat baseStyle)
		{
			var colored = (StyleBoxFlat)baseStyle.Duplicate();
			colored.BgColor = bgColor;
			colored.BorderColor = borderColor;
			pill.AddThemeStyleboxOverride("panel", colored);
		}

		if (pill.FindChild("CurrencyIcon", true, false) is TextureRect iconRect)
		{
			iconRect.Visible = currencyIcon != null;
			if (currencyIcon != null) iconRect.Texture = currencyIcon;
		}

		if (pill.FindChild("PriceLabel", true, false) is Label priceLabel)
		{
			priceLabel.Text = text;
		}

		if (pill.FindChild("HitBtn", true, false) is Button hitBtn)
		{
			hitBtn.Disabled = disabled;
			hitBtn.MouseDefaultCursorShape = disabled ? CursorShape.Arrow : CursorShape.PointingHand;
			if (onPressed != null && !disabled)
			{
				hitBtn.Pressed += onPressed;
				UIButtonHelper.AttachBounceEffect(hitBtn, targetControl: pill);
			}
		}

		return pill;
	}

	private void AttachCardClick(Control card, ShopEntry entry, bool disabled)
	{
		card.GuiInput += (InputEvent @event) =>
		{
			bool isLeftClick = @event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left;
			bool isTouch = @event is InputEventScreenTouch st && st.Pressed;
			if ((isLeftClick || isTouch) && !disabled)
			{
				card.AcceptEvent();
				OnEntryClicked(entry);
			}
		};
	}

	private void GetOwnershipState(ShopEntry entry, out bool isOwned, out bool isEquipped, out bool isGiftClaimed, out int itemQty)
	{
		bool isCharUnlocked = entry.Kind == ShopEntryKind.Character && IsCharacterUnlocked(entry.CharacterId);
		bool isCharSelected = isCharUnlocked && _data != null && _data.SelectedCharacterIndex == entry.CharacterId;
		bool isSkinUnlocked = entry.Kind == ShopEntryKind.Skin && (_data?.IsSkinUnlocked(entry.Id) ?? entry.Id == "outfit_default");
		bool isSkinEquipped = isSkinUnlocked && (_data?.EquippedShopSkinId == entry.Id);
		isGiftClaimed = entry.IsStarterGift && (_data?.HasClaimedStarterShopGift ?? false);
		itemQty = entry.Kind == ShopEntryKind.ConsumableItem ? (_data?.GetItemQuantity(entry.Id) ?? 0) : 0;

		isOwned = isCharUnlocked || isSkinUnlocked || isGiftClaimed;
		isEquipped = isCharSelected || isSkinEquipped;
	}

	private void NavigateToCharacterPreview(int characterId)
	{
		if (characterId < 0) return;

		if (_data != null)
		{
			_data.PendingPreviewCharacterIndex = characterId;
		}

		if (_shellSidebar != null)
		{
			_shellSidebar.SelectTab(1);
			return;
		}

		var sidebar = GetNodeOrNull<LeftSidebar>("LeftSidebar");
		var tabChars = sidebar?.GetNodeOrNull<SidebarTab>("TabsVBox/Tab1_NhanVat");
		if (tabChars != null)
		{
			tabChars.EmitSignal(SidebarTab.SignalName.TabSelected, 1);
		}
		else
		{
			GetTree().ChangeSceneToFile("res://scenes/ui/character_screen.tscn");
		}
	}

	private void OpenCharacterUnlockModal(ShopEntry entry, CurrencyType currency, int price)
	{
		if (entry == null || IsCharacterUnlocked(entry.CharacterId)) return;

		_pendingUnlockCurrency = currency;
		_pendingUnlockPrice = price;
		OpenBuyModal(entry);
	}

	private void OnEntryClicked(ShopEntry entry)
	{
		if (entry == null) return;

		if (entry.Kind == ShopEntryKind.Character)
		{
			if (IsCharacterUnlocked(entry.CharacterId))
			{
				if (_data != null && _data.SetSelectedCharacter(entry.CharacterId))
				{
					ShowToast($"Đã chọn nhân vật {entry.Name} ra trận!");
					RebuildGrid(animate: false);
				}
			}
			else
			{
				OpenCharacterUnlockModal(entry, entry.Currency, entry.Price);
			}
			return;
		}

		if (entry.Kind == ShopEntryKind.Skin && (_data?.IsSkinUnlocked(entry.Id) ?? false))
		{
			if (_data != null && _data.TryEquipShopSkin(entry.Id))
			{
				ShowToast($"Đã trang bị {entry.Name}!");
				RebuildGrid(animate: false);
			}
			return;
		}

		if (entry.IsStarterGift && (_data?.HasClaimedStarterShopGift ?? false))
		{
			return;
		}

		if (entry.Kind == ShopEntryKind.MarketTrade && entry.MarketListing != null && entry.MarketListing.IsPlayerOwned)
		{
			if (_data != null && _data.TryCancelPlayerMarketListing(entry.MarketListing.Id))
			{
				ShowToast("Đã thu hồi vật phẩm về túi đồ!");
				RebuildGrid(animate: false);
			}
			return;
		}

		_pendingUnlockCurrency = entry.Currency;
		_pendingUnlockPrice = entry.Price;
		OpenBuyModal(entry);
	}

	private void OpenBuyModal(ShopEntry entry)
	{
		if (_buyModal == null || entry == null) return;

		_pendingEntry = entry;
		_pendingQuantity = 1;

		if (_buyModalTitle != null)
		{
			_buyModalTitle.Text = entry.Kind switch
			{
				ShopEntryKind.Character => "Mở khóa nhân vật",
				ShopEntryKind.Skin => "Mở khóa trang phục",
				ShopEntryKind.ExchangePack => entry.IsStarterGift ? "Nhận quà mở sạp" : "Quy đổi tiền tệ",
				ShopEntryKind.MarketTrade => "Xác nhận trao đổi",
				_ => "Xác nhận mua vật phẩm"
			};
		}

		if (_buyPreviewTexture != null)
		{
			_buyPreviewTexture.Texture = LoadIcon(entry.IconPath);
			_buyPreviewTexture.Modulate = entry.IconTint;
		}

		bool isCharacter = entry.Kind == ShopEntryKind.Character;
		if (_buyItemName != null) _buyItemName.Text = entry.Name;
		if (_buyItemTag != null)
		{
			_buyItemTag.Visible = !isCharacter;
			if (!isCharacter)
			{
				_buyItemTag.Text = entry.Kind == ShopEntryKind.MarketTrade ? entry.Summary : $"{entry.Tag} · {entry.Summary}";
			}
		}
		if (_buyItemDesc != null)
		{
			_buyItemDesc.Visible = !isCharacter;
			if (!isCharacter)
			{
				_buyItemDesc.Text = entry.Description;
			}
		}

		if (_buyQuantityRow != null)
		{
			_buyQuantityRow.Visible = entry.Kind == ShopEntryKind.ConsumableItem;
		}

		if (_buyPriceTitle != null)
		{
			_buyPriceTitle.Text = entry.Kind switch
			{
				ShopEntryKind.ExchangePack => entry.IsStarterGift ? "Giá gói:" : "Chi phí đổi:",
				ShopEntryKind.MarketTrade => "Bạn đưa:",
				_ => "Tổng thanh toán:"
			};
		}

		if (_buyConfirmBtn != null)
		{
			_buyConfirmBtn.Text = entry.Kind switch
			{
				ShopEntryKind.ExchangePack => entry.IsStarterGift ? "Nhận Ngay" : "Đổi Ngay",
				ShopEntryKind.MarketTrade => "Trao Đổi",
				_ => "Mua Ngay"
			};
		}

		RefreshBuyModalPricing();
		AnimateModalOpen(_buyModal, _buyDialogBox, ref _buyModalTween);
	}

	private void ChangePendingQuantity(int delta)
	{
		if (_pendingEntry == null || _pendingEntry.Kind != ShopEntryKind.ConsumableItem) return;

		_pendingQuantity = Mathf.Clamp(_pendingQuantity + delta, 1, 99);
		RefreshBuyModalPricing();
	}

	private void RefreshBuyModalPricing()
	{
		if (_pendingEntry == null) return;

		if (_pendingEntry.Kind == ShopEntryKind.MarketTrade && _pendingEntry.MarketListing != null)
		{
			var listing = _pendingEntry.MarketListing;
			int owned = GetTradeAssetOwned(listing.GiveType, listing.GiveItemId);
			bool enoughTrade = owned >= listing.GiveAmount;
			string giveName = FormatTradeAsset(listing.GiveType, listing.GiveItemId, listing.GiveAmount);

			if (_buyPriceCurrencyIcon != null)
			{
				_buyPriceCurrencyIcon.Texture = LoadIcon(GetTradeAssetIconPath(listing.GiveType, listing.GiveItemId));
				_buyPriceCurrencyIcon.Modulate = GetTradeAssetTint(listing.GiveType, listing.GiveItemId);
				_buyPriceCurrencyIcon.Visible = true;
			}
			if (_buyPriceLabel != null)
			{
				_buyPriceLabel.Text = giveName;
				_buyPriceLabel.AddThemeColorOverride("font_color", ColorGoldText);
			}
			if (_buyBalanceLabel != null)
			{
				_buyBalanceLabel.Text = enoughTrade
					? $"Trong túi hiện có: {owned:N0} (Đủ để trao đổi)"
					: $"Trong túi hiện có: {owned:N0} (Thiếu {listing.GiveAmount - owned:N0})";
				_buyBalanceLabel.AddThemeColorOverride("font_color", enoughTrade ? ColorRauText : ColorErrorText);
			}
			return;
		}

		CurrencyType activeCurrency = _pendingEntry.Kind == ShopEntryKind.Character ? _pendingUnlockCurrency : _pendingEntry.Currency;
		int unitPrice = _pendingEntry.Kind == ShopEntryKind.Character ? _pendingUnlockPrice : _pendingEntry.Price;
		int totalCost = unitPrice * _pendingQuantity;
		int currentBalance = GetBalance(activeCurrency);
		string unit = GetCurrencyName(activeCurrency);
		bool hasEnough = currentBalance >= totalCost;

		if (_buyQtyValueLabel != null) _buyQtyValueLabel.Text = _pendingQuantity.ToString();
		if (_buyQtyMinusBtn != null) _buyQtyMinusBtn.Disabled = _pendingQuantity <= 1;
		if (_buyQtyPlusBtn != null) _buyQtyPlusBtn.Disabled = _pendingQuantity >= 99;

		if (_buyPriceCurrencyIcon != null)
		{
			_buyPriceCurrencyIcon.Texture = GetCurrencyIcon(activeCurrency);
			_buyPriceCurrencyIcon.Modulate = Colors.White;
			_buyPriceCurrencyIcon.Visible = totalCost > 0;
		}

		if (_buyPriceLabel != null)
		{
			_buyPriceLabel.Text = totalCost == 0 ? "Miễn phí" : $"{totalCost:N0} {unit}";
			_buyPriceLabel.AddThemeColorOverride("font_color",
				activeCurrency == CurrencyType.Rau ? ColorRauText : ColorGoldText);
		}

		if (_buyBalanceLabel != null)
		{
			if (totalCost == 0)
			{
				_buyBalanceLabel.Text = "Quà hỗ trợ nhận miễn phí 1 lần";
				_buyBalanceLabel.AddThemeColorOverride("font_color", ColorRauText);
			}
			else if (hasEnough)
			{
				int remaining = currentBalance - totalCost;
				_buyBalanceLabel.Text = $"Hiện có: {currentBalance:N0} {unit}  ·  Còn lại: {remaining:N0} {unit}";
				_buyBalanceLabel.AddThemeColorOverride("font_color", ColorMutedText);
			}
			else
			{
				_buyBalanceLabel.Text = $"Hiện có: {currentBalance:N0} {unit} (Thiếu {totalCost - currentBalance:N0} {unit})";
				_buyBalanceLabel.AddThemeColorOverride("font_color", ColorErrorText);
			}
		}
	}

	private void CloseBuyModal()
	{
		AnimateModalClose(_buyModal, _buyDialogBox, ref _buyModalTween);
	}

	private void ConfirmPendingPurchase()
	{
		if (_pendingEntry == null || _data == null) return;

		var entry = _pendingEntry;
		CurrencyType activeCurrency = entry.Kind == ShopEntryKind.Character ? _pendingUnlockCurrency : entry.Currency;
		int unitPrice = entry.Kind == ShopEntryKind.Character ? _pendingUnlockPrice : entry.Price;
		int qty = entry.Kind == ShopEntryKind.ConsumableItem ? _pendingQuantity : 1;
		int totalPrice = unitPrice * qty;
		bool success = false;
		string successToast = "";

		switch (entry.Kind)
		{
			case ShopEntryKind.Character:
				success = _data.TryUnlockCharacter(entry.CharacterId, activeCurrency);
				successToast = $"Đã mở khóa và chọn nhân vật {entry.Name}!";
				break;

			case ShopEntryKind.ConsumableItem:
				success = _data.TryBuyShopItem(entry.Id, qty, activeCurrency, totalPrice);
				successToast = $"Đã mua {entry.Name} x{qty} (Đang có: x{_data.GetItemQuantity(entry.Id)})";
				break;

			case ShopEntryKind.Skin:
				success = _data.TryBuyShopSkin(entry.Id, activeCurrency, unitPrice);
				successToast = $"Đã mở khóa và trang bị {entry.Name}!";
				break;

			case ShopEntryKind.ExchangePack:
				success = _data.TryExchangeCurrency(activeCurrency, unitPrice, entry.GainCoins, entry.GainRau, entry.IsStarterGift);
				successToast = entry.IsStarterGift
					? "Đã nhận +1,000 Vàng và +100 Rau!"
					: $"Quy đổi thành công: {entry.Summary}!";
				break;

			case ShopEntryKind.MarketTrade:
				success = _data.TryExecuteMarketTrade(entry.MarketListing);
				successToast = $"Trao đổi thành công với {entry.Name}!";
				break;
		}

		CloseBuyModal();

		if (success)
		{
			RebuildGrid(animate: false);
			ShowToast(successToast);
		}
		else if (entry.Kind == ShopEntryKind.MarketTrade && entry.MarketListing != null)
		{
			OpenMarketInsufficientModal(entry.MarketListing);
		}
		else
		{
			string unit = GetCurrencyName(activeCurrency);
			string msg = entry.Kind switch
			{
				ShopEntryKind.Character => $"Bạn không đủ {unit} để mở khóa nhân vật {entry.Name}!",
				ShopEntryKind.Skin => $"Bạn không đủ {unit} để mở khóa trang phục {entry.Name}!",
				ShopEntryKind.ExchangePack => $"Bạn không đủ {unit} để đổi {entry.Name}!",
				_ => $"Bạn không đủ {unit} để mua {entry.Name} (x{qty})!"
			};
			OpenFailModal(totalPrice, msg, activeCurrency, entry.Kind == ShopEntryKind.ExchangePack ? "Chi phí đổi:" : "Tổng thanh toán:");
		}
	}

	private void OpenMarketInsufficientModal(MarketTradeListing listing)
	{
		if (listing.GiveType == TradeAssetType.Gold || listing.GiveType == TradeAssetType.Rau)
		{
			CurrencyType cur = listing.GiveType == TradeAssetType.Rau ? CurrencyType.Rau : CurrencyType.Gold;
			OpenFailModal(listing.GiveAmount, $"Bạn không đủ {GetCurrencyName(cur)} để trao đổi với {listing.SellerName}!", cur, "Cần đưa:");
			return;
		}

		if (_failModal == null) return;

		int owned = GetTradeAssetOwned(listing.GiveType, listing.GiveItemId);
		int missing = Mathf.Max(0, listing.GiveAmount - owned);
		string itemName = GetItemDisplayName(listing.GiveItemId);

		_failRedirectTab = ShopTab.Items;
		if (_failTitleLabel != null) _failTitleLabel.Text = "Thiếu vật phẩm trao đổi";
		if (_failMessageLabel != null) _failMessageLabel.Text = $"Bạn chưa có đủ {itemName} để trao đổi với {listing.SellerName}!";
		if (_failCostLabel != null) _failCostLabel.Text = "Cần đưa:";
		if (_failCostValue != null) _failCostValue.Text = $"{listing.GiveAmount:N0} {itemName}";
		if (_failCurrentValue != null) _failCurrentValue.Text = $"{owned:N0} {itemName}";
		if (_failMissingValue != null) _failMissingValue.Text = $"{missing:N0} {itemName}";
		if (_failGoExchangeBtn != null)
		{
			_failGoExchangeBtn.Visible = true;
			_failGoExchangeBtn.Text = "Mua Vật phẩm";
		}

		AnimateModalOpen(_failModal, _failDialogBox, ref _failModalTween);
	}

	private void OpenFailModal(int price, string message, CurrencyType currency, string costTitle)
	{
		if (_failModal == null) return;

		int balance = GetBalance(currency);
		int missing = Mathf.Max(0, price - balance);
		string unit = GetCurrencyName(currency);

		_failRedirectTab = ShopTab.Exchange;
		if (_failTitleLabel != null) _failTitleLabel.Text = currency == CurrencyType.Rau ? "Không đủ Rau" : "Không đủ Vàng";
		if (_failMessageLabel != null) _failMessageLabel.Text = message;
		if (_failCostLabel != null) _failCostLabel.Text = costTitle;
		if (_failCostValue != null) _failCostValue.Text = $"{price:N0} {unit}";
		if (_failCurrentValue != null) _failCurrentValue.Text = $"{balance:N0} {unit}";
		if (_failMissingValue != null) _failMissingValue.Text = $"{missing:N0} {unit}";
		if (_failGoExchangeBtn != null)
		{
			_failGoExchangeBtn.Visible = _currentTab != ShopTab.Exchange;
			_failGoExchangeBtn.Text = "Đổi Vàng/Rau";
		}

		AnimateModalOpen(_failModal, _failDialogBox, ref _failModalTween);
	}

	private void CloseFailModal()
	{
		AnimateModalClose(_failModal, _failDialogBox, ref _failModalTween);
	}

	private void OnGoToRedirectTabClicked()
	{
		CloseFailModal();
		SwitchTab(_failRedirectTab);
	}

	private void OpenPostTradeModal()
	{
		if (_data == null || _postModal == null) return;

		_offerItemIds.Clear();
		_offerItemOption?.Clear();

		foreach (var item in GameCatalog.Consumables)
		{
			int qty = _data.GetItemQuantity(item.Id);
			if (qty > 0)
			{
				_offerItemIds.Add(item.Id);
				_offerItemOption?.AddItem($"{item.Name} (Có: x{qty})");
			}
		}

		if (_offerItemIds.Count == 0)
		{
			ShowToast("Túi vật phẩm đang trống — hãy mua vật phẩm ở tab Vật phẩm trước!");
			return;
		}

		_wantTargets.Clear();
		_wantOption?.Clear();
		_wantTargets.Add((TradeAssetType.Gold, null, "Vàng"));
		_wantOption?.AddItem("Vàng");
		_wantTargets.Add((TradeAssetType.Rau, null, "Rau"));
		_wantOption?.AddItem("Rau");

		foreach (var item in GameCatalog.Consumables)
		{
			_wantTargets.Add((TradeAssetType.Item, item.Id, item.Name));
			_wantOption?.AddItem(item.Name);
		}

		if (_offerItemOption != null) _offerItemOption.Selected = 0;
		if (_wantOption != null) _wantOption.Selected = 0;
		StripDropdownRadioCircles(_offerItemOption);
		StripDropdownRadioCircles(_wantOption);

		ResetPostOfferQty();
		ResetPostWantAmount();
		AnimateModalOpen(_postModal, _postDialogBox, ref _postModalTween);
	}

	private void ClosePostTradeModal()
	{
		AnimateModalClose(_postModal, _postDialogBox, ref _postModalTween);
	}

	private void ResetPostOfferQty()
	{
		_postOfferQty = 1;
		UpdatePostOfferQtyUI();
	}

	private void AdjustPostOfferQty(int delta)
	{
		int maxQty = GetSelectedPostOfferMaxQty();
		_postOfferQty = Mathf.Clamp(_postOfferQty + delta, 1, Mathf.Max(1, maxQty));
		UpdatePostOfferQtyUI();
	}

	private int GetSelectedPostOfferMaxQty()
	{
		if (_data == null || _offerItemOption == null) return 1;
		int idx = _offerItemOption.Selected;
		if (idx < 0 || idx >= _offerItemIds.Count) return 1;
		return Mathf.Max(1, _data.GetItemQuantity(_offerItemIds[idx]));
	}

	private void UpdatePostOfferQtyUI()
	{
		int maxQty = GetSelectedPostOfferMaxQty();
		if (_offerQtyLabel != null) _offerQtyLabel.Text = $"x{_postOfferQty}";
		if (_offerMinusBtn != null) _offerMinusBtn.Disabled = _postOfferQty <= 1;
		if (_offerPlusBtn != null) _offerPlusBtn.Disabled = _postOfferQty >= maxQty;
	}

	private void ResetPostWantAmount()
	{
		if (_wantOption == null || _wantOption.Selected < 0 || _wantOption.Selected >= _wantTargets.Count)
		{
			_postWantAmount = 200;
		}
		else
		{
			_postWantAmount = _wantTargets[_wantOption.Selected].Type switch
			{
				TradeAssetType.Gold => 200,
				TradeAssetType.Rau => 50,
				_ => 1
			};
		}
		UpdatePostWantAmountUI();
	}

	private void AdjustPostWantAmount(int direction)
	{
		if (_wantOption == null || _wantOption.Selected < 0 || _wantOption.Selected >= _wantTargets.Count) return;

		var targetType = _wantTargets[_wantOption.Selected].Type;
		int step = targetType switch
		{
			TradeAssetType.Gold => 50,
			TradeAssetType.Rau => 10,
			_ => 1
		};
		int minVal = step;
		int maxVal = targetType == TradeAssetType.Item ? 20 : 10000;

		_postWantAmount = Mathf.Clamp(_postWantAmount + direction * step, minVal, maxVal);
		UpdatePostWantAmountUI();
	}

	private void UpdatePostWantAmountUI()
	{
		if (_wantOption == null || _wantOption.Selected < 0 || _wantOption.Selected >= _wantTargets.Count) return;

		var targetType = _wantTargets[_wantOption.Selected].Type;
		if (_wantAmountLabel != null)
		{
			_wantAmountLabel.Text = targetType == TradeAssetType.Item ? $"x{_postWantAmount}" : $"{_postWantAmount:N0}";
		}
		int minVal = targetType switch { TradeAssetType.Gold => 50, TradeAssetType.Rau => 10, _ => 1 };
		int maxVal = targetType == TradeAssetType.Item ? 20 : 10000;
		if (_wantMinusBtn != null) _wantMinusBtn.Disabled = _postWantAmount <= minVal;
		if (_wantPlusBtn != null) _wantPlusBtn.Disabled = _postWantAmount >= maxVal;
	}

	private void PublishPlayerTrade()
	{
		if (_data == null || _offerItemOption == null || _wantOption == null) return;

		int offerIdx = _offerItemOption.Selected;
		int wantIdx = _wantOption.Selected;
		if (offerIdx < 0 || offerIdx >= _offerItemIds.Count || wantIdx < 0 || wantIdx >= _wantTargets.Count) return;

		string giveItemId = _offerItemIds[offerIdx];
		var wantTarget = _wantTargets[wantIdx];

		if (wantTarget.Type == TradeAssetType.Item && wantTarget.ItemId == giveItemId)
		{
			ShowToast("Hãy chọn vật phẩm hoặc tiền tệ khác với món bạn đang đem đổi!");
			return;
		}

		if (_data.TryPostPlayerMarketListing(giveItemId, _postOfferQty, wantTarget.Type, wantTarget.ItemId, _postWantAmount))
		{
			ClosePostTradeModal();
			RebuildGrid(animate: false);
			ShowToast("Đã đăng sạp lên Chợ phiên!");
		}
	}

	private void ShowToast(string message)
	{
		if (_toastBanner == null || _toastLabel == null || string.IsNullOrEmpty(message)) return;

		_toastLabel.Text = message;
		_toastBanner.Visible = true;
		_toastBanner.Modulate = new Color(1, 1, 1, 0);

		_toastTween?.Kill();
		_toastTween = CreateTween();
		_toastTween.TweenProperty(_toastBanner, "modulate:a", 1.0f, 0.18f);
		_toastTween.TweenInterval(2.0f);
		_toastTween.TweenProperty(_toastBanner, "modulate:a", 0.0f, 0.25f);
		_toastTween.TweenCallback(Callable.From(() => _toastBanner.Visible = false));
	}

	private void AnimateModalOpen(Control modalRoot, Control dialogBox, ref Tween activeTween)
	{
		if (modalRoot == null) return;

		activeTween?.Kill();
		modalRoot.Visible = true;
		modalRoot.Modulate = new Color(1, 1, 1, 0);

		if (dialogBox != null)
		{
			if (!_modalBaseScales.TryGetValue(dialogBox, out var baseScale))
			{
				baseScale = dialogBox.Scale;
				_modalBaseScales[dialogBox] = baseScale;
			}

			dialogBox.PivotOffset = dialogBox.Size / 2f;
			dialogBox.Scale = baseScale * 0.85f;
		}

		var tween = CreateTween().SetParallel(true);
		tween.TweenProperty(modalRoot, "modulate:a", 1.0f, 0.18f)
			.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);

		if (dialogBox != null)
		{
			tween.TweenProperty(dialogBox, "scale", _modalBaseScales[dialogBox], 0.22f)
				.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		}

		activeTween = tween;
	}

	private void AnimateModalClose(Control modalRoot, Control dialogBox, ref Tween activeTween)
	{
		if (modalRoot == null || !modalRoot.Visible) return;

		activeTween?.Kill();
		var tween = CreateTween().SetParallel(true);
		tween.TweenProperty(modalRoot, "modulate:a", 0.0f, 0.15f)
			.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);

		if (dialogBox != null)
		{
			Vector2 baseScale = _modalBaseScales.TryGetValue(dialogBox, out var saved) ? saved : dialogBox.Scale;
			tween.TweenProperty(dialogBox, "scale", baseScale * 0.85f, 0.15f)
				.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
		}

		tween.Chain().TweenCallback(Callable.From(() => modalRoot.Visible = false));
		activeTween = tween;
	}

	private void OnSharedCurrenciesChanged(int coins, int rau)
	{
		_coinBar?.SetCoins(coins, animate: true);
		_rauBar?.SetCoins(rau, animate: true);
		if (_buyModal != null && _buyModal.Visible)
		{
			RefreshBuyModalPricing();
		}
	}

	private void OnInventoryChanged()
	{
		RebuildGrid(animate: false);
	}

	private void OnSelectedCharacterChanged(int _)
	{
		if (_currentTab == ShopTab.Characters)
		{
			RebuildGrid(animate: false);
		}
	}

	private bool IsCharacterUnlocked(int characterId)
	{
		var ch = _data?.GetCharacter(characterId);
		return ch != null ? ch.IsUnlocked : characterId == 0;
	}

	private int GetBalance(CurrencyType currency) =>
		_data != null ? _data.GetBalance(currency) : 0;

	private static string GetCurrencyName(CurrencyType currency) =>
		currency == CurrencyType.Rau ? "Rau" : "Vàng";

	private Texture2D GetCurrencyIcon(CurrencyType currency) =>
		currency == CurrencyType.Rau ? _rauIconTexture : _goldIconTexture;

	private int GetTradeAssetOwned(TradeAssetType type, string itemId) => type switch
	{
		TradeAssetType.Gold => _data?.Coins ?? 0,
		TradeAssetType.Rau => _data?.Rau ?? 0,
		_ => _data?.GetItemQuantity(itemId) ?? 0
	};

	private static string GetItemDisplayName(string itemId) =>
		GameCatalog.FindConsumable(itemId)?.Name ?? itemId;

	private static string FormatTradeAsset(TradeAssetType type, string itemId, int amount) => type switch
	{
		TradeAssetType.Gold => $"{amount:N0} Vàng",
		TradeAssetType.Rau => $"{amount:N0} Rau",
		_ => $"x{amount} {GetItemDisplayName(itemId)}"
	};

	private static string GetTradeAssetIconPath(TradeAssetType type, string itemId) => type switch
	{
		TradeAssetType.Gold => "res://assets/sprites/icons/coin.png",
		TradeAssetType.Rau => "res://assets/sprites/icons/rau.png",
		_ => GameCatalog.FindConsumable(itemId)?.IconPath ?? "res://assets/sprites/ui/buttons/tab_shop.png"
	};

	private static Color GetTradeAssetTint(TradeAssetType type, string itemId) => type switch
	{
		TradeAssetType.Gold or TradeAssetType.Rau => Colors.White,
		_ => GameCatalog.FindConsumable(itemId)?.IconTint ?? Colors.White
	};

	private Texture2D LoadIcon(string path)
	{
		if (string.IsNullOrEmpty(path)) return null;
		if (_iconCache.TryGetValue(path, out var cached)) return cached;

		var loaded = GD.Load<Texture2D>(path);
		if (loaded != null) _iconCache[path] = loaded;
		return loaded;
	}
}
