using System;
using System.Collections.Generic;
using Godot;
using MarketMobile.Core;
using MarketMobile.Utils;

namespace MarketMobile.UI;

public partial class CharacterScreen : Control
{
    private enum StatType
    {
        Damage,
        Health,
        Defense
    }

    // Upgrade buttons for stats (using upgrade.png)
    [Export] private BaseButton _btnUpgradeDamage;
    [Export] private BaseButton _btnUpgradeHealth;
    [Export] private BaseButton _btnUpgradeDefense;

    // Bottom Action Area
    [Export] private Button _upgradeBtn;
    [Export] private Control _upgradeDetailsPanel;

    // Per-stat detail rows
    [Export] private Control _rowDetailDamage;
    [Export] private Label _labelBonusDamage;
    [Export] private Label _labelCostDamage;

    [Export] private Control _rowDetailHealth;
    [Export] private Label _labelBonusHealth;
    [Export] private Label _labelCostHealth;

    [Export] private Control _rowDetailDefense;
    [Export] private Label _labelBonusDefense;
    [Export] private Label _labelCostDefense;

    [Export] private Label _totalCostLabel;
    [Export] private Button _resetUpgradeBtn;

    // Stat Rows UI
    [Export] private ProgressBar _progressDamage;
    [Export] private Label _labelDamage;
    [Export] private ProgressBar _progressHealth;
    [Export] private Label _labelHealth;
    [Export] private ProgressBar _progressDefense;
    [Export] private Label _labelDefense;

    private bool _isUpgradeMode = false;

    // Base stat values
    private int _currentDamage = 15;
    private int _currentHealth = 250;
    private int _currentDefense = 20;

    // Sub-Tabs ("Thông số" & "Trang phục")
    [Export] private Button _tabStats;
    [Export] private Button _tabSkins;
    [Export] private Control _statsBody;
    [Export] private Control _skinsBody;

    private StyleBox _styleTabStatsActive;
    private StyleBox _styleTabStatsInactive;
    private int _currentSubTabIndex = 0;

    // Responsive layout nodes
    private Node2D _characterDisplay;
    private Control _avatarList;
    private Control _infoPanel;

    // Cumulative bonus values (multi upgrade in 1 batch)
    private int _bonusDamage = 0;
    private int _bonusHealth = 0;
    private int _bonusDefense = 0;

    // History stack for step-by-step undo
    private readonly Stack<StatType> _upgradeHistory = new();

    // Costs per unit increment
    private const int CostPerDamage = 500;   // +5 Damage
    private const int CostPerHealth = 300;   // +10 Health
    private const int CostPerDefense = 800;  // +5% Defense

    // Styles for UpgradeBtn
    private StyleBox _defaultBtnStyle;
    private StyleBoxFlat _confirmBtnStyle;

    private CharacterProfile[] _characters;
    private PlayerDataManager _data;
    private int _selectedCharIndex = 0;

    // Character UI nodes
    private Label _nameValueLabel;
    private Label _roleValueLabel;

    // Avatar list nodes
    private readonly PanelContainer[] _avatarCards = new PanelContainer[4];
    private readonly TextureRect[] _avatarIcons = new TextureRect[4];
    private readonly TextureRect[] _avatarLocks = new TextureRect[4];
    private readonly Button[] _avatarButtons = new Button[4];
    private StyleBox _styleAvatarActive;
    private StyleBox _styleAvatarNormal;

    // Character Locked Overlay
    private Control _charLockedOverlay;
    private TextureRect _charBigLockIcon;
    private TextureRect _charLockedCurrencyIcon;
    private Label _charLockedPriceLabel;
    private Button _charBuyBtn;

    // Skins & Quick Buy
    [ExportGroup("Kinh Tế")]
    [Export] public bool OverrideSharedBalances { get; set; } = false;
    [Export] public int PlayerCoins { get; set; } = 123456;
    [Export] public int PlayerRau { get; set; } = 123456;

    private PanelContainer _cardOutfit;
    private PanelContainer _cardHat;
    private Button _btnOutfit;
    private Button _btnHat;
    private TextureRect _hatTexture;
    private TextureRect _lockIcon;
    private StyleBox _styleSkinCardNormal;
    private StyleBox _styleSkinCardActive;
    private bool _isHatUnlocked = false;
    private int _equippedSkinIndex = 0; // 0 = Áo dài đỏ, 1 = Nón hồng
    private int _playerCoins = 123456;
    private int _playerRau = 123456;
    private const int HatUnlockPrice = 1500;
    private Texture2D _goldIconTexture;
    private Texture2D _rauIconTexture;

    // Quick Buy Modal nodes
    private Control _quickBuyModal;
    private Control _modalDialogBox;
    private Control _modalBackdrop;
    private Button _modalCloseBtn;
    private Button _modalCancelBtn;
    private Button _modalBuyBtn;
    private Label _modalPriceLabel;
    private Label _modalBalanceLabel;
    private Tween _quickBuyTween;

    // Insufficient Funds Modal nodes
    private Control _failModal;
    private Control _failDialogBox;
    private Control _failBackdrop;
    private Button _failCloseBtn;
    private Button _failCloseModalBtn;
    private Button _failGoToShopBtn;
    private Label _failTitleLabel;
    private Label _failMessageLabel;
    private Label _failCostLabel;
    private Label _failCostValue;
    private Label _failCurrentValue;
    private Label _failMissingValue;
    private Tween _failModalTween;
    private readonly Dictionary<Control, Vector2> _modalBaseScales = new();
    private CoinBar _coinBar;
    private CoinBar _rauBar;

    public override void _Ready()
    {
        _data = PlayerDataManager.Instance ?? GetNodeOrNull<PlayerDataManager>("/root/PlayerDataManager");
        if (_data != null)
        {
            if (OverrideSharedBalances)
            {
                _data.SetBalances(PlayerCoins, PlayerRau);
            }

            _characters = _data.Characters;
            _playerCoins = _data.Coins;
            _playerRau = _data.Rau;
            _isHatUnlocked = _data.IsHatUnlocked;
            _equippedSkinIndex = _data.EquippedSkinIndex;
            _data.CurrenciesChanged += OnSharedCurrenciesChanged;
        }
        else
        {
            _characters = PlayerDataManager.CreateDefaultCharacters();
            _playerCoins = PlayerCoins;
            _playerRau = PlayerRau;
        }

        _goldIconTexture = GD.Load<Texture2D>("res://assets/sprites/icons/coin.png");
        _rauIconTexture = GD.Load<Texture2D>("res://assets/sprites/icons/rau.png");

        _btnUpgradeDamage ??= GetNodeOrNull<BaseButton>("InfoPanel/MainVBox/BodyMargin/BodyVBox/StatDamageRow/BtnUpgradeDamage");
        _btnUpgradeHealth ??= GetNodeOrNull<BaseButton>("InfoPanel/MainVBox/BodyMargin/BodyVBox/StatHealthRow/BtnUpgradeHealth");
        _btnUpgradeDefense ??= GetNodeOrNull<BaseButton>("InfoPanel/MainVBox/BodyMargin/BodyVBox/StatDefenseRow/BtnUpgradeDefense");

        _upgradeBtn ??= GetNodeOrNull<Button>("InfoPanel/MainVBox/BodyMargin/BodyVBox/UpgradeBtn");
        _upgradeDetailsPanel ??= GetNodeOrNull<Control>("InfoPanel/MainVBox/BodyMargin/BodyVBox/UpgradeDetailsPanel");

        _rowDetailDamage ??= GetNodeOrNull<Control>("InfoPanel/MainVBox/BodyMargin/BodyVBox/UpgradeDetailsPanel/DetailsVBox/RowDamage");
        _labelBonusDamage ??= GetNodeOrNull<Label>("InfoPanel/MainVBox/BodyMargin/BodyVBox/UpgradeDetailsPanel/DetailsVBox/RowDamage/BonusLabel");
        _labelCostDamage ??= GetNodeOrNull<Label>("InfoPanel/MainVBox/BodyMargin/BodyVBox/UpgradeDetailsPanel/DetailsVBox/RowDamage/CostHBox/CostLabel");

        _rowDetailHealth ??= GetNodeOrNull<Control>("InfoPanel/MainVBox/BodyMargin/BodyVBox/UpgradeDetailsPanel/DetailsVBox/RowHealth");
        _labelBonusHealth ??= GetNodeOrNull<Label>("InfoPanel/MainVBox/BodyMargin/BodyVBox/UpgradeDetailsPanel/DetailsVBox/RowHealth/BonusLabel");
        _labelCostHealth ??= GetNodeOrNull<Label>("InfoPanel/MainVBox/BodyMargin/BodyVBox/UpgradeDetailsPanel/DetailsVBox/RowHealth/CostHBox/CostLabel");

        _rowDetailDefense ??= GetNodeOrNull<Control>("InfoPanel/MainVBox/BodyMargin/BodyVBox/UpgradeDetailsPanel/DetailsVBox/RowDefense");
        _labelBonusDefense ??= GetNodeOrNull<Label>("InfoPanel/MainVBox/BodyMargin/BodyVBox/UpgradeDetailsPanel/DetailsVBox/RowDefense/BonusLabel");
        _labelCostDefense ??= GetNodeOrNull<Label>("InfoPanel/MainVBox/BodyMargin/BodyVBox/UpgradeDetailsPanel/DetailsVBox/RowDefense/CostHBox/CostLabel");

        _totalCostLabel ??= GetNodeOrNull<Label>("InfoPanel/MainVBox/BodyMargin/BodyVBox/UpgradeDetailsPanel/DetailsVBox/SummaryRow/TotalCostHBox/TotalCostLabel");
        _resetUpgradeBtn ??= GetNodeOrNull<Button>("InfoPanel/MainVBox/BodyMargin/BodyVBox/UpgradeDetailsPanel/DetailsVBox/SummaryRow/ResetUpgradeBtn");

        _progressDamage ??= GetNodeOrNull<ProgressBar>("InfoPanel/MainVBox/BodyMargin/BodyVBox/StatDamageRow/ProgressBar");
        _labelDamage ??= GetNodeOrNull<Label>("InfoPanel/MainVBox/BodyMargin/BodyVBox/StatDamageRow/ProgressBar/ValueLabel");
        _progressHealth ??= GetNodeOrNull<ProgressBar>("InfoPanel/MainVBox/BodyMargin/BodyVBox/StatHealthRow/ProgressBar");
        _labelHealth ??= GetNodeOrNull<Label>("InfoPanel/MainVBox/BodyMargin/BodyVBox/StatHealthRow/ProgressBar/ValueLabel");
        _progressDefense ??= GetNodeOrNull<ProgressBar>("InfoPanel/MainVBox/BodyMargin/BodyVBox/StatDefenseRow/ProgressBar");
        _labelDefense ??= GetNodeOrNull<Label>("InfoPanel/MainVBox/BodyMargin/BodyVBox/StatDefenseRow/ProgressBar/ValueLabel");

        if (_upgradeBtn != null)
        {
            _defaultBtnStyle = _upgradeBtn.GetThemeStylebox("normal");
        }

        _confirmBtnStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.15f, 0.68f, 0.35f, 1f),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            BorderColor = new Color(1f, 1f, 1f, 1f),
            CornerRadiusTopLeft = 16,
            CornerRadiusTopRight = 16,
            CornerRadiusBottomRight = 16,
            CornerRadiusBottomLeft = 16,
            ContentMarginLeft = 32,
            ContentMarginRight = 32,
            ContentMarginTop = 10,
            ContentMarginBottom = 10
        };

        // Wire up signals
        if (_upgradeBtn != null) _upgradeBtn.Pressed += OnUpgradeBtnPressed;
        if (_resetUpgradeBtn != null) _resetUpgradeBtn.Pressed += UndoLastUpgrade;

        // Gắn tính năng nhấp nhả hoặc đè nút 3 giây để tăng liên tục với tần suất kiểm soát được
        if (_btnUpgradeDamage != null)
            UIButtonHelper.AttachHoldRepeat(_btnUpgradeDamage, AddDamageBonus, 3.0f, 0.25f, () => _isUpgradeMode);

        if (_btnUpgradeHealth != null)
            UIButtonHelper.AttachHoldRepeat(_btnUpgradeHealth, AddHealthBonus, 3.0f, 0.25f, () => _isUpgradeMode);

        if (_btnUpgradeDefense != null)
            UIButtonHelper.AttachHoldRepeat(_btnUpgradeDefense, AddDefenseBonus, 3.0f, 0.25f, () => _isUpgradeMode);

        // Initial state
        SetUpgradeButtonsState(enabled: false);
        if (_upgradeDetailsPanel != null) _upgradeDetailsPanel.Visible = false;
        RefreshLabels();

        // Responsive layout setup
        _characterDisplay = GetNodeOrNull<Node2D>("CharacterDisplay");
        _avatarList = GetNodeOrNull<Control>("AvatarList");
        _infoPanel = GetNodeOrNull<Control>("InfoPanel");

        GetViewport().SizeChanged += UpdateResponsiveLayout;
        UpdateResponsiveLayout();

        // Setup Sub-Tabs ("Thông số" & "Trang phục")
        _tabStats ??= GetNodeOrNull<Button>("InfoPanel/MainVBox/TabsHeader/TabsMargin/SubTabsHBox/TabStats");
        _tabSkins ??= GetNodeOrNull<Button>("InfoPanel/MainVBox/TabsHeader/TabsMargin/SubTabsHBox/TabSkins");
        _statsBody ??= GetNodeOrNull<Control>("InfoPanel/MainVBox/BodyMargin/BodyVBox");
        _skinsBody ??= GetNodeOrNull<Control>("InfoPanel/MainVBox/BodyMargin/SkinsBody");

        if (_tabStats != null) _styleTabStatsActive = _tabStats.GetThemeStylebox("normal");
        if (_tabSkins != null) _styleTabStatsInactive = _tabSkins.GetThemeStylebox("normal");

        if (_tabStats != null) _tabStats.Pressed += () => SwitchSubTab(0);
        if (_tabSkins != null) _tabSkins.Pressed += () => SwitchSubTab(1);

        _nameValueLabel = GetNodeOrNull<Label>("InfoPanel/MainVBox/BodyMargin/BodyVBox/NameHBox/NameValue");
        _roleValueLabel = GetNodeOrNull<Label>("InfoPanel/MainVBox/BodyMargin/BodyVBox/RoleRow/RoleValueHBox/RoleValue");

        // Khởi tạo hệ thống Nhân vật & Avatar List
        SetupAvatarsAndLockedOverlay();

        // Khởi tạo hệ thống Trang phục & Popup Mua Nhanh
        SetupSkinsAndModal();

        // Khởi tạo thanh hiển thị Vàng & Rau dùng chung
        _coinBar = GetNodeOrNull<CoinBar>("CurrenciesHBox/CoinBar") ?? GetNodeOrNull<CoinBar>("CoinBar");
        if (_coinBar != null)
        {
            _coinBar.SetCoins(_playerCoins, animate: false);
            _coinBar.AddCoinPressed += NavigateToShopTab;
        }

        _rauBar = GetNodeOrNull<CoinBar>("CurrenciesHBox/RauBar") ?? GetNodeOrNull<CoinBar>("RauBar");
        if (_rauBar != null)
        {
            _rauBar.SetCoins(_playerRau, animate: false);
            _rauBar.AddCoinPressed += NavigateToShopTab;
        }

        // Gắn hiệu ứng nhún (bounce) và tắt hover cho toàn bộ các nút bấm
        UIButtonHelper.AttachToAllButtons(this);
    }

    private void SwitchSubTab(int index)
    {
        if (_currentSubTabIndex == index) return;
        _currentSubTabIndex = index;

        var colorWhite = new Color(1f, 1f, 1f, 1f);
        var colorDarkPurple = new Color(0.18f, 0.12f, 0.32f, 1f);

        if (index == 0) // Chuyển sang Tab Thông số
        {
            ApplySubTabVisual(_tabStats, _styleTabStatsActive, colorWhite, 40);
            ApplySubTabVisual(_tabSkins, _styleTabStatsInactive, colorDarkPurple, 38);

            AnimateTabContent(_skinsBody, _statsBody);
        }
        else // Chuyển sang Tab Trang phục
        {
            ApplySubTabVisual(_tabStats, _styleTabStatsInactive, colorDarkPurple, 38);
            ApplySubTabVisual(_tabSkins, _styleTabStatsActive, colorWhite, 40);

            AnimateTabContent(_statsBody, _skinsBody);
        }
    }

    private void ApplySubTabVisual(Button tab, StyleBox style, Color fontColor, int fontSize)
    {
        if (tab == null) return;
        if (style != null)
        {
            tab.AddThemeStyleboxOverride("normal", style);
            tab.AddThemeStyleboxOverride("pressed", style);
            tab.AddThemeStyleboxOverride("hover", style);
            tab.AddThemeStyleboxOverride("hover_pressed", style);
        }
        tab.AddThemeColorOverride("font_color", fontColor);
        tab.AddThemeColorOverride("font_hover_color", fontColor);
        tab.AddThemeColorOverride("font_focus_color", fontColor);
        tab.AddThemeColorOverride("font_pressed_color", fontColor);
        tab.AddThemeFontSizeOverride("font_size", fontSize);
    }

    private void AnimateTabContent(Control hideContent, Control showContent)
    {
        if (hideContent != null)
        {
            hideContent.Visible = false;
        }

        if (showContent != null)
        {
            showContent.Visible = true;
            showContent.Modulate = new Color(1, 1, 1, 0);

            var tween = CreateTween();
            tween.TweenProperty(showContent, "modulate:a", 1.0f, 0.18f)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        }
    }

    private void SetupSkinsAndModal()
    {
        _cardOutfit = GetNodeOrNull<PanelContainer>("InfoPanel/MainVBox/BodyMargin/SkinsBody/ScrollContainer/CardsMargin/CardsFlow/CardOutfit");
        _cardHat = GetNodeOrNull<PanelContainer>("InfoPanel/MainVBox/BodyMargin/SkinsBody/ScrollContainer/CardsMargin/CardsFlow/CardHat");
        _btnOutfit = GetNodeOrNull<Button>("InfoPanel/MainVBox/BodyMargin/SkinsBody/ScrollContainer/CardsMargin/CardsFlow/CardOutfit/OutfitBtn");
        _btnHat = GetNodeOrNull<Button>("InfoPanel/MainVBox/BodyMargin/SkinsBody/ScrollContainer/CardsMargin/CardsFlow/CardHat/HatBtn");
        _hatTexture = GetNodeOrNull<TextureRect>("InfoPanel/MainVBox/BodyMargin/SkinsBody/ScrollContainer/CardsMargin/CardsFlow/CardHat/HatContent/HatTexture");
        _lockIcon = GetNodeOrNull<TextureRect>("InfoPanel/MainVBox/BodyMargin/SkinsBody/ScrollContainer/CardsMargin/CardsFlow/CardHat/LockLayer/LockIcon");

        if (_cardHat != null) _styleSkinCardNormal = _cardHat.GetThemeStylebox("panel");
        if (_cardOutfit != null) _styleSkinCardActive = _cardOutfit.GetThemeStylebox("panel");

        if (_btnOutfit != null) _btnOutfit.Pressed += OnOutfitCardClicked;
        if (_btnHat != null) _btnHat.Pressed += OnHatCardClicked;

        // Khôi phục trạng thái trang phục đã mua/trang bị từ PlayerDataManager
        if (_isHatUnlocked)
        {
            if (_lockIcon != null) _lockIcon.Visible = false;
            if (_hatTexture != null) _hatTexture.Modulate = new Color(1f, 1f, 1f, 1f);
        }
        ApplySkinCardSelection();

        // Modal nodes
        _quickBuyModal = GetNodeOrNull<Control>("QuickBuyModal");
        _modalDialogBox = GetNodeOrNull<Control>("QuickBuyModal/Center/DialogBox");
        _modalBackdrop = GetNodeOrNull<Control>("QuickBuyModal/Backdrop");
        _modalCloseBtn = GetNodeOrNull<Button>("QuickBuyModal/Center/DialogBox/DialogVBox/HeaderHBox/CloseBtn");
        _modalCancelBtn = GetNodeOrNull<Button>("QuickBuyModal/Center/DialogBox/DialogVBox/ActionsHBox/CancelBtn");
        _modalBuyBtn = GetNodeOrNull<Button>("QuickBuyModal/Center/DialogBox/DialogVBox/ActionsHBox/BuyBtn");
        _modalPriceLabel = GetNodeOrNull<Label>("QuickBuyModal/Center/DialogBox/DialogVBox/PriceHBox/PriceLabel");
        _modalBalanceLabel = GetNodeOrNull<Label>("QuickBuyModal/Center/DialogBox/DialogVBox/BalanceLabel");

        if (_modalCloseBtn != null) _modalCloseBtn.Pressed += CloseQuickBuyModal;
        if (_modalCancelBtn != null) _modalCancelBtn.Pressed += CloseQuickBuyModal;
        if (_modalBuyBtn != null) _modalBuyBtn.Pressed += ConfirmQuickBuy;

        ConfigureModalOutsideClick(_quickBuyModal, _modalBackdrop, _modalDialogBox, CloseQuickBuyModal);

        if (_quickBuyModal != null) _quickBuyModal.Visible = false;

        // Insufficient Funds Modal nodes
        _failModal = GetNodeOrNull<Control>("InsufficientFundsModal");
        _failDialogBox = GetNodeOrNull<Control>("InsufficientFundsModal/Center/DialogBox");
        _failBackdrop = GetNodeOrNull<Control>("InsufficientFundsModal/Backdrop");
        _failCloseBtn = GetNodeOrNull<Button>("InsufficientFundsModal/Center/DialogBox/DialogVBox/HeaderHBox/CloseBtn");
        _failCloseModalBtn = GetNodeOrNull<Button>("InsufficientFundsModal/Center/DialogBox/DialogVBox/ActionsHBox/CloseModalBtn");
        _failGoToShopBtn = GetNodeOrNull<Button>("InsufficientFundsModal/Center/DialogBox/DialogVBox/ActionsHBox/GoToShopBtn");
        _failTitleLabel = GetNodeOrNull<Label>("InsufficientFundsModal/Center/DialogBox/DialogVBox/HeaderHBox/TitleLabel");
        _failMessageLabel = GetNodeOrNull<Label>("InsufficientFundsModal/Center/DialogBox/DialogVBox/MessageLabel");
        _failCostLabel = GetNodeOrNull<Label>("InsufficientFundsModal/Center/DialogBox/DialogVBox/DetailBox/DetailVBox/CostRow/Label");
        _failCostValue = GetNodeOrNull<Label>("InsufficientFundsModal/Center/DialogBox/DialogVBox/DetailBox/DetailVBox/CostRow/Value");
        _failCurrentValue = GetNodeOrNull<Label>("InsufficientFundsModal/Center/DialogBox/DialogVBox/DetailBox/DetailVBox/CurrentRow/Value");
        _failMissingValue = GetNodeOrNull<Label>("InsufficientFundsModal/Center/DialogBox/DialogVBox/DetailBox/DetailVBox/MissingRow/Value");

        if (_failCloseBtn != null) _failCloseBtn.Pressed += CloseFailModal;
        if (_failCloseModalBtn != null) _failCloseModalBtn.Pressed += CloseFailModal;
        if (_failGoToShopBtn != null) _failGoToShopBtn.Pressed += OnGoToShopClicked;

        ConfigureModalOutsideClick(_failModal, _failBackdrop, _failDialogBox, CloseFailModal);

        if (_failModal != null) _failModal.Visible = false;
    }

    /// <summary>
    /// Đảm bảo vùng bên ngoài DialogBox truyền sự kiện bấm/chạm xuống Backdrop để đóng modal,
    /// đồng thời chặn click xuyên vào nội dung màn hình phía sau hoặc vào vùng bên trong hộp thoại.
    /// </summary>
    private static void ConfigureModalOutsideClick(Control modalRoot, Control backdrop, Control dialogBox, Action onDismiss)
    {
        if (modalRoot == null) return;

        var center = modalRoot.GetNodeOrNull<Control>("Center");
        if (center != null) center.MouseFilter = MouseFilterEnum.Ignore;
        if (dialogBox != null) dialogBox.MouseFilter = MouseFilterEnum.Stop;

        if (backdrop != null)
        {
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
    }

    private void SetupAvatarsAndLockedOverlay()
    {
        for (int i = 0; i < 4; i++)
        {
            int index = i;
            var card = GetNodeOrNull<PanelContainer>($"AvatarList/Avatar{i}");
            _avatarCards[i] = card;
            if (card != null)
            {
                _avatarIcons[i] = card.GetNodeOrNull<TextureRect>("AvatarIcon");
                _avatarLocks[i] = card.GetNodeOrNull<TextureRect>("LockCenter/LockIcon");
                _avatarButtons[i] = card.GetNodeOrNull<Button>("AvatarBtn");

                if (i == 0) _styleAvatarActive = card.GetThemeStylebox("panel");
                if (i == 1) _styleAvatarNormal = card.GetThemeStylebox("panel");

                if (_avatarButtons[i] != null)
                {
                    _avatarButtons[i].Pressed += () => SelectCharacter(index);
                }
            }
        }

        _charLockedOverlay = GetNodeOrNull<Control>("InfoPanel/MainVBox/BodyMargin/CharacterLockedOverlay")
            ?? GetNodeOrNull<Control>("InfoPanel/CharacterLockedOverlay");
        _charBigLockIcon = _charLockedOverlay?.FindChild("BigLockIcon", true, false) as TextureRect;
        _charLockedCurrencyIcon = _charLockedOverlay?.FindChild("CoinIcon", true, false) as TextureRect;
        _charLockedPriceLabel = _charLockedOverlay?.FindChild("PriceLabel", true, false) as Label;
        _charBuyBtn = _charLockedOverlay?.FindChild("BuyCharBtn", true, false) as Button;

        if (_charBuyBtn != null)
        {
            _charBuyBtn.Pressed += OnBuyCurrentCharacterClicked;
        }

        // Khởi tạo trạng thái nhân vật ban đầu theo dữ liệu dùng chung
        int initialIndex = _data?.SelectedCharacterIndex ?? 0;
        SelectCharacter(initialIndex, animate: false);
    }

    private void SelectCharacter(int index, bool animate = true)
    {
        if (index < 0 || index >= _characters.Length) return;

        // Reset các điểm nâng cấp chưa xác nhận của nhân vật trước
        if (_isUpgradeMode)
        {
            _isUpgradeMode = false;
            SetUpgradeButtonsState(enabled: false);
            if (_upgradeBtn != null)
            {
                _upgradeBtn.Text = "Nâng cấp";
                ApplyBtnStyle(_defaultBtnStyle);
            }
        }
        ResetAllCumulativeBonuses();

        _selectedCharIndex = index;
        var character = _characters[index];
        if (character.IsUnlocked)
        {
            _data?.SetSelectedCharacter(index);
        }

        // Cập nhật tên và vai trò
        if (_nameValueLabel != null) _nameValueLabel.Text = character.Name;
        if (_roleValueLabel != null) _roleValueLabel.Text = character.Role;

        // Cập nhật chỉ số
        _currentDamage = character.Damage;
        _currentHealth = character.Health;
        _currentDefense = character.Defense;

        if (_progressDamage != null) _progressDamage.Value = _currentDamage;
        if (_progressHealth != null) _progressHealth.Value = Mathf.Clamp((float)_currentHealth / 400f * 100f, 0, 100);
        if (_progressDefense != null) _progressDefense.Value = _currentDefense;
        RefreshLabels();

        // Cập nhật màu hiển thị nhân vật ở giữa sân khấu
        if (_characterDisplay != null)
        {
            var targetColor = character.IsUnlocked ? character.Tint : character.Tint * new Color(0.7f, 0.7f, 0.75f, 0.85f);
            if (animate)
            {
                var tween = _characterDisplay.CreateTween();
                tween.TweenProperty(_characterDisplay, "modulate", targetColor, 0.2f)
                    .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            }
            else
            {
                _characterDisplay.Modulate = targetColor;
            }
        }

        // Cập nhật giao diện Lock Overlay trên InfoPanel
        if (_charLockedOverlay != null)
        {
            if (character.IsUnlocked)
            {
                if (animate && _charLockedOverlay.Visible)
                {
                    var tween = _charLockedOverlay.CreateTween();
                    tween.TweenProperty(_charLockedOverlay, "modulate:a", 0.0f, 0.15f)
                        .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
                    tween.Chain().TweenCallback(Callable.From(() =>
                    {
                        _charLockedOverlay.Visible = false;
                    }));
                }
                else
                {
                    _charLockedOverlay.Visible = false;
                }

                if (_upgradeBtn != null) _upgradeBtn.Disabled = false;
            }
            else
            {
                _charLockedOverlay.Visible = true;
                _charLockedOverlay.Modulate = new Color(1, 1, 1, 1);
                if (_charBigLockIcon != null)
                {
                    _charBigLockIcon.Scale = Vector2.One;
                    _charBigLockIcon.Modulate = new Color(1, 1, 1, 1f);
                }
                if (_charLockedPriceLabel != null)
                {
                    _charLockedPriceLabel.Text = $"{character.Price:N0} {GetCurrencyName(character.Currency)}";
                }
                if (_charLockedCurrencyIcon != null)
                {
                    var currencyIcon = GetCurrencyIcon(character.Currency);
                    if (currencyIcon != null) _charLockedCurrencyIcon.Texture = currencyIcon;
                }
                if (_upgradeBtn != null) _upgradeBtn.Disabled = true;
            }
        }

        // Cập nhật viền cho hàng Avatar
        ApplyAvatarVisuals();
    }

    private void ApplyAvatarVisuals()
    {
        for (int i = 0; i < 4; i++)
        {
            var card = _avatarCards[i];
            if (card != null && _styleAvatarActive != null && _styleAvatarNormal != null)
            {
                card.AddThemeStyleboxOverride("panel", i == _selectedCharIndex ? _styleAvatarActive : _styleAvatarNormal);
            }

            var lockIcon = _avatarLocks[i];
            if (lockIcon != null)
            {
                lockIcon.Visible = !_characters[i].IsUnlocked;
            }

            var avatarIcon = _avatarIcons[i];
            if (avatarIcon != null)
            {
                avatarIcon.Modulate = _characters[i].IsUnlocked
                    ? new Color(1, 1, 1, 1)
                    : new Color(0.45f, 0.45f, 0.55f, 0.8f);
            }
        }
    }

    private void OnBuyCurrentCharacterClicked()
    {
        var character = _characters[_selectedCharIndex];
        if (character.IsUnlocked) return;

        bool unlocked = _data != null
            ? _data.TryUnlockCharacter(_selectedCharIndex)
            : TrySpendCurrencyLocal(character.Currency, character.Price);

        if (unlocked)
        {
            character.IsUnlocked = true;

            // Hiệu ứng mở khóa hoành tráng:
            // 1. Ổ khóa lớn bung to và mờ dần
            if (_charBigLockIcon != null)
            {
                _charBigLockIcon.PivotOffset = _charBigLockIcon.Size / 2f;
                var lockTween = _charBigLockIcon.CreateTween().SetParallel(true);
                lockTween.TweenProperty(_charBigLockIcon, "scale", new Vector2(1.4f, 1.4f), 0.22f)
                    .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
                lockTween.TweenProperty(_charBigLockIcon, "modulate:a", 0.0f, 0.22f)
                    .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            }

            // 2. Overlay mờ biến mất
            if (_charLockedOverlay != null)
            {
                var overlayTween = _charLockedOverlay.CreateTween();
                overlayTween.TweenProperty(_charLockedOverlay, "modulate:a", 0.0f, 0.25f)
                    .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
                overlayTween.Chain().TweenCallback(Callable.From(() =>
                {
                    _charLockedOverlay.Visible = false;
                    _charLockedOverlay.Modulate = new Color(1, 1, 1, 1);
                    if (_charBigLockIcon != null)
                    {
                        _charBigLockIcon.Scale = Vector2.One;
                        _charBigLockIcon.Modulate = new Color(1, 1, 1, 1f);
                    }
                }));
            }

            // 3. Ổ khóa trên avatar biến mất & avatar sáng 100%
            var avatarLock = _avatarLocks[_selectedCharIndex];
            if (avatarLock != null)
            {
                avatarLock.PivotOffset = avatarLock.Size / 2f;
                var avTween = avatarLock.CreateTween().SetParallel(true);
                avTween.TweenProperty(avatarLock, "scale", new Vector2(1.5f, 1.5f), 0.2f);
                avTween.TweenProperty(avatarLock, "modulate:a", 0.0f, 0.2f);
                avTween.Chain().TweenCallback(Callable.From(() =>
                {
                    avatarLock.Visible = false;
                    avatarLock.Scale = Vector2.One;
                    avatarLock.Modulate = new Color(1, 1, 1, 1);
                }));
            }

            var avatarIcon = _avatarIcons[_selectedCharIndex];
            if (avatarIcon != null)
            {
                var iconTween = avatarIcon.CreateTween();
                iconTween.TweenProperty(avatarIcon, "modulate", new Color(1, 1, 1, 1), 0.25f);
            }

            // 4. Nhân vật trên sân khấu sáng rực rỡ
            if (_characterDisplay != null)
            {
                var displayTween = _characterDisplay.CreateTween();
                displayTween.TweenProperty(_characterDisplay, "modulate", character.Tint, 0.25f);
            }

            // 5. Bật nút nâng cấp
            if (_upgradeBtn != null) _upgradeBtn.Disabled = false;
        }
        else
        {
            // Không đủ tiền -> Mở modal cảnh báo đúng loại tiền tệ & nhân vật
            string unit = GetCurrencyName(character.Currency);
            OpenFailModal(
                character.Price,
                $"Bạn không có đủ {unit} để mở khóa nhân vật {character.Name}!",
                character.Currency,
                "Giá mở khóa:"
            );
        }
    }

    private void OnOutfitCardClicked()
    {
        _equippedSkinIndex = 0;
        _data?.SetEquippedSkin(0);
        ApplySkinCardSelection();
    }

    private void OnHatCardClicked()
    {
        if (!_isHatUnlocked)
        {
            OpenQuickBuyModal();
        }
        else
        {
            _equippedSkinIndex = 1;
            _data?.SetEquippedSkin(1);
            ApplySkinCardSelection();
        }
    }

    private void ApplySkinCardSelection()
    {
        if (_cardOutfit != null && _styleSkinCardActive != null && _styleSkinCardNormal != null)
        {
            _cardOutfit.AddThemeStyleboxOverride("panel", _equippedSkinIndex == 0 ? _styleSkinCardActive : _styleSkinCardNormal);
        }
        if (_cardHat != null && _styleSkinCardActive != null && _styleSkinCardNormal != null)
        {
            _cardHat.AddThemeStyleboxOverride("panel", _equippedSkinIndex == 1 ? _styleSkinCardActive : _styleSkinCardNormal);
        }
    }

    private void OpenQuickBuyModal()
    {
        if (_quickBuyModal == null) return;
        bool hasEnough = _playerCoins >= HatUnlockPrice;
        if (_modalBalanceLabel != null)
        {
            if (hasEnough)
            {
                _modalBalanceLabel.Text = $"Số dư hiện tại: {_playerCoins:N0} Vàng";
                _modalBalanceLabel.AddThemeColorOverride("font_color", new Color(0.65f, 0.6f, 0.8f, 1f));
            }
            else
            {
                _modalBalanceLabel.Text = $"Số dư hiện tại: {_playerCoins:N0} Vàng (Không đủ)";
                _modalBalanceLabel.AddThemeColorOverride("font_color", new Color(1f, 0.42f, 0.42f, 1f));
            }
        }

        AnimateModalOpen(_quickBuyModal, _modalDialogBox, ref _quickBuyTween);
    }

    private void CloseQuickBuyModal()
    {
        AnimateModalClose(_quickBuyModal, _modalDialogBox, ref _quickBuyTween);
    }

    private void ConfirmQuickBuy()
    {
        bool unlocked = _data != null
            ? _data.TryUnlockHat(HatUnlockPrice)
            : TrySpendCoinsLocal(HatUnlockPrice);

        if (unlocked)
        {
            _isHatUnlocked = true;
            _equippedSkinIndex = 1;

            CloseQuickBuyModal();

            // Hiệu ứng mở khóa hoành tráng trên thẻ Nón hồng:
            // 1. Ổ khóa nảy bung lên và tan biến (fade out)
            if (_lockIcon != null)
            {
                _lockIcon.PivotOffset = _lockIcon.Size / 2f;
                var lockTween = _lockIcon.CreateTween().SetParallel(true);
                lockTween.TweenProperty(_lockIcon, "scale", new Vector2(1.5f, 1.5f), 0.22f)
                    .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
                lockTween.TweenProperty(_lockIcon, "modulate:a", 0.0f, 0.22f)
                    .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
                lockTween.Chain().TweenCallback(Callable.From(() =>
                {
                    _lockIcon.Visible = false;
                }));
            }

            // 2. Chiếc nón sáng rực rỡ từ 50% lên 100% full màu
            if (_hatTexture != null)
            {
                var hatTween = _hatTexture.CreateTween();
                hatTween.TweenProperty(_hatTexture, "modulate", new Color(1f, 1f, 1f, 1f), 0.25f)
                    .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            }

            // 3. Tự động khoác lên người: gán viền active cho CardHat
            ApplySkinCardSelection();
        }
        else
        {
            // Không đủ tiền: Đóng QuickBuyModal và hiển thị Modal Không Đủ Vàng!
            CloseQuickBuyModal();
            OpenFailModal(
                HatUnlockPrice,
                "Bạn không có đủ Vàng để mở khóa trang phục Nón Hoa Hồng!",
                CurrencyType.Gold,
                "Giá mở khóa:"
            );
        }
    }

    private void OpenFailModal(int price, string message, CurrencyType currency = CurrencyType.Gold, string costTitle = "Giá mở khóa:")
    {
        if (_failModal == null) return;

        int balance = GetBalance(currency);
        int missing = Mathf.Max(0, price - balance);
        string unit = GetCurrencyName(currency);

        if (_failTitleLabel != null) _failTitleLabel.Text = currency == CurrencyType.Rau ? "Không đủ Rau" : "Không đủ Vàng";
        if (_failMessageLabel != null) _failMessageLabel.Text = message;
        if (_failCostLabel != null) _failCostLabel.Text = costTitle;
        if (_failCostValue != null) _failCostValue.Text = $"{price:N0} {unit}";
        if (_failCurrentValue != null) _failCurrentValue.Text = $"{balance:N0} {unit}";
        if (_failMissingValue != null) _failMissingValue.Text = $"{missing:N0} {unit}";

        AnimateModalOpen(_failModal, _failDialogBox, ref _failModalTween);
    }

    private void CloseFailModal()
    {
        AnimateModalClose(_failModal, _failDialogBox, ref _failModalTween);
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
            Vector2 targetScale = _modalBaseScales[dialogBox];
            tween.TweenProperty(dialogBox, "scale", targetScale, 0.22f)
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

        tween.Chain().TweenCallback(Callable.From(() =>
        {
            modalRoot.Visible = false;
        }));

        activeTween = tween;
    }

    private void OnGoToShopClicked()
    {
        CloseFailModal();
        NavigateToShopTab();
    }

    private void NavigateToShopTab()
    {
        // Chuyển sang Tab Cửa Hàng (Tab 3) trên LeftSidebar
        var sidebar = GetNodeOrNull<LeftSidebar>("LeftSidebar");
        if (sidebar != null)
        {
            var tabShop = sidebar.GetNodeOrNull<SidebarTab>("TabsVBox/Tab3_CuaHang");
            if (tabShop != null)
            {
                tabShop.EmitSignal(SidebarTab.SignalName.TabSelected, 3);
            }
        }
    }

    private static string GetCurrencyName(CurrencyType currency) =>
        currency == CurrencyType.Rau ? "Rau" : "Vàng";

    private int GetBalance(CurrencyType currency) =>
        currency == CurrencyType.Rau ? _playerRau : _playerCoins;

    private Texture2D GetCurrencyIcon(CurrencyType currency) =>
        currency == CurrencyType.Rau ? _rauIconTexture : _goldIconTexture;

    private void OnSharedCurrenciesChanged(int coins, int rau)
    {
        _playerCoins = coins;
        _playerRau = rau;
        _coinBar?.SetCoins(_playerCoins, animate: true);
        _rauBar?.SetCoins(_playerRau, animate: true);
    }

    private bool TrySpendCoinsLocal(int amount) => TrySpendCurrencyLocal(CurrencyType.Gold, amount);

    private bool TrySpendCurrencyLocal(CurrencyType currency, int amount)
    {
        if (GetBalance(currency) < amount) return false;

        if (currency == CurrencyType.Rau)
        {
            _playerRau -= amount;
            _rauBar?.SetCoins(_playerRau, animate: true);
        }
        else
        {
            _playerCoins -= amount;
            _coinBar?.SetCoins(_playerCoins, animate: true);
        }

        return true;
    }

    public override void _ExitTree()
    {
        if (_data != null)
        {
            _data.CurrenciesChanged -= OnSharedCurrenciesChanged;
        }
        if (GetViewport() != null)
        {
            GetViewport().SizeChanged -= UpdateResponsiveLayout;
        }
    }

    private void UpdateResponsiveLayout()
    {
        var viewportSize = GetViewportRect().Size;
        var center = viewportSize / 2f;

        // 1. Căn nhân vật (Bà 7) luôn đồng bộ 100% với vòng tròn ma thuật của background
        if (_characterDisplay != null)
        {
            _characterDisplay.Position = new Vector2(center.X - 137f, center.Y + 15f);
        }

        // 2. Căn hàng 4 Avatar luôn nằm ngay ngắn dưới chân nhân vật và cách đáy an toàn
        if (_avatarList != null)
        {
            _avatarList.AnchorLeft = 0.5f;
            _avatarList.AnchorRight = 0.5f;
            _avatarList.AnchorTop = 1.0f;
            _avatarList.AnchorBottom = 1.0f;

            const float halfWidth = 255f; // Chiều rộng 510 / 2
            _avatarList.OffsetLeft = -137f - halfWidth;
            _avatarList.OffsetRight = -137f + halfWidth;
            _avatarList.OffsetTop = -160f;
            _avatarList.OffsetBottom = -60f;
        }

        // 3. Căn bảng thông số (InfoPanel) bám mép phải với khoảng đệm an toàn
        if (_infoPanel != null)
        {
            _infoPanel.AnchorLeft = 1.0f;
            _infoPanel.AnchorRight = 1.0f;
            _infoPanel.AnchorTop = 0.0f;
            _infoPanel.AnchorBottom = 0.0f;

            _infoPanel.OffsetRight = -80f;
            _infoPanel.OffsetLeft = -700f; // Rộng 620px
            _infoPanel.OffsetTop = 150f;
            _infoPanel.OffsetBottom = 760f; // Cao 610px
        }
    }

    private void OnUpgradeBtnPressed()
    {
        // If there are cumulative bonuses, big button acts as "Xác nhận"
        if (HasAnyBonus())
        {
            ExecuteConfirmUpgrade();
            return;
        }

        // Otherwise toggle upgrade mode on/off
        _isUpgradeMode = !_isUpgradeMode;
        SetUpgradeButtonsState(enabled: _isUpgradeMode);

        if (_upgradeBtn != null)
        {
            _upgradeBtn.Text = _isUpgradeMode ? "Hủy" : "Nâng cấp";
            ApplyBtnStyle(_defaultBtnStyle);
        }

        if (!_isUpgradeMode)
        {
            ResetAllCumulativeBonuses();
        }
    }

    private void SetUpgradeButtonsState(bool enabled)
    {
        var color = enabled ? new Color(1f, 1f, 1f, 1f) : new Color(1f, 1f, 1f, 0.35f);

        SetButtonState(_btnUpgradeDamage, enabled, color);
        SetButtonState(_btnUpgradeHealth, enabled, color);
        SetButtonState(_btnUpgradeDefense, enabled, color);
    }

    private void SetButtonState(BaseButton btn, bool enabled, Color color)
    {
        if (btn == null) return;
        btn.Modulate = color;
        if (btn is TextureButton tb) tb.Disabled = !enabled;
        else if (btn is Button b) b.Disabled = !enabled;
    }

    private void AddDamageBonus()
    {
        if (!_isUpgradeMode) return;
        if (_currentDamage + _bonusDamage + 5 <= 100)
        {
            _bonusDamage += 5;
            _upgradeHistory.Push(StatType.Damage);
            UpdateCumulativeUI();
        }
    }

    private void AddHealthBonus()
    {
        if (!_isUpgradeMode) return;
        _bonusHealth += 10;
        _upgradeHistory.Push(StatType.Health);
        UpdateCumulativeUI();
    }

    private void AddDefenseBonus()
    {
        if (!_isUpgradeMode) return;
        if (_currentDefense + _bonusDefense + 5 <= 100)
        {
            _bonusDefense += 5;
            _upgradeHistory.Push(StatType.Defense);
            UpdateCumulativeUI();
        }
    }

    /// <summary>
    /// Undoes only the most recently added stat increment (step-by-step undo)
    /// </summary>
    private void UndoLastUpgrade()
    {
        if (_upgradeHistory.Count == 0) return;

        var lastStat = _upgradeHistory.Pop();
        switch (lastStat)
        {
            case StatType.Damage:
                _bonusDamage = Mathf.Max(0, _bonusDamage - 5);
                break;
            case StatType.Health:
                _bonusHealth = Mathf.Max(0, _bonusHealth - 10);
                break;
            case StatType.Defense:
                _bonusDefense = Mathf.Max(0, _bonusDefense - 5);
                break;
        }

        UpdateCumulativeUI();
    }

    private bool HasAnyBonus() => _bonusDamage > 0 || _bonusHealth > 0 || _bonusDefense > 0;

    private int CalculateTotalCost()
    {
        int cost = 0;
        if (_bonusDamage > 0) cost += (_bonusDamage / 5) * CostPerDamage;
        if (_bonusHealth > 0) cost += (_bonusHealth / 10) * CostPerHealth;
        if (_bonusDefense > 0) cost += (_bonusDefense / 5) * CostPerDefense;
        return cost;
    }

    private void UpdateCumulativeUI()
    {
        RefreshLabels();

        // Highlight icons that have active bonuses
        if (_btnUpgradeDamage != null)
            _btnUpgradeDamage.Modulate = _bonusDamage > 0 ? new Color(1f, 0.9f, 0.2f, 1f) : new Color(1f, 1f, 1f, 1f);
        if (_btnUpgradeHealth != null)
            _btnUpgradeHealth.Modulate = _bonusHealth > 0 ? new Color(1f, 0.9f, 0.2f, 1f) : new Color(1f, 1f, 1f, 1f);
        if (_btnUpgradeDefense != null)
            _btnUpgradeDefense.Modulate = _bonusDefense > 0 ? new Color(1f, 0.9f, 0.2f, 1f) : new Color(1f, 1f, 1f, 1f);

        if (HasAnyBonus())
        {
            if (_upgradeDetailsPanel != null) _upgradeDetailsPanel.Visible = true;

            // Damage line
            if (_rowDetailDamage != null)
            {
                _rowDetailDamage.Visible = _bonusDamage > 0;
                if (_bonusDamage > 0)
                {
                    if (_labelBonusDamage != null) _labelBonusDamage.Text = $"+{_bonusDamage}";
                    if (_labelCostDamage != null) _labelCostDamage.Text = ((_bonusDamage / 5) * CostPerDamage).ToString("N0");
                }
            }

            // Health line
            if (_rowDetailHealth != null)
            {
                _rowDetailHealth.Visible = _bonusHealth > 0;
                if (_bonusHealth > 0)
                {
                    if (_labelBonusHealth != null) _labelBonusHealth.Text = $"+{_bonusHealth}";
                    if (_labelCostHealth != null) _labelCostHealth.Text = ((_bonusHealth / 10) * CostPerHealth).ToString("N0");
                }
            }

            // Defense line
            if (_rowDetailDefense != null)
            {
                _rowDetailDefense.Visible = _bonusDefense > 0;
                if (_bonusDefense > 0)
                {
                    if (_labelBonusDefense != null) _labelBonusDefense.Text = $"+{_bonusDefense}%";
                    if (_labelCostDefense != null) _labelCostDefense.Text = ((_bonusDefense / 5) * CostPerDefense).ToString("N0");
                }
            }

            // Total cost in summary row
            if (_totalCostLabel != null)
            {
                _totalCostLabel.Text = CalculateTotalCost().ToString("N0");
            }

            // Big button becomes "Xác nhận"
            if (_upgradeBtn != null)
            {
                _upgradeBtn.Text = "Xác nhận";
                ApplyBtnStyle(_confirmBtnStyle);
            }
        }
        else
        {
            if (_upgradeDetailsPanel != null) _upgradeDetailsPanel.Visible = false;

            if (_upgradeBtn != null)
            {
                _upgradeBtn.Text = _isUpgradeMode ? "Hủy" : "Nâng cấp";
                ApplyBtnStyle(_defaultBtnStyle);
            }
        }
    }

    private void RefreshLabels()
    {
        if (_labelDamage != null)
        {
            _labelDamage.Text = _bonusDamage > 0
                ? $"{_currentDamage} (+{_bonusDamage})/100"
                : $"{_currentDamage}/100";
        }

        if (_labelHealth != null)
        {
            _labelHealth.Text = _bonusHealth > 0
                ? $"{_currentHealth} (+{_bonusHealth})"
                : $"{_currentHealth}";
        }

        if (_labelDefense != null)
        {
            _labelDefense.Text = _bonusDefense > 0
                ? $"{_currentDefense}% (+{_bonusDefense}%)"
                : $"{_currentDefense}%";
        }
    }

    private void ResetAllCumulativeBonuses()
    {
        _upgradeHistory.Clear();
        _bonusDamage = 0;
        _bonusHealth = 0;
        _bonusDefense = 0;

        UpdateCumulativeUI();
    }

    private void ExecuteConfirmUpgrade()
    {
        int totalCost = CalculateTotalCost();
        bool upgraded = _data != null
            ? _data.TryUpgradeStats(_selectedCharIndex, _bonusDamage, _bonusHealth, _bonusDefense, totalCost)
            : TrySpendCoinsLocal(totalCost);

        if (!upgraded)
        {
            OpenFailModal(
                totalCost,
                "Bạn không có đủ Vàng để thực hiện nâng cấp chỉ số!",
                CurrencyType.Gold,
                "Chi phí nâng cấp:"
            );
            return;
        }

        if (_data != null)
        {
            _currentDamage = _characters[_selectedCharIndex].Damage;
            _currentHealth = _characters[_selectedCharIndex].Health;
            _currentDefense = _characters[_selectedCharIndex].Defense;
        }
        else
        {
            _currentDamage = Mathf.Min(100, _currentDamage + _bonusDamage);
            _currentHealth += _bonusHealth;
            _currentDefense = Mathf.Min(100, _currentDefense + _bonusDefense);
        }

        // Update progress bars to new base values
        if (_progressDamage != null) _progressDamage.Value = _currentDamage;
        if (_progressHealth != null) _progressHealth.Value = Mathf.Clamp((float)_currentHealth / 400f * 100f, 0, 100);
        if (_progressDefense != null) _progressDefense.Value = _currentDefense;

        // Reset bonuses and history
        _upgradeHistory.Clear();
        _bonusDamage = 0;
        _bonusHealth = 0;
        _bonusDefense = 0;

        RefreshLabels();

        if (_upgradeDetailsPanel != null) _upgradeDetailsPanel.Visible = false;

        // Finish upgrade mode
        _isUpgradeMode = false;
        SetUpgradeButtonsState(enabled: false);

        if (_upgradeBtn != null)
        {
            _upgradeBtn.Text = "Nâng cấp";
            ApplyBtnStyle(_defaultBtnStyle);
        }
    }

    private void ApplyBtnStyle(StyleBox style)
    {
        if (_upgradeBtn == null || style == null) return;
        _upgradeBtn.AddThemeStyleboxOverride("normal", style);
        _upgradeBtn.AddThemeStyleboxOverride("hover", style);
        _upgradeBtn.AddThemeStyleboxOverride("pressed", style);
    }
}
