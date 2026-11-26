using System;
using System.Collections.Generic;
using Godot;
using MarketMobile.Core;
using MarketMobile.Utils;

namespace MarketMobile.UI;

public partial class EventsScreen : Control
{
	[Export] private PackedScene _posterCardScene;
	[Export] private PackedScene _rewardItemScene;

	private PlayerDataManager _data;
	private LeftSidebar _shellSidebar;

	private Control _currenciesHBox;
	private CoinBar _ticketBar;
	private CoinBar _coinBar;
	private CoinBar _rauBar;

	private Control _posterListView;
	private Control _gachaDetailView;
	private Button _posterHitBtn;
	private BaseButton _backBtn;
	private Button _ratesBtn;
	private Button _rulesBtn;

	private Control _glowRing;
	private TextureRect _chestImage;
	private readonly Texture2D[] _chestFrames = new Texture2D[32];
	private Texture2D _greenCardTex;
	private Texture2D _blueCardTex;
	private readonly Texture2D[] _purpleCardFrames = new Texture2D[12];
	private readonly Texture2D[] _goldCardFrames = new Texture2D[12];
	private readonly Texture2D[] _redCardFrames = new Texture2D[12];
	private StyleBoxEmpty _emptyCardStyle;
	private Label _milestoneLabel;
	private ProgressBar _milestoneBar;
	private Button _resetMilestonesBtn;
	private readonly Dictionary<int, Button> _milestoneButtons = new();
	private StyleBoxFlat _milestoneLockedStyle;
	private StyleBoxFlat _milestoneReadyStyle;
	private StyleBoxFlat _milestoneClaimedStyle;
	private Button _roll1Btn;
	private Button _roll10Btn;

	// Drop Rate Modal
	private Control _dropRateModal;
	private Control _dropRateBackdrop;
	private Control _dropRateDialog;
	private Button _dropRateCloseBtn;
	private Button _dropRateOkBtn;
	private Tween _dropRateTween;

	private Control _toastBanner;
	private Label _toastLabel;
	private Tween _toastTween;

	// Buy Ticket Modal
	private Control _buyTicketModal;
	private Control _buyTicketBackdrop;
	private Control _buyTicketDialog;
	private Button _buyTicketCloseBtn;
	private Button _buyTicketMinusBtn;
	private Button _buyTicketPlusBtn;
	private Button _buyTicketPreset10Btn;
	private Button _buyTicketPreset50Btn;
	private Button _maxByGoldBtn;
	private Button _maxByRauBtn;
	private LineEdit _buyTicketQtyInput;
	private Label _buyTicketBalanceLabel;
	private Button _buyWithGoldBtn;
	private Button _buyWithRauBtn;
	private Tween _buyTicketTween;
	private LeftSidebar _localSidebar;
	private int _buyTicketQty = 1;

	// Rules Modal
	private Control _rulesModal;
	private Control _rulesBackdrop;
	private Control _rulesDialog;
	private Button _rulesCloseBtn;
	private Button _rulesOkBtn;
	private Tween _rulesTween;

	// Reward Result Modal
	private Control _rewardModal;
	private Control _rewardBackdrop;
	private Control _rewardDialog;
	private Button _rewardCloseBtn;
	private Button _rewardOkBtn;
	private Button _modalRoll1Btn;
	private Button _modalRoll10Btn;
	private GridContainer _rewardsGrid;
	private Label _rewardTitleLabel;
	private Tween _rewardTween;
	private readonly List<Action> _pendingRevealActions = new();
	private readonly List<GachaRewardResult> _queuedMilestoneRewards = new();

	private Tween _viewSwitchTween;
	private Tween _chestIdleTween;
	private Tween _chestRollTween;
	private bool _isRolling = false;
	private readonly Dictionary<string, Texture2D> _iconCache = new();
	private readonly Dictionary<Control, Vector2> _modalBaseScales = new();

	public override void _Ready()
	{
		_data = PlayerDataManager.Instance ?? GetNodeOrNull<PlayerDataManager>("/root/PlayerDataManager");
		_posterCardScene ??= GD.Load<PackedScene>("res://prefabs/ui/event_poster_card.tscn");
		_rewardItemScene ??= GD.Load<PackedScene>("res://prefabs/ui/gacha_reward_item.tscn");
		LoadChestAnimationFrames();

		InitializeMilestoneStyles();
		BindNodes();
		BindSignals();

		if (_data != null)
		{
			_data.CurrenciesChanged += OnSharedCurrenciesChanged;
			_data.GachaStateChanged += OnGachaStateChanged;
			SyncAllBalancesAndPity(animate: false);
		}

		_localSidebar = GetNodeOrNull<LeftSidebar>("LeftSidebar");
		if (_localSidebar != null)
		{
			_localSidebar.TabReselected += OnLocalSidebarTabReselected;
		}

		UIButtonHelper.AttachToAllButtons(this);
		ShowPosterList(animate: false);
	}

	public override void _ExitTree()
	{
		_chestIdleTween?.Kill();
		_chestRollTween?.Kill();
		_viewSwitchTween?.Kill();
		_toastTween?.Kill();

		if (_localSidebar != null)
		{
			_localSidebar.TabReselected -= OnLocalSidebarTabReselected;
		}

		if (_data != null)
		{
			_data.CurrenciesChanged -= OnSharedCurrenciesChanged;
			_data.GachaStateChanged -= OnGachaStateChanged;
		}
	}

	/// <summary>
	/// Cấu hình EventsScreen khi được nhúng trong Persistent Shell (lobby_screen):
	/// ẩn sidebar cục bộ, ẩn background trùng và đưa modal lên overlay host dùng chung.
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
			ReparentOverlay(_toastBanner, modalHost);
			ReparentOverlay(_buyTicketModal, modalHost);
			ReparentOverlay(_dropRateModal, modalHost);
			ReparentOverlay(_rulesModal, modalHost);
			ReparentOverlay(_rewardModal, modalHost);
		}
	}

	private static void ReparentOverlay(Control node, Control modalHost)
	{
		if (node != null && node.GetParent() != modalHost)
		{
			node.Reparent(modalHost, keepGlobalTransform: false);
		}
	}

	/// <summary>
	/// Quay về danh sách poster sự kiện khi đang ở chi tiết Rương May Mắn.
	/// </summary>
	public void ReturnToPosterList(bool animate = true)
	{
		if (_gachaDetailView != null && _gachaDetailView.Visible)
		{
			ShowPosterList(animate);
		}
	}

	private void OnLocalSidebarTabReselected(int tabIndex)
	{
		if (tabIndex == 2)
		{
			ReturnToPosterList(animate: true);
		}
	}

	/// <summary>
	/// Đồng bộ dữ liệu mới nhất mỗi khi chuyển sang tab Sự kiện và luôn bắt đầu ở màn hình poster.
	/// </summary>
	public void OnActivatedInShell()
	{
		_isRolling = false;
		SetRollButtonsDisabled(false);
		ShowPosterList(animate: false);
		SyncAllBalancesAndPity(animate: false);
	}

	/// <summary>
	/// Tạm dừng animation rương, ẩn các modal đang mở và đặt lại về danh sách poster khi rời tab Sự kiện.
	/// </summary>
	public void OnDeactivatedInShell()
	{
		_isRolling = false;
		SetRollButtonsDisabled(false);
		_chestRollTween?.Kill();
		StopChestIdleAnimation();
		ResetChestToClosed();
		_toastTween?.Kill();
		_buyTicketTween?.Kill();
		_dropRateTween?.Kill();
		_rulesTween?.Kill();
		_rewardTween?.Kill();

		if (_toastBanner != null) _toastBanner.Visible = false;
		if (_buyTicketModal != null) _buyTicketModal.Visible = false;
		if (_dropRateModal != null) _dropRateModal.Visible = false;
		if (_rulesModal != null) _rulesModal.Visible = false;
		if (_rewardModal != null) _rewardModal.Visible = false;
		_pendingRevealActions.Clear();
		_queuedMilestoneRewards.Clear();
		ShowPosterList(animate: false);
	}

	private void InitializeMilestoneStyles()
	{
		_emptyCardStyle = new StyleBoxEmpty();
		_milestoneLockedStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.24f, 0.16f, 0.44f, 1f),
			BorderColor = new Color(0.62f, 0.52f, 0.86f, 0.85f),
			BorderWidthLeft = 3, BorderWidthTop = 3, BorderWidthRight = 3, BorderWidthBottom = 3,
			CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10, CornerRadiusBottomRight = 10, CornerRadiusBottomLeft = 10
		};
		_milestoneReadyStyle = new StyleBoxFlat
		{
			BgColor = new Color(1f, 0.86f, 0.25f, 1f),
			BorderColor = new Color(1f, 0.98f, 0.75f, 1f),
			BorderWidthLeft = 3, BorderWidthTop = 3, BorderWidthRight = 3, BorderWidthBottom = 3,
			CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10, CornerRadiusBottomRight = 10, CornerRadiusBottomLeft = 10
		};
		_milestoneClaimedStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.22f, 0.82f, 0.46f, 1f),
			BorderColor = new Color(0.78f, 1f, 0.86f, 1f),
			BorderWidthLeft = 3, BorderWidthTop = 3, BorderWidthRight = 3, BorderWidthBottom = 3,
			CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10, CornerRadiusBottomRight = 10, CornerRadiusBottomLeft = 10
		};
	}

	private void BindNodes()
	{
		_currenciesHBox = GetNodeOrNull<Control>("CurrenciesHBox");
		_ticketBar = GetNodeOrNull<CoinBar>("CurrenciesHBox/TicketBar");
		_coinBar = GetNodeOrNull<CoinBar>("CurrenciesHBox/CoinBar");
		_rauBar = GetNodeOrNull<CoinBar>("CurrenciesHBox/RauBar");

		_posterListView = GetNodeOrNull<Control>("ContentArea/PosterListView");
		_gachaDetailView = GetNodeOrNull<Control>("ContentArea/GachaDetailView");
		_posterHitBtn = GetNodeOrNull<Button>("ContentArea/PosterListView/PostersScroll/PostersVBox/DefaultPosterCard/HitBtn");

		_backBtn = GetNodeOrNull<BaseButton>("ContentArea/GachaDetailView/TopBar/BackBtn");
		_ratesBtn = GetNodeOrNull<Button>("ContentArea/GachaDetailView/TopBar/TitleBlock/TitleHBox/RatesBtn");
		_rulesBtn = GetNodeOrNull<Button>("ContentArea/GachaDetailView/TopBar/TitleBlock/TitleHBox/RulesBtn");

		_glowRing = GetNodeOrNull<Control>("ContentArea/GachaDetailView/BodySplit/StageCol/ChestStage/GlowRing");
		_chestImage = GetNodeOrNull<TextureRect>("ContentArea/GachaDetailView/BodySplit/StageCol/ChestStage/ChestImage");

		_milestoneLabel = GetNodeOrNull<Label>("ContentArea/GachaDetailView/BodySplit/StageCol/MilestoneArea/MilestoneHeaderHBox/MilestoneLabel");
		_milestoneBar = GetNodeOrNull<ProgressBar>("ContentArea/GachaDetailView/BodySplit/StageCol/MilestoneArea/MilestoneTrack/MilestoneBar");
		_resetMilestonesBtn = GetNodeOrNull<Button>("ContentArea/GachaDetailView/BodySplit/StageCol/MilestoneArea/MilestoneHeaderHBox/ResetMilestonesBtn");
		_milestoneButtons.Clear();
		foreach (int m in PlayerDataManager.GachaMilestoneTargets)
		{
			var btn = GetNodeOrNull<Button>($"ContentArea/GachaDetailView/BodySplit/StageCol/MilestoneArea/MilestoneTrack/MilestonesHBox/Milestone{m}Btn");
			if (btn != null) _milestoneButtons[m] = btn;
		}

		_roll1Btn = GetNodeOrNull<Button>("ContentArea/GachaDetailView/BodySplit/StageCol/RollButtonsRow/Roll1Btn");
		_roll10Btn = GetNodeOrNull<Button>("ContentArea/GachaDetailView/BodySplit/StageCol/RollButtonsRow/Roll10Btn");

		_dropRateModal = GetNodeOrNull<Control>("DropRateModal");
		_dropRateBackdrop = GetNodeOrNull<Control>("DropRateModal/Backdrop");
		_dropRateDialog = GetNodeOrNull<Control>("DropRateModal/Center/DialogBox");
		_dropRateCloseBtn = GetNodeOrNull<Button>("DropRateModal/Center/DialogBox/DialogVBox/HeaderHBox/CloseBtn");
		_dropRateOkBtn = GetNodeOrNull<Button>("DropRateModal/Center/DialogBox/DialogVBox/OkBtn");
		if (_dropRateModal != null) _dropRateModal.Visible = false;

		_toastBanner = GetNodeOrNull<Control>("ToastBanner");
		_toastLabel = GetNodeOrNull<Label>("ToastBanner/ToastLabel");
		if (_toastBanner != null) _toastBanner.Visible = false;

		_buyTicketModal = GetNodeOrNull<Control>("BuyTicketModal");
		_buyTicketBackdrop = GetNodeOrNull<Control>("BuyTicketModal/Backdrop");
		_buyTicketDialog = GetNodeOrNull<Control>("BuyTicketModal/Center/DialogBox");
		_buyTicketCloseBtn = GetNodeOrNull<Button>("BuyTicketModal/Center/DialogBox/DialogVBox/HeaderHBox/CloseBtn");
		_buyTicketMinusBtn = GetNodeOrNull<Button>("BuyTicketModal/Center/DialogBox/DialogVBox/QtyRow/MinusBtn");
		_buyTicketPlusBtn = GetNodeOrNull<Button>("BuyTicketModal/Center/DialogBox/DialogVBox/QtyRow/PlusBtn");
		_buyTicketPreset10Btn = GetNodeOrNull<Button>("BuyTicketModal/Center/DialogBox/DialogVBox/QtyRow/Preset10Btn");
		_buyTicketPreset50Btn = GetNodeOrNull<Button>("BuyTicketModal/Center/DialogBox/DialogVBox/QtyRow/Preset50Btn");
		_maxByGoldBtn = GetNodeOrNull<Button>("BuyTicketModal/Center/DialogBox/DialogVBox/MaxByCurrencyRow/MaxByGoldBtn");
		_maxByRauBtn = GetNodeOrNull<Button>("BuyTicketModal/Center/DialogBox/DialogVBox/MaxByCurrencyRow/MaxByRauBtn");
		_buyTicketQtyInput = GetNodeOrNull<LineEdit>("BuyTicketModal/Center/DialogBox/DialogVBox/QtyRow/QtyLineEdit");
		_buyTicketBalanceLabel = GetNodeOrNull<Label>("BuyTicketModal/Center/DialogBox/DialogVBox/BalanceHintLabel");
		_buyWithGoldBtn = GetNodeOrNull<Button>("BuyTicketModal/Center/DialogBox/DialogVBox/ActionsHBox/BuyWithGoldBtn");
		_buyWithRauBtn = GetNodeOrNull<Button>("BuyTicketModal/Center/DialogBox/DialogVBox/ActionsHBox/BuyWithRauBtn");
		if (_buyTicketModal != null) _buyTicketModal.Visible = false;

		_rulesModal = GetNodeOrNull<Control>("RulesModal");
		_rulesBackdrop = GetNodeOrNull<Control>("RulesModal/Backdrop");
		_rulesDialog = GetNodeOrNull<Control>("RulesModal/Center/DialogBox");
		_rulesCloseBtn = GetNodeOrNull<Button>("RulesModal/Center/DialogBox/DialogVBox/HeaderHBox/CloseBtn");
		_rulesOkBtn = GetNodeOrNull<Button>("RulesModal/Center/DialogBox/DialogVBox/OkBtn");
		if (_rulesModal != null) _rulesModal.Visible = false;

		_rewardModal = GetNodeOrNull<Control>("RewardResultModal");
		_rewardBackdrop = GetNodeOrNull<Control>("RewardResultModal/Backdrop");
		_rewardDialog = GetNodeOrNull<Control>("RewardResultModal/Center/DialogBox");
		_rewardTitleLabel = GetNodeOrNull<Label>("RewardResultModal/Center/DialogBox/DialogVBox/HeaderHBox/TitleLabel");
		_rewardCloseBtn = GetNodeOrNull<Button>("RewardResultModal/Center/DialogBox/DialogVBox/HeaderHBox/CloseBtn");
		_rewardOkBtn = GetNodeOrNull<Button>("RewardResultModal/Center/DialogBox/DialogVBox/ActionsHBox/OkBtn");
		_modalRoll1Btn = GetNodeOrNull<Button>("RewardResultModal/Center/DialogBox/DialogVBox/ActionsHBox/Roll1Btn");
		_modalRoll10Btn = GetNodeOrNull<Button>("RewardResultModal/Center/DialogBox/DialogVBox/ActionsHBox/Roll10Btn");
		_rewardsGrid = GetNodeOrNull<GridContainer>("RewardResultModal/Center/DialogBox/DialogVBox/RewardsGrid");
		if (_rewardModal != null) _rewardModal.Visible = false;
	}

	private void BindSignals()
	{
		if (_posterHitBtn != null) _posterHitBtn.Pressed += () => ShowGachaDetail(animate: true);
		if (_backBtn != null) _backBtn.Pressed += () => ShowPosterList(animate: true);
		if (_ratesBtn != null) _ratesBtn.Pressed += OpenDropRateModal;
		if (_rulesBtn != null) _rulesBtn.Pressed += OpenRulesModal;
		if (_resetMilestonesBtn != null) _resetMilestonesBtn.Pressed += OnResetMilestonesPressed;

		foreach (var kv in _milestoneButtons)
		{
			int milestoneTurns = kv.Key;
			kv.Value.Pressed += () => OnMilestoneClicked(milestoneTurns);
		}

		if (_dropRateCloseBtn != null) _dropRateCloseBtn.Pressed += CloseDropRateModal;
		if (_dropRateOkBtn != null) _dropRateOkBtn.Pressed += CloseDropRateModal;
		ConfigureModalOutsideClick(_dropRateModal, _dropRateBackdrop, _dropRateDialog, CloseDropRateModal);

		if (_ticketBar != null) _ticketBar.AddCoinPressed += () => OpenBuyTicketModal(1);
		if (_coinBar != null) _coinBar.AddCoinPressed += NavigateToShopExchange;
		if (_rauBar != null) _rauBar.AddCoinPressed += NavigateToShopExchange;

		if (_roll1Btn != null) _roll1Btn.Pressed += () => OnRollPressed(1);
		if (_roll10Btn != null) _roll10Btn.Pressed += () => OnRollPressed(10);

		if (_buyTicketCloseBtn != null) _buyTicketCloseBtn.Pressed += CloseBuyTicketModal;
		if (_buyTicketMinusBtn != null) _buyTicketMinusBtn.Pressed += () => ChangeTicketQty(-1);
		if (_buyTicketPlusBtn != null) _buyTicketPlusBtn.Pressed += () => ChangeTicketQty(1);
		if (_buyTicketPreset10Btn != null) _buyTicketPreset10Btn.Pressed += () => ChangeTicketQty(10);
		if (_buyTicketPreset50Btn != null) _buyTicketPreset50Btn.Pressed += () => ChangeTicketQty(50);
		if (_maxByGoldBtn != null) _maxByGoldBtn.Pressed += () => SetMaxTicketQtyByCurrency(CurrencyType.Gold);
		if (_maxByRauBtn != null) _maxByRauBtn.Pressed += () => SetMaxTicketQtyByCurrency(CurrencyType.Rau);
		if (_buyTicketQtyInput != null)
		{
			_buyTicketQtyInput.TextChanged += OnBuyTicketQtyTextChanged;
			_buyTicketQtyInput.TextSubmitted += (_) => CommitTicketQtyInput();
			_buyTicketQtyInput.FocusExited += CommitTicketQtyInput;
		}
		if (_buyWithGoldBtn != null) _buyWithGoldBtn.Pressed += () => ConfirmBuyTickets(CurrencyType.Gold);
		if (_buyWithRauBtn != null) _buyWithRauBtn.Pressed += () => ConfirmBuyTickets(CurrencyType.Rau);
		ConfigureModalOutsideClick(_buyTicketModal, _buyTicketBackdrop, _buyTicketDialog, CloseBuyTicketModal);

		if (_rulesCloseBtn != null) _rulesCloseBtn.Pressed += CloseRulesModal;
		if (_rulesOkBtn != null) _rulesOkBtn.Pressed += CloseRulesModal;
		ConfigureModalOutsideClick(_rulesModal, _rulesBackdrop, _rulesDialog, CloseRulesModal);

		if (_rewardCloseBtn != null) _rewardCloseBtn.Pressed += CloseRewardModal;
		if (_rewardOkBtn != null) _rewardOkBtn.Pressed += OnRewardOkPressed;
		if (_modalRoll1Btn != null) _modalRoll1Btn.Pressed += () => OnModalRollAgainPressed(1);
		if (_modalRoll10Btn != null) _modalRoll10Btn.Pressed += () => OnModalRollAgainPressed(10);
		ConfigureModalOutsideClick(_rewardModal, _rewardBackdrop, _rewardDialog, CloseRewardModal);
	}

	private void OnSharedCurrenciesChanged(int coins, int rau)
	{
		_coinBar?.SetCoins(coins, animate: true);
		_rauBar?.SetCoins(rau, animate: true);
		RefreshBuyTicketUI();
	}

	private void OnGachaStateChanged(int tickets, int pityCount)
	{
		_ticketBar?.SetCoins(tickets, animate: true);
		UpdateMilestonesUI();
	}

	private void SyncAllBalancesAndPity(bool animate)
	{
		if (_data == null) return;
		_ticketBar?.SetCoins(_data.GachaTickets, animate);
		_coinBar?.SetCoins(_data.Coins, animate);
		_rauBar?.SetCoins(_data.Rau, animate);
		UpdateMilestonesUI();
		RefreshBuyTicketUI();
	}

	private void UpdateMilestonesUI()
	{
		if (_data == null) return;
		int progress = _data.GachaMilestoneProgress;

		if (_milestoneLabel != null)
		{
			_milestoneLabel.Text = BuildMilestoneHintText(progress);
		}
		if (_milestoneBar != null)
		{
			_milestoneBar.MaxValue = 100.0;
			_milestoneBar.Value = ComputeMilestoneBarPercent(progress);
		}

		foreach (var kv in _milestoneButtons)
		{
			int target = kv.Key;
			var btn = kv.Value;
			bool claimed = _data.IsGachaMilestoneClaimed(target);
			bool reached = progress >= target;

			if (btn.FindChild("Dot", true, false) is Panel dotPanel)
			{
				StyleBoxFlat dotStyle = claimed ? _milestoneClaimedStyle : (reached ? _milestoneReadyStyle : _milestoneLockedStyle);
				if (dotStyle != null)
				{
					dotPanel.AddThemeStyleboxOverride("panel", dotStyle);
				}
			}

			if (btn.FindChild("CheckBadge", true, false) is Control checkBadge)
			{
				checkBadge.Visible = claimed;
			}

			if (btn.FindChild("ItemSlot", true, false) is Control itemSlot)
			{
				itemSlot.Modulate = claimed
					? new Color(0.72f, 0.78f, 0.74f, 0.85f)
					: (reached ? new Color(1.12f, 1.08f, 0.96f, 1f) : new Color(0.86f, 0.84f, 0.94f, 0.9f));
			}

			if (btn.FindChild("StepLabel", true, false) is Label stepLabel)
			{
				Color stepColor = claimed
					? new Color(0.58f, 0.98f, 0.72f, 1f)
					: (reached ? new Color(1f, 0.92f, 0.36f, 1f) : new Color(0.95f, 0.92f, 1f, 1f));
				stepLabel.AddThemeColorOverride("font_color", stepColor);
			}
		}
	}

	private static string GetMilestoneRewardName(int target) => target switch
	{
		1 => "150 Vàng",
		5 => "Trà Đá",
		10 => "50 Rau",
		15 => "2 Vé Rương",
		30 => "Lộc 1.2K",
		_ => "Phần thưởng"
	};

	private string BuildMilestoneHintText(int progress)
	{
		foreach (int target in PlayerDataManager.GachaMilestoneTargets)
		{
			if (progress < target)
			{
				int need = target - progress;
				return $"Còn {need} vé nữa sẽ nhận {GetMilestoneRewardName(target)}";
			}
		}

		bool anyUnclaimed = false;
		foreach (int target in PlayerDataManager.GachaMilestoneTargets)
		{
			if (!_data.IsGachaMilestoneClaimed(target))
			{
				anyUnclaimed = true;
				break;
			}
		}

		return anyUnclaimed
			? "Đã đạt hết mốc! Bấm vào vật phẩm để nhận quà"
			: "Đã nhận hết tất cả mốc phần thưởng!";
	}

	private static double ComputeMilestoneBarPercent(int progress)
	{
		if (progress <= 0) return 0.0;
		if (progress >= 30) return 100.0;

		return progress switch
		{
			<= 1 => progress * 10.0,
			<= 5 => 10.0 + ((progress - 1) / 4.0) * 20.0,
			<= 10 => 30.0 + ((progress - 5) / 5.0) * 20.0,
			<= 15 => 50.0 + ((progress - 10) / 5.0) * 20.0,
			_ => 70.0 + ((progress - 15) / 15.0) * 30.0
		};
	}

	private void OnMilestoneClicked(int milestoneTurns)
	{
		if (_data == null || _isRolling) return;

		if (_data.IsGachaMilestoneClaimed(milestoneTurns))
		{
			ShowToast($"Bạn đã nhận quà mốc {milestoneTurns} lượt rồi!");
			return;
		}

		if (_data.GachaMilestoneProgress < milestoneTurns)
		{
			int need = milestoneTurns - _data.GachaMilestoneProgress;
			ShowToast($"Cần quay thêm {need} lượt để mở khóa mốc {milestoneTurns}!");
			return;
		}

		var reward = _data.TryClaimGachaMilestone(milestoneTurns);
		if (reward != null)
		{
			bool didCycleReset = _data.GachaMilestoneProgress == 0;
			UpdateMilestonesUI();
			if (didCycleReset)
			{
				ShowToast("Đã nhận đủ mốc thưởng — tự động bắt đầu vòng mới!");
			}
			OpenRewardModal(new List<GachaRewardResult> { reward }, allowMysteryFlip: false, title: $"Quà Mốc {milestoneTurns} Lượt");
		}
	}

	private void OnResetMilestonesPressed()
	{
		if (_data == null || _isRolling) return;
		_data.ResetGachaMilestones();
		UpdateMilestonesUI();
		ShowToast("Đã đặt lại mốc phần thưởng về ban đầu!");
	}

	private void ShowPosterList(bool animate)
	{
		if (_isRolling) return;
		StopChestIdleAnimation();

		if (_currenciesHBox != null) _currenciesHBox.Visible = false;
		if (_gachaDetailView != null) _gachaDetailView.Visible = false;
		if (_posterListView == null) return;

		_posterListView.Visible = true;
		FadeInView(_posterListView, animate);
	}

	private void ShowGachaDetail(bool animate)
	{
		if (_posterListView != null) _posterListView.Visible = false;
		if (_currenciesHBox != null)
		{
			_currenciesHBox.Visible = true;
			_currenciesHBox.Modulate = animate ? new Color(1f, 1f, 1f, 0f) : Colors.White;
		}
		if (_gachaDetailView == null) return;

		_gachaDetailView.Visible = true;
		FadeInView(_gachaDetailView, animate);
		if (animate && _currenciesHBox != null)
		{
			_viewSwitchTween?.Parallel().TweenProperty(_currenciesHBox, "modulate:a", 1f, 0.16f);
		}
		CallDeferred(MethodName.StartChestIdleAnimation);
	}

	private void FadeInView(Control view, bool animate)
	{
		_viewSwitchTween?.Kill();
		if (!animate)
		{
			view.Modulate = Colors.White;
			return;
		}

		view.Modulate = new Color(1f, 1f, 1f, 0f);
		_viewSwitchTween = CreateTween().SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
		_viewSwitchTween.TweenProperty(view, "modulate:a", 1f, 0.16f);
	}

	private void StartChestIdleAnimation()
	{
		if (_chestImage == null || !IsInsideTree() || (_gachaDetailView != null && !_gachaDetailView.Visible))
			return;

		_chestIdleTween?.Kill();
		_chestImage.PivotOffset = _chestImage.Size / 2f;
		_chestImage.Rotation = 0f;
		_chestImage.Scale = Vector2.One;

		if (_glowRing != null)
		{
			_glowRing.PivotOffset = _glowRing.Size / 2f;
			_chestIdleTween = CreateTween().SetLoops();
			_chestIdleTween.TweenProperty(_glowRing, "scale", new Vector2(1.10f, 1.10f), 1.25f)
				.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
			_chestIdleTween.Parallel().TweenProperty(_glowRing, "modulate:a", 1.0f, 1.25f)
				.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);

			_chestIdleTween.TweenProperty(_glowRing, "scale", Vector2.One, 1.25f)
				.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
			_chestIdleTween.Parallel().TweenProperty(_glowRing, "modulate:a", 0.72f, 1.25f)
				.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		}
	}

	private void StopChestIdleAnimation()
	{
		_chestIdleTween?.Kill();
		if (_chestImage != null)
		{
			_chestImage.Rotation = 0f;
			_chestImage.Scale = Vector2.One;
		}
		if (_glowRing != null)
		{
			_glowRing.Scale = Vector2.One;
			_glowRing.Modulate = Colors.White;
		}
	}

	private void LoadChestAnimationFrames()
	{
		for (int i = 0; i < _chestFrames.Length; i++)
		{
			_chestFrames[i] = GD.Load<Texture2D>($"res://assets/sprites/icons/animation_chest/chest_full_{i:D2}.png");
		}

		_greenCardTex = GD.Load<Texture2D>("res://assets/sprites/icons/cards/static/card_01_cui_green.png");
		_blueCardTex = GD.Load<Texture2D>("res://assets/sprites/icons/cards/static/card_02_so_cap_blue.png");

		for (int i = 0; i < 12; i++)
		{
			_purpleCardFrames[i] = GD.Load<Texture2D>($"res://assets/sprites/icons/cards/animated/card_03_trung_cap_purple/card_03_trung_cap_purple_frame_{i:D2}.png");
			_goldCardFrames[i] = GD.Load<Texture2D>($"res://assets/sprites/icons/cards/animated/card_04_cao_cap_gold/card_04_cao_cap_gold_frame_{i:D2}.png");
			_redCardFrames[i] = GD.Load<Texture2D>($"res://assets/sprites/icons/cards/animated/card_05_sieu_cao_cap_red/card_05_sieu_cao_cap_red_frame_{i:D2}.png");
		}
	}

	private void SetChestFrame(int frameIndex)
	{
		if (_chestImage == null || frameIndex < 0 || frameIndex >= _chestFrames.Length) return;
		var tex = _chestFrames[frameIndex];
		if (tex != null)
		{
			_chestImage.Texture = tex;
		}
	}

	private void OnChestFrameStep(int frameIdx, GachaRarity highestRarity = GachaRarity.Common)
	{
		SetChestFrame(frameIdx);
		if (frameIdx == 15 && _glowRing != null)
		{
			Color burstGlow = highestRarity switch
			{
				GachaRarity.Legendary => new Color(1.50f, 0.56f, 0.44f, 1f),
				GachaRarity.Epic => new Color(1.36f, 1.18f, 0.42f, 1f),
				GachaRarity.Rare => new Color(1.20f, 0.72f, 1.40f, 1f),
				_ => new Color(1.28f, 1.16f, 0.92f, 1f)
			};

			var burst = CreateTween().SetParallel(true);
			_glowRing.PivotOffset = _glowRing.Size / 2f;
			burst.TweenProperty(_glowRing, "scale", new Vector2(1.52f, 1.52f), 0.34f)
				.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
			burst.TweenProperty(_glowRing, "modulate", burstGlow, 0.28f);
		}
	}

	private void ResetChestToClosed()
	{
		SetChestFrame(0);
	}

	private async void OnRollPressed(int count)
	{
		if (_isRolling || _data == null) return;

		if (_data.GachaTickets < count)
		{
			int missing = count - _data.GachaTickets;
			ShowToast($"Cần thêm {missing} Vé để Quay {count} — hãy đổi Vé tại đây!");
			OpenBuyTicketModal(missing);
			return;
		}

		var rewards = _data.TryExecuteGachaRoll(count);
		if (rewards == null || rewards.Count == 0) return;

		GachaRarity highestRarity = GachaRarity.Common;
		foreach (var r in rewards)
		{
			if (r.Rarity > highestRarity) highestRarity = r.Rarity;
		}

		int shakeRepeats = highestRarity switch
		{
			GachaRarity.Legendary => 3, // Đỏ: lắc lâu hơn nữa
			GachaRarity.Epic => 2,      // Vàng: lắc lâu hơn xíu
			GachaRarity.Rare => 1,      // Tím: lặp lại nhịp lắc
			_ => 0                      // Xanh lá / Xanh dương: bình thường
		};

		_isRolling = true;
		SetRollButtonsDisabled(true);
		StopChestIdleAnimation();
		ResetChestToClosed();

		// Hiệu ứng mở rương Chợ Lớn 32 frame chậm rãi; lặp lại nhịp lắc 02->12 nếu trúng đồ hiếm
		if (_chestImage != null)
		{
			_chestImage.PivotOffset = _chestImage.Size / 2f;
			_chestRollTween?.Kill();
			_chestRollTween = CreateTween();

			const float shakeStep = 0.068f;
			const float openStep = 0.074f;

			// Lắc ban đầu 01 -> 12
			for (int i = 1; i <= 12; i++)
			{
				int frameIdx = i;
				_chestRollTween.TweenCallback(Callable.From(() => OnChestFrameStep(frameIdx, highestRarity)));
				_chestRollTween.TweenInterval(shakeStep);
			}

			// Lặp lại nhịp lắc rương (02 -> 12) tùy theo bậc quà hiếm
			if (shakeRepeats > 0 && _glowRing != null)
			{
				Color hintColor = highestRarity switch
				{
					GachaRarity.Legendary => new Color(1.45f, 0.52f, 0.42f, 1f),
					GachaRarity.Epic => new Color(1.32f, 1.15f, 0.48f, 1f),
					_ => new Color(1.15f, 0.72f, 1.35f, 1f)
				};
				_chestRollTween.TweenCallback(Callable.From(() =>
				{
					if (!IsInstanceValid(_glowRing)) return;
					var glowHint = CreateTween();
					glowHint.TweenProperty(_glowRing, "modulate", hintColor, 0.30f);
				}));

				for (int rep = 0; rep < shakeRepeats; rep++)
				{
					for (int i = 2; i <= 12; i++)
					{
						int frameIdx = i;
						_chestRollTween.TweenCallback(Callable.From(() => OnChestFrameStep(frameIdx, highestRarity)));
						_chestRollTween.TweenInterval(shakeStep);
					}
				}
			}

			// Bật nắp & bùng sáng 13 -> 31
			for (int i = 13; i < _chestFrames.Length; i++)
			{
				int frameIdx = i;
				_chestRollTween.TweenCallback(Callable.From(() => OnChestFrameStep(frameIdx, highestRarity)));
				_chestRollTween.TweenInterval(openStep);
			}

			_chestRollTween.TweenInterval(0.42f);
			await ToSignal(_chestRollTween, Tween.SignalName.Finished);
		}

		if (!IsInstanceValid(this) || !IsInsideTree()) return;

		_isRolling = false;
		SetRollButtonsDisabled(false);

		_queuedMilestoneRewards.Clear();
		if (_data.LastAutoClaimedMilestones.Count > 0)
		{
			_queuedMilestoneRewards.AddRange(_data.LastAutoClaimedMilestones);
		}
		OpenRewardModal(rewards, allowMysteryFlip: true, title: "Phần Quà Mở Rương");
	}

	private void SetRollButtonsDisabled(bool disabled)
	{
		if (_roll1Btn != null) _roll1Btn.Disabled = disabled;
		if (_roll10Btn != null) _roll10Btn.Disabled = disabled;
		if (_modalRoll1Btn != null) _modalRoll1Btn.Disabled = disabled;
		if (_modalRoll10Btn != null) _modalRoll10Btn.Disabled = disabled;
	}

	private void OpenBuyTicketModal(int initialQty)
	{
		if (_buyTicketModal == null) return;
		_buyTicketQty = Mathf.Max(1, initialQty);
		RefreshBuyTicketUI(syncInputText: true);
		AnimateModalOpen(_buyTicketModal, _buyTicketDialog, ref _buyTicketTween);
	}

	private void CloseBuyTicketModal()
	{
		AnimateModalClose(_buyTicketModal, _buyTicketDialog, ref _buyTicketTween);
	}

	private void ChangeTicketQty(int delta)
	{
		long next = (long)_buyTicketQty + delta;
		if (next < 1) next = 1;
		if (next > int.MaxValue) next = int.MaxValue;
		SetTicketQty((int)next);
	}

	private void SetTicketQty(int qty)
	{
		_buyTicketQty = Mathf.Max(1, qty);
		RefreshBuyTicketUI(syncInputText: true);
	}

	private void SetMaxTicketQtyByCurrency(CurrencyType currency)
	{
		if (_data == null)
		{
			SetTicketQty(1);
			return;
		}

		int maxAffordable = currency == CurrencyType.Rau
			? _data.Rau / PlayerDataManager.TicketPriceRau
			: _data.Coins / PlayerDataManager.TicketPriceGold;

		if (maxAffordable <= 0)
		{
			string unit = currency == CurrencyType.Rau ? "Rau" : "Vàng";
			ShowToast($"Chưa đủ {unit} để đổi thêm 1 Vé!");
			return;
		}

		SetTicketQty(maxAffordable);
	}

	private void OnBuyTicketQtyTextChanged(string newText)
	{
		if (_buyTicketQtyInput == null) return;

		string digits = "";
		foreach (char c in newText)
		{
			if (char.IsDigit(c)) digits += c;
		}

		if (digits != newText)
		{
			_buyTicketQtyInput.Text = digits;
			_buyTicketQtyInput.CaretColumn = digits.Length;
		}

		if (string.IsNullOrEmpty(digits)) return;

		if (long.TryParse(digits, out long parsed))
		{
			long clampedLong = System.Math.Clamp(parsed, 1L, int.MaxValue);
			_buyTicketQty = (int)clampedLong;
			if (clampedLong != parsed)
			{
				_buyTicketQtyInput.Text = _buyTicketQty.ToString();
				_buyTicketQtyInput.CaretColumn = _buyTicketQtyInput.Text.Length;
			}
			RefreshBuyTicketUI(syncInputText: false);
		}
		else
		{
			_buyTicketQty = int.MaxValue;
			_buyTicketQtyInput.Text = _buyTicketQty.ToString();
			_buyTicketQtyInput.CaretColumn = _buyTicketQtyInput.Text.Length;
			RefreshBuyTicketUI(syncInputText: false);
		}
	}

	private void CommitTicketQtyInput()
	{
		if (_buyTicketQtyInput == null) return;
		if (!long.TryParse(_buyTicketQtyInput.Text, out long parsed) || parsed < 1)
		{
			SetTicketQty(_buyTicketQty);
			return;
		}
		SetTicketQty((int)System.Math.Min(parsed, int.MaxValue));
	}

	private void RefreshBuyTicketUI(bool syncInputText = true)
	{
		long goldCostLong = (long)_buyTicketQty * PlayerDataManager.TicketPriceGold;
		long rauCostLong = (long)_buyTicketQty * PlayerDataManager.TicketPriceRau;
		int maxByGold = _data != null ? _data.Coins / PlayerDataManager.TicketPriceGold : 0;
		int maxByRau = _data != null ? _data.Rau / PlayerDataManager.TicketPriceRau : 0;

		if (syncInputText && _buyTicketQtyInput != null)
		{
			_buyTicketQtyInput.Text = _buyTicketQty.ToString();
		}
		if (_buyTicketMinusBtn != null) _buyTicketMinusBtn.Disabled = _buyTicketQty <= 1;
		if (_buyTicketPlusBtn != null) _buyTicketPlusBtn.Disabled = false;

		if (_maxByGoldBtn != null)
		{
			_maxByGoldBtn.Text = $"Tối đa Vàng ({maxByGold:N0})";
			_maxByGoldBtn.Disabled = maxByGold <= 0;
		}
		if (_maxByRauBtn != null)
		{
			_maxByRauBtn.Text = $"Tối đa Rau ({maxByRau:N0})";
			_maxByRauBtn.Disabled = maxByRau <= 0;
		}

		if (_buyWithGoldBtn != null)
		{
			_buyWithGoldBtn.Text = $"{goldCostLong:N0} Vàng";
			_buyWithGoldBtn.Disabled = _data == null || goldCostLong > _data.Coins;
		}
		if (_buyWithRauBtn != null)
		{
			_buyWithRauBtn.Text = $"{rauCostLong:N0} Rau";
			_buyWithRauBtn.Disabled = _data == null || rauCostLong > _data.Rau;
		}
		if (_buyTicketBalanceLabel != null && _data != null)
		{
			_buyTicketBalanceLabel.Text = $"Hiện có: {_data.Coins:N0} Vàng · {_data.Rau:N0} Rau · {_data.GachaTickets:N0} Vé";
		}
	}

	private void ConfirmBuyTickets(CurrencyType currency)
	{
		if (_data == null) return;
		if (_data.TryBuyGachaTickets(_buyTicketQty, currency))
		{
			CloseBuyTicketModal();
			ShowToast($"Đã đổi thành công +{_buyTicketQty} Vé Rương May Mắn!");
		}
		else
		{
			string unit = currency == CurrencyType.Rau ? "Rau" : "Vàng";
			ShowToast($"Không đủ {unit} để đổi {_buyTicketQty} Vé!");
		}
	}

	private void OpenDropRateModal()
	{
		if (_dropRateModal == null) return;
		AnimateModalOpen(_dropRateModal, _dropRateDialog, ref _dropRateTween);
	}

	private void CloseDropRateModal()
	{
		AnimateModalClose(_dropRateModal, _dropRateDialog, ref _dropRateTween);
	}

	private void OpenRulesModal()
	{
		if (_rulesModal == null) return;
		AnimateModalOpen(_rulesModal, _rulesDialog, ref _rulesTween);
	}

	private void CloseRulesModal()
	{
		AnimateModalClose(_rulesModal, _rulesDialog, ref _rulesTween);
	}

	private void OpenRewardModal(List<GachaRewardResult> rewards, bool allowMysteryFlip = true, string title = "Phần Quà Mở Rương")
	{
		if (_rewardModal == null || _rewardsGrid == null || _rewardItemScene == null) return;

		if (_rewardTitleLabel != null)
		{
			_rewardTitleLabel.Text = title;
		}
		_pendingRevealActions.Clear();
		foreach (Node child in _rewardsGrid.GetChildren())
		{
			child.QueueFree();
		}

		if (_modalRoll1Btn != null) _modalRoll1Btn.Visible = allowMysteryFlip;
		if (_modalRoll10Btn != null) _modalRoll10Btn.Visible = allowMysteryFlip;

		_rewardsGrid.Columns = rewards.Count == 1 ? 1 : (rewards.Count <= 5 ? rewards.Count : 5);
		if (_rewardDialog != null)
		{
			float singleWidth = allowMysteryFlip ? 680f : 440f;
			_rewardDialog.CustomMinimumSize = rewards.Count == 1 ? new Vector2(singleWidth, 420) : new Vector2(880, 620);
		}

		for (int i = 0; i < rewards.Count; i++)
		{
			var reward = rewards[i];
			var item = _rewardItemScene.Instantiate<PanelContainer>();
			bool isHighTier = reward.Rarity >= GachaRarity.Rare;

			var frontWrap = item.FindChild("FrontWrap", true, false) as Control;
			var frontCard = item.FindChild("FrontCard", true, false) as PanelContainer;
			var cardBack = item.FindChild("CardBack", true, false) as Control;
			var cardArtRect = item.FindChild("CardArtRect", true, false) as TextureRect;
			var flipHitBtn = item.FindChild("FlipHitBtn", true, false) as Button;

			if (item.FindChild("IconRect", true, false) is TextureRect iconRect)
			{
				iconRect.Texture = LoadIcon(reward.IconPath);
				iconRect.Modulate = reward.IconTint;
			}
			if (item.FindChild("AmountLabel", true, false) is Label amountLabel)
			{
				amountLabel.Text = reward.AmountText;
				amountLabel.AddThemeColorOverride("font_color", GetRarityAccentColor(reward.Rarity));
			}
			if (item.FindChild("NameLabel", true, false) is Label nameLabel)
			{
				nameLabel.Text = reward.Name;
			}
			if (item.FindChild("NoteLabel", true, false) is Label noteLabel)
			{
				noteLabel.Text = reward.Note;
			}

			if (frontCard != null)
			{
				ApplyRarityStyle(frontCard, reward.Rarity, isFaceDown: false);
			}

			if (allowMysteryFlip)
			{
				if (frontWrap != null) frontWrap.Visible = false;
				if (cardBack != null) cardBack.Visible = true;
				if (cardArtRect != null)
				{
					cardArtRect.Texture = GetStaticCardBackTexture(reward.Rarity);
				}
			}
			else
			{
				if (frontWrap != null) frontWrap.Visible = true;
				if (cardBack != null) cardBack.Visible = false;
			}

			_rewardsGrid.AddChild(item);

			// Hiệu ứng xuất hiện từ từ từng thẻ quà để tạo nhịp hồi hộp
			item.Modulate = new Color(1f, 1f, 1f, 0f);
			item.Scale = new Vector2(0.76f, 0.76f);
			item.PivotOffset = new Vector2(75f, 103f);
			var pop = item.CreateTween().SetParallel(true);
			float delay = 0.18f + i * 0.20f;
			pop.TweenProperty(item, "modulate:a", 1f, 0.36f).SetDelay(delay);
			pop.TweenProperty(item, "scale", Vector2.One, 0.44f).SetDelay(delay)
				.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);

			if (allowMysteryFlip)
			{
				Texture2D[] animFrames = GetAnimatedCardBackFrames(reward.Rarity);
				Tween cardAnimTween = null;
				if (isHighTier && animFrames != null && cardArtRect != null)
				{
					int frameIdx = 0;
					cardAnimTween = item.CreateTween().SetLoops();
					cardAnimTween.TweenCallback(Callable.From(() =>
					{
						if (!IsInstanceValid(cardArtRect) || cardBack == null || !cardBack.Visible) return;
						frameIdx = (frameIdx + 1) % animFrames.Length;
						if (animFrames[frameIdx] != null) cardArtRect.Texture = animFrames[frameIdx];
					}));
					cardAnimTween.TweenInterval(0.135f);
				}

				bool flipped = false;
				Action flipAction = null;
				flipAction = () =>
				{
					if (flipped || !IsInstanceValid(item)) return;
					flipped = true;
					if (isHighTier)
					{
						_pendingRevealActions.Remove(flipAction);
						UpdateRewardOkButtonText();
					}
					cardAnimTween?.Kill();
					FlipMysteryRewardCard(item, frontWrap, cardBack, slowDramatic: isHighTier);
				};

				if (isHighTier)
				{
					_pendingRevealActions.Add(flipAction);
				}
				else
				{
					// Thẻ Xanh lá / Xanh dương hiện mặt lưng rồi tự lật mở sau một nhịp chậm rãi
					pop.Chain().TweenInterval(0.38f);
					pop.Chain().TweenCallback(Callable.From(flipAction));
				}

				if (flipHitBtn != null)
				{
					flipHitBtn.Pressed += flipAction;
				}
			}
		}

		UpdateRewardOkButtonText();
		AnimateModalOpen(_rewardModal, _rewardDialog, ref _rewardTween);
	}

	private void FlipMysteryRewardCard(PanelContainer card, Control frontWrap, Control cardBack, bool slowDramatic = true)
	{
		card.PivotOffset = card.Size / 2f;
		float baseY = card.Position.Y;
		float outDur = slowDramatic ? 0.36f : 0.24f;
		float inDur = slowDramatic ? 0.44f : 0.28f;
		float liftOffset = slowDramatic ? 24f : 12f;

		var flipOut = card.CreateTween().SetParallel(true);
		flipOut.TweenProperty(card, "scale", new Vector2(0f, 1f), outDur)
			.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
		flipOut.TweenProperty(card, "position:y", baseY - liftOffset, outDur)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);

		flipOut.Chain().TweenCallback(Callable.From(() =>
		{
			if (!IsInstanceValid(card)) return;
			if (cardBack != null) cardBack.Visible = false;
			if (frontWrap != null) frontWrap.Visible = true;
			card.Modulate = slowDramatic ? new Color(1.40f, 1.34f, 1.12f, 1f) : new Color(1.16f, 1.16f, 1.12f, 1f);

			var flipIn = card.CreateTween().SetParallel(true);
			flipIn.TweenProperty(card, "scale", Vector2.One, inDur)
				.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
			flipIn.TweenProperty(card, "position:y", baseY, inDur)
				.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.InOut);
			flipIn.TweenProperty(card, "modulate", Colors.White, slowDramatic ? 0.55f : 0.32f)
				.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
		}));
	}

	private async void OnRewardOkPressed()
	{
		if (_pendingRevealActions.Count > 0)
		{
			var snapshot = new List<Action>(_pendingRevealActions);
			for (int i = 0; i < snapshot.Count; i++)
			{
				snapshot[i]?.Invoke();
				if (i < snapshot.Count - 1 && IsInsideTree())
				{
					await ToSignal(GetTree().CreateTimer(0.24f), SceneTreeTimer.SignalName.Timeout);
					if (!IsInstanceValid(this) || !IsInsideTree()) return;
				}
			}
			return;
		}

		CloseRewardModal();
	}

	private void OnModalRollAgainPressed(int count)
	{
		if (_isRolling || _data == null) return;

		_pendingRevealActions.Clear();
		if (_queuedMilestoneRewards.Count > 0)
		{
			_queuedMilestoneRewards.Clear();
			ShowToast("Đã tự động cộng quà mốc 30 lượt vào túi đồ!");
		}

		_rewardTween?.Kill();
		if (_rewardModal != null) _rewardModal.Visible = false;

		if (_data.GachaTickets < count)
		{
			ResetChestToClosed();
			StartChestIdleAnimation();
			int missing = count - _data.GachaTickets;
			ShowToast($"Cần thêm {missing} Vé để Quay {count} — hãy đổi Vé tại đây!");
			OpenBuyTicketModal(missing);
			return;
		}

		OnRollPressed(count);
	}

	private void UpdateRewardOkButtonText()
	{
		if (_rewardOkBtn == null) return;
		_rewardOkBtn.Text = _pendingRevealActions.Count > 0
			? "Lật tất cả"
			: "Nhận Quà";
	}

	private void CloseRewardModal()
	{
		_pendingRevealActions.Clear();
		if (_queuedMilestoneRewards.Count > 0)
		{
			var queued = new List<GachaRewardResult>(_queuedMilestoneRewards);
			_queuedMilestoneRewards.Clear();
			ShowToast("Đã đạt đầy mốc 30 lượt — tự nhận quà & bắt đầu vòng mới!");
			OpenRewardModal(queued, allowMysteryFlip: false, title: "Quà Hoàn Tất Mốc Thưởng");
			return;
		}

		AnimateModalClose(_rewardModal, _rewardDialog, ref _rewardTween);
		ResetChestToClosed();
		StartChestIdleAnimation();
	}

	private Texture2D GetStaticCardBackTexture(GachaRarity rarity) => rarity switch
	{
		GachaRarity.Legendary => _redCardFrames[0],
		GachaRarity.Epic => _goldCardFrames[0],
		GachaRarity.Rare => _purpleCardFrames[0],
		GachaRarity.Uncommon => _blueCardTex,
		_ => _greenCardTex
	};

	private Texture2D[] GetAnimatedCardBackFrames(GachaRarity rarity) => rarity switch
	{
		GachaRarity.Legendary => _redCardFrames,
		GachaRarity.Epic => _goldCardFrames,
		GachaRarity.Rare => _purpleCardFrames,
		_ => null
	};

	private void ApplyRarityStyle(PanelContainer card, GachaRarity rarity, bool isFaceDown = false)
	{
		if (isFaceDown)
		{
			card.AddThemeStyleboxOverride("panel", _emptyCardStyle);
			return;
		}

		card.RemoveThemeStyleboxOverride("panel");
		if (card.GetThemeStylebox("panel") is not StyleBoxFlat baseStyle) return;
		var copy = (StyleBoxFlat)baseStyle.Duplicate();
		copy.BorderColor = GetRarityAccentColor(rarity);
		int width = rarity >= GachaRarity.Epic ? 3 : 2;
		copy.BorderWidthLeft = width;
		copy.BorderWidthTop = width;
		copy.BorderWidthRight = width;
		copy.BorderWidthBottom = width;
		card.AddThemeStyleboxOverride("panel", copy);
	}

	private static Color GetRarityAccentColor(GachaRarity rarity) => rarity switch
	{
		GachaRarity.Legendary => new Color(1f, 0.34f, 0.28f, 1f),  // Đỏ siêu cao cấp
		GachaRarity.Epic => new Color(1f, 0.84f, 0.22f, 1f),       // Vàng cao cấp
		GachaRarity.Rare => new Color(0.82f, 0.46f, 1f, 1f),       // Tím trung cấp
		GachaRarity.Uncommon => new Color(0.32f, 0.72f, 1f, 1f),   // Xanh dương sơ cấp
		_ => new Color(0.38f, 0.88f, 0.48f, 1f)                    // Xanh lá thường
	};

	private void NavigateToShopExchange()
	{
		if (_data != null)
		{
			_data.PendingShopTabIndex = 3; // Mở thẳng tab Đổi Vàng/Rau
		}
		if (_shellSidebar != null)
		{
			_shellSidebar.SelectTab(3);
			return;
		}

		var sidebar = GetNodeOrNull<LeftSidebar>("LeftSidebar");
		var tabShop = sidebar?.GetNodeOrNull<SidebarTab>("TabsVBox/Tab3_CuaHang");
		if (tabShop != null)
		{
			tabShop.EmitSignal(SidebarTab.SignalName.TabSelected, 3);
		}
		else
		{
			GetTree().ChangeSceneToFile("res://scenes/ui/shop_screen.tscn");
		}
	}

	private Texture2D LoadIcon(string path)
	{
		if (string.IsNullOrEmpty(path)) return null;
		if (_iconCache.TryGetValue(path, out var cached)) return cached;
		var loaded = GD.Load<Texture2D>(path);
		if (loaded != null) _iconCache[path] = loaded;
		return loaded;
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

	private void AnimateModalOpen(Control modalRoot, Control dialogBox, ref Tween activeTween)
	{
		if (modalRoot == null) return;
		activeTween?.Kill();
		modalRoot.Visible = true;
		modalRoot.Modulate = new Color(1f, 1f, 1f, 0f);

		var tween = modalRoot.CreateTween().SetParallel(true);
		tween.TweenProperty(modalRoot, "modulate:a", 1f, 0.16f)
			.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);

		if (dialogBox != null)
		{
			if (!_modalBaseScales.TryGetValue(dialogBox, out var baseScale))
			{
				baseScale = dialogBox.Scale == Vector2.Zero ? Vector2.One : dialogBox.Scale;
				_modalBaseScales[dialogBox] = baseScale;
			}
			dialogBox.PivotOffset = dialogBox.Size / 2f;
			dialogBox.Scale = baseScale * 0.88f;
			tween.TweenProperty(dialogBox, "scale", baseScale, 0.22f)
				.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		}
		activeTween = tween;
	}

	private void AnimateModalClose(Control modalRoot, Control dialogBox, ref Tween activeTween)
	{
		if (modalRoot == null || !modalRoot.Visible) return;
		activeTween?.Kill();

		var tween = modalRoot.CreateTween().SetParallel(true);
		tween.TweenProperty(modalRoot, "modulate:a", 0f, 0.14f)
			.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);

		if (dialogBox != null)
		{
			if (!_modalBaseScales.TryGetValue(dialogBox, out var baseScale))
			{
				baseScale = Vector2.One;
				_modalBaseScales[dialogBox] = baseScale;
			}
			dialogBox.PivotOffset = dialogBox.Size / 2f;
			tween.TweenProperty(dialogBox, "scale", baseScale * 0.90f, 0.14f)
				.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
		}

		tween.Chain().TweenCallback(Callable.From(() => modalRoot.Visible = false));
		activeTween = tween;
	}

	private void ShowToast(string message)
	{
		if (_toastBanner == null || _toastLabel == null) return;
		_toastLabel.Text = message;
		_toastBanner.Visible = true;
		_toastBanner.Modulate = new Color(1f, 1f, 1f, 0f);

		_toastTween?.Kill();
		_toastTween = _toastBanner.CreateTween();
		_toastTween.TweenProperty(_toastBanner, "modulate:a", 1f, 0.16f);
		_toastTween.TweenInterval(2.2f);
		_toastTween.TweenProperty(_toastBanner, "modulate:a", 0f, 0.24f);
		_toastTween.TweenCallback(Callable.From(() => _toastBanner.Visible = false));
	}
}
