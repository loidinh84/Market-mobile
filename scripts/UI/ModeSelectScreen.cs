using System;
using System.Collections.Generic;
using Godot;
using MarketMobile.Core;
using MarketMobile.Utils;

namespace MarketMobile.UI;

/// <summary>
/// Màn hình Chọn Loại Đấu / Chế Độ (1v1, 2v2) / Bản Đồ và Tùy chỉnh Đấu Máy / Custom.
/// </summary>
public partial class ModeSelectScreen : Control
{
	[Signal]
	public delegate void SelectionConfirmedEventHandler();

	[Signal]
	public delegate void ClosedEventHandler();

	private const float ActiveTabWidth = 380f;
	private const float InactiveTabWidth = 345f;
	private const double InviteCooldownSeconds = 5.0;

	private static readonly Font OrcishFont = GD.Load<Font>("res://assets/fonts/FZ-Orcish.ttf");
	private static readonly Texture2D TexTabNormal = GD.Load<Texture2D>("res://assets/sprites/ui/buttons/tab_normal.png");
	private static readonly Texture2D TexTabRank = GD.Load<Texture2D>("res://assets/sprites/ui/buttons/tab_rank.png");
	private static readonly Texture2D TexTabBot = GD.Load<Texture2D>("res://assets/sprites/ui/buttons/tab_bot.png");
	private static readonly Texture2D TexTabCustom = GD.Load<Texture2D>("res://assets/sprites/ui/buttons/tab_custom.png");
	private static readonly Texture2D TexTabLocked = GD.Load<Texture2D>("res://assets/sprites/ui/buttons/tab_locked.png");
	private static readonly Texture2D TexModeInactive = GD.Load<Texture2D>("res://assets/sprites/icons/1vs1.png");
	private static readonly Texture2D TexModeActive = GD.Load<Texture2D>("res://assets/sprites/icons/2vs2.png");
	private static readonly Texture2D TexLockedIcon = GD.Load<Texture2D>("res://assets/sprites/icons/locked.png");
	private static readonly Texture2D TexFriendAvatar = GD.Load<Texture2D>("res://assets/sprites/icons/avatar_character.png");

	private PlayerDataManager _data;

	private Control _rankAtmosphere;
	private Control _rankPulseAura;
	private Button _backBtn;
	private Button _rulesBtn;

	// Left PlayType ribbon tabs
	private TextureButton _tabNormal;
	private TextureButton _tabRanked;
	private TextureRect _rankLockIcon;
	private Label _rankReqLabel;
	private TextureButton _tabBot;
	private TextureButton _tabCustom;

	// Right Section: Team size
	private Control _teamSection;
	private TextureButton _btn1vs1;
	private TextureButton _btn2vs2;
	private Control _marker1vs1;
	private Control _marker2vs2;

	// Right Section: Maps
	private Control _mapSection;
	private ScrollContainer _mapsScroll;
	private GridContainer _mapsGrid;

	// Dynamic context options
	private Control _botOptionsRow;
	private TextureButton[] _botDiffBtns;
	private Control _customOptionsPanel;

	// Custom Row 1: Ghế người xem (tối đa 4) + Mời bạn + Toggles
	private Button[] _specBtns;
	private Button _inviteFriendsBtn;
	private Button _consumableToggleBtn;
	private Button _privacyToggleBtn;

	// Custom Row 2: Thời gian
	private Button[] _timeBtns;
	private LineEdit _timeInput;

	// Custom Row 3: Củ cải Đỏ
	private Button[] _redRadishBtns;
	private LineEdit _redInput;

	// Custom Row 4: Củ cải Trắng
	private Button[] _whiteRadishBtns;
	private LineEdit _whiteInput;

	// Footer
	private Button _confirmBtn;

	// Rules Modal (góc trên phải, có 4 tab bên trong)
	private Control _rulesModal;
	private Control _rulesBackdrop;
	private Control _rulesDialogBox;
	private Button _rulesCloseBtn;
	private Button _rulesOkBtn;
	private Button[] _rulesTabBtns;
	private Label _rulesBodyTitle;
	private Label _rulesBodyLabel;

	// Invite Friends Modal (trong Custom)
	private Control _inviteModal;
	private Control _inviteBackdrop;
	private Control _inviteDialogBox;
	private Button _inviteCloseBtn;
	private Button _inviteDoneBtn;
	private Label _inviteRoomSummaryLabel;
	private ScrollContainer _inviteScroll;
	private VBoxContainer _inviteListVBox;

	// Right-edge Toast Notification
	private PanelContainer _toastBanner;
	private Label _toastLabel;

	private PlayMatchType _draftPlayType = PlayMatchType.Normal;
	private TeamMode _draftTeamMode = TeamMode.Solo1v1;
	private string _draftMapId = "map_cho_dau_moi";
	private BotDifficulty _draftBotDiff = BotDifficulty.Easy;
	private int _draftSpectators = 2;
	private int _draftMinutes = 5;
	private int _draftRedRadish = 0;   // -1 = Không giới hạn
	private int _draftWhiteRadish = 0; // -1 = Không giới hạn
	private bool _draftAllowConsumables = true;
	private bool _draftIsPrivate = false;

	private bool _editingTimeCustom = false;
	private bool _editingRedCustom = false;
	private bool _editingWhiteCustom = false;
	private bool _suppressInputSignal = false;

	private sealed class InviteRowUi
	{
		public FriendMeta Friend { get; init; }
		public Button ActionBtn { get; init; }
		public Label SubLbl { get; init; }
		public int LastSecShown { get; set; } = -1;
	}

	private readonly Dictionary<string, Texture2D> _mapTexCache = new();
	private readonly HashSet<string> _invitedFriendIds = new();
	private readonly Dictionary<string, double> _inviteCooldownEndSec = new();
	private readonly Dictionary<string, InviteRowUi> _inviteRowMap = new();
	private Tween _rankAuraTween;
	private Tween _rankPulseLoopTween;
	private Tween _modalTween;
	private Tween _rulesTween;
	private Tween _inviteTween;
	private Tween _toastTween;
	private bool _isOverlayMode = false;

	private StyleBoxFlat _pillActive;
	private StyleBoxFlat _pillInactive;
	private StyleBoxFlat _pillToggleOn;
	private StyleBoxFlat _pillToggleOff;
	private StyleBoxFlat _inviteBtnStyle;
	private StyleBoxFlat _inviteWaitingStyle;
	private StyleBoxFlat _inviteResendStyle;
	private StyleBoxFlat _avatarFrameStyle;
	private StyleBoxFlat _friendCardStyle;
	private StyleBoxFlat _cardBorderNormal;
	private StyleBoxFlat _cardBorderSelected;
	private StyleBoxFlat _confirmStyleGreen;
	private StyleBoxFlat _rulesTabActive;
	private StyleBoxFlat _rulesTabInactive;
	private StyleBoxFlat _inputActiveStyle;
	private StyleBoxFlat _inputInactiveStyle;

	public override void _Ready()
	{
		_data = PlayerDataManager.Instance ?? GetNodeOrNull<PlayerDataManager>("/root/PlayerDataManager");

		_rankAtmosphere = GetNodeOrNull<Control>("RankAtmosphere");
		_rankPulseAura = GetNodeOrNull<Control>("RankAtmosphere/PulseAura");
		_backBtn = GetNodeOrNull<Button>("SafeMargin/RootVBox/HeaderRow/BackBtn");
		_rulesBtn = GetNodeOrNull<Button>("SafeMargin/RootVBox/HeaderRow/RulesBtn");

		_tabNormal = GetNodeOrNull<TextureButton>("SafeMargin/RootVBox/MainSplit/LeftCol/TabNormal");
		_tabRanked = GetNodeOrNull<TextureButton>("SafeMargin/RootVBox/MainSplit/LeftCol/RankGroup/TabRanked");
		_rankLockIcon = GetNodeOrNull<TextureRect>("SafeMargin/RootVBox/MainSplit/LeftCol/RankGroup/TabRanked/LockIcon");
		_rankReqLabel = GetNodeOrNull<Label>("SafeMargin/RootVBox/MainSplit/LeftCol/RankGroup/RankReqLabel");
		_tabBot = GetNodeOrNull<TextureButton>("SafeMargin/RootVBox/MainSplit/LeftCol/TabBot");
		_tabCustom = GetNodeOrNull<TextureButton>("SafeMargin/RootVBox/MainSplit/LeftCol/TabCustom");

		_teamSection = GetNodeOrNull<Control>("SafeMargin/RootVBox/MainSplit/RightCol/TeamSection");
		_btn1vs1 = GetNodeOrNull<TextureButton>("SafeMargin/RootVBox/MainSplit/RightCol/TeamSection/TeamRow/Btn1vs1");
		_btn2vs2 = GetNodeOrNull<TextureButton>("SafeMargin/RootVBox/MainSplit/RightCol/TeamSection/TeamRow/Btn2vs2");
		_marker1vs1 = GetNodeOrNull<Control>("SafeMargin/RootVBox/MainSplit/RightCol/TeamSection/TeamRow/Btn1vs1/ActiveMarker");
		_marker2vs2 = GetNodeOrNull<Control>("SafeMargin/RootVBox/MainSplit/RightCol/TeamSection/TeamRow/Btn2vs2/ActiveMarker");

		_mapSection = GetNodeOrNull<Control>("SafeMargin/RootVBox/MainSplit/RightCol/MapSection");
		_mapsScroll = GetNodeOrNull<ScrollContainer>("SafeMargin/RootVBox/MainSplit/RightCol/MapSection/MapsPanel/Margin/MapsScroll");
		_mapsGrid = GetNodeOrNull<GridContainer>("SafeMargin/RootVBox/MainSplit/RightCol/MapSection/MapsPanel/Margin/MapsScroll/MapsGrid");

		_botOptionsRow = GetNodeOrNull<Control>("SafeMargin/RootVBox/MainSplit/RightCol/DynamicArea/BotOptionsRow");
		_botDiffBtns = new[]
		{
			GetNodeOrNull<TextureButton>("SafeMargin/RootVBox/MainSplit/RightCol/DynamicArea/BotOptionsRow/BtnBotEasy"),
			GetNodeOrNull<TextureButton>("SafeMargin/RootVBox/MainSplit/RightCol/DynamicArea/BotOptionsRow/BtnBotMedium"),
			GetNodeOrNull<TextureButton>("SafeMargin/RootVBox/MainSplit/RightCol/DynamicArea/BotOptionsRow/BtnBotHard")
		};

		const string customRoot = "SafeMargin/RootVBox/MainSplit/RightCol/DynamicArea/CustomOptionsPanel/Margin/VBox";
		_customOptionsPanel = GetNodeOrNull<Control>("SafeMargin/RootVBox/MainSplit/RightCol/DynamicArea/CustomOptionsPanel");

		_specBtns = new[]
		{
			GetNodeOrNull<Button>($"{customRoot}/Row1/SpecGroup/BtnSpec0"),
			GetNodeOrNull<Button>($"{customRoot}/Row1/SpecGroup/BtnSpec2"),
			GetNodeOrNull<Button>($"{customRoot}/Row1/SpecGroup/BtnSpec4")
		};
		_inviteFriendsBtn = GetNodeOrNull<Button>($"{customRoot}/Row1/BtnInviteFriends");
		_consumableToggleBtn = GetNodeOrNull<Button>($"{customRoot}/Row1/BtnConsumableToggle");
		_privacyToggleBtn = GetNodeOrNull<Button>($"{customRoot}/Row1/BtnPrivacyToggle");

		_timeBtns = new[]
		{
			GetNodeOrNull<Button>($"{customRoot}/Row2/TimeGroup/BtnTime3"),
			GetNodeOrNull<Button>($"{customRoot}/Row2/TimeGroup/BtnTime5"),
			GetNodeOrNull<Button>($"{customRoot}/Row2/TimeGroup/BtnTime7"),
			GetNodeOrNull<Button>($"{customRoot}/Row2/TimeGroup/BtnTimeUnlimited")
		};
		_timeInput = GetNodeOrNull<LineEdit>($"{customRoot}/Row2/TimeGroup/TimeInput");

		_redRadishBtns = new[]
		{
			GetNodeOrNull<Button>($"{customRoot}/Row3/RedGroup/BtnRed0"),
			GetNodeOrNull<Button>($"{customRoot}/Row3/RedGroup/BtnRed3"),
			GetNodeOrNull<Button>($"{customRoot}/Row3/RedGroup/BtnRed6"),
			GetNodeOrNull<Button>($"{customRoot}/Row3/RedGroup/BtnRedUnlimited")
		};
		_redInput = GetNodeOrNull<LineEdit>($"{customRoot}/Row3/RedGroup/RedInput");

		_whiteRadishBtns = new[]
		{
			GetNodeOrNull<Button>($"{customRoot}/Row4/WhiteGroup/BtnWhite0"),
			GetNodeOrNull<Button>($"{customRoot}/Row4/WhiteGroup/BtnWhite2"),
			GetNodeOrNull<Button>($"{customRoot}/Row4/WhiteGroup/BtnWhite4"),
			GetNodeOrNull<Button>($"{customRoot}/Row4/WhiteGroup/BtnWhiteUnlimited")
		};
		_whiteInput = GetNodeOrNull<LineEdit>($"{customRoot}/Row4/WhiteGroup/WhiteInput");

		_confirmBtn = GetNodeOrNull<Button>("SafeMargin/RootVBox/FooterRow/ConfirmBtn");

		_rulesModal = GetNodeOrNull<Control>("RulesModal");
		_rulesBackdrop = GetNodeOrNull<Control>("RulesModal/Backdrop");
		_rulesDialogBox = GetNodeOrNull<Control>("RulesModal/Center/DialogBox");
		_rulesCloseBtn = GetNodeOrNull<Button>("RulesModal/Center/DialogBox/Margin/VBox/Header/CloseBtn");
		_rulesOkBtn = GetNodeOrNull<Button>("RulesModal/Center/DialogBox/Margin/VBox/Footer/OkBtn");
		_rulesTabBtns = new[]
		{
			GetNodeOrNull<Button>("RulesModal/Center/DialogBox/Margin/VBox/TabsRow/TabRuleNormal"),
			GetNodeOrNull<Button>("RulesModal/Center/DialogBox/Margin/VBox/TabsRow/TabRuleRank"),
			GetNodeOrNull<Button>("RulesModal/Center/DialogBox/Margin/VBox/TabsRow/TabRuleBot"),
			GetNodeOrNull<Button>("RulesModal/Center/DialogBox/Margin/VBox/TabsRow/TabRuleCustom")
		};
		_rulesBodyTitle = GetNodeOrNull<Label>("RulesModal/Center/DialogBox/Margin/VBox/BodyPanel/Margin/VBox/BodyTitle");
		_rulesBodyLabel = GetNodeOrNull<Label>("RulesModal/Center/DialogBox/Margin/VBox/BodyPanel/Margin/VBox/BodyLabel");

		_inviteModal = GetNodeOrNull<Control>("InviteModal");
		_inviteBackdrop = GetNodeOrNull<Control>("InviteModal/Backdrop");
		_inviteDialogBox = GetNodeOrNull<Control>("InviteModal/Center/DialogBox");
		_inviteCloseBtn = GetNodeOrNull<Button>("InviteModal/Center/DialogBox/Margin/VBox/Header/CloseBtn");
		_inviteDoneBtn = GetNodeOrNull<Button>("InviteModal/Center/DialogBox/Margin/VBox/Footer/DoneBtn");
		_inviteRoomSummaryLabel = GetNodeOrNull<Label>("InviteModal/Center/DialogBox/Margin/VBox/RoomSummaryLabel");
		_inviteScroll = GetNodeOrNull<ScrollContainer>("InviteModal/Center/DialogBox/Margin/VBox/FriendsPanel/Margin/FriendsScroll");
		_inviteListVBox = GetNodeOrNull<VBoxContainer>("InviteModal/Center/DialogBox/Margin/VBox/FriendsPanel/Margin/FriendsScroll/ContentMargin/FriendsListVBox");

		_toastBanner = GetNodeOrNull<PanelContainer>("ToastBanner");
		_toastLabel = GetNodeOrNull<Label>("ToastBanner/ToastLabel");

		InitStyles();
		StyleScrollbar(_mapsScroll);
		StyleScrollbar(_inviteScroll);
		WireSignals();

		if (_toastBanner != null) _toastBanner.Visible = false;
		if (_rulesModal != null) _rulesModal.Visible = false;
		if (_inviteModal != null) _inviteModal.Visible = false;
		if (_rankAtmosphere != null) _rankAtmosphere.Modulate = new Color(1, 1, 1, 0);

		UIButtonHelper.AttachToAllButtons(this);

		if (GetTree().CurrentScene == this)
		{
			_isOverlayMode = false;
			OpenWithFocus("type", animate: false);
		}
		else
		{
			_isOverlayMode = true;
			Visible = false;
		}
	}

	public override void _ExitTree()
	{
		_rankAuraTween?.Kill();
		_rankPulseLoopTween?.Kill();
		_modalTween?.Kill();
		_rulesTween?.Kill();
		_inviteTween?.Kill();
		_toastTween?.Kill();
	}

	public override void _Process(double delta)
	{
		if (_inviteModal == null || !_inviteModal.Visible || _inviteCooldownEndSec.Count == 0)
			return;

		double now = Time.GetTicksMsec() / 1000.0;
		List<string> finishedIds = null;

		foreach (var kv in _inviteCooldownEndSec)
		{
			double rem = kv.Value - now;
			int ceilSec = rem > 0.0 ? Mathf.Clamp((int)Math.Ceiling(rem), 1, (int)InviteCooldownSeconds) : 0;

			if (_inviteRowMap.TryGetValue(kv.Key, out var row) && row.LastSecShown != ceilSec)
			{
				ApplyFriendRowState(row, now);
			}

			if (rem <= 0.0)
			{
				finishedIds ??= new List<string>();
				finishedIds.Add(kv.Key);
			}
		}

		if (finishedIds != null)
		{
			foreach (string id in finishedIds)
			{
				_inviteCooldownEndSec.Remove(id);
			}
		}
	}

	private void InitStyles()
	{
		_pillActive = MakeSharpStyle(new Color(0.95f, 0.72f, 0.16f, 1f), new Color(1.0f, 0.90f, 0.45f, 1f), 2, 6, 14, 11, 5);
		_pillInactive = MakeSharpStyle(new Color(0.24f, 0.16f, 0.42f, 0.96f), new Color(0.52f, 0.42f, 0.76f, 0.65f), 2, 6, 14, 11, 5);
		_pillToggleOn = MakeSharpStyle(new Color(0.16f, 0.68f, 0.36f, 1f), new Color(0.45f, 0.96f, 0.64f, 0.9f), 2, 6, 16, 11, 5);
		_pillToggleOff = MakeSharpStyle(new Color(0.32f, 0.24f, 0.44f, 0.95f), new Color(0.62f, 0.52f, 0.75f, 0.7f), 2, 6, 16, 11, 5);
		_inviteBtnStyle = MakeSharpStyle(new Color(0.14f, 0.45f, 0.86f, 1f), new Color(0.54f, 0.84f, 1.0f, 0.95f), 2, 6, 16, 11, 5);
		_inviteWaitingStyle = MakeSharpStyle(new Color(0.25f, 0.19f, 0.38f, 0.95f), new Color(0.86f, 0.68f, 0.28f, 0.75f), 2, 6, 14, 11, 5);
		_inviteResendStyle = MakeSharpStyle(new Color(0.95f, 0.72f, 0.16f, 1f), new Color(1.0f, 0.90f, 0.45f, 1f), 2, 6, 16, 11, 5);
		_avatarFrameStyle = MakeSharpStyle(new Color(0.84f, 0.87f, 0.90f, 1f), new Color(1.0f, 0.88f, 0.45f, 0.85f), 2, 6, 4, 4, 4);
		_friendCardStyle = MakeSharpStyle(new Color(0.20f, 0.12f, 0.36f, 0.96f), new Color(0.46f, 0.36f, 0.68f, 0.60f), 2, 6, 14, 10, 10);

		_cardBorderNormal = MakeSharpStyle(new Color(0, 0, 0, 0), new Color(0.55f, 0.45f, 0.80f, 0.70f), 2, 6, 0, 0, 0);
		_cardBorderSelected = MakeSharpStyle(new Color(0, 0, 0, 0), new Color(1.0f, 0.84f, 0.25f, 1f), 4, 6, 0, 0, 0);

		_confirmStyleGreen = MakeSharpStyle(new Color(0.16f, 0.72f, 0.38f, 1f), new Color(0.50f, 0.98f, 0.68f, 0.95f), 2, 6, 26, 15, 6);

		_rulesTabActive = MakeSharpStyle(new Color(0.95f, 0.72f, 0.16f, 1f), new Color(1.0f, 0.90f, 0.45f, 1f), 2, 6, 16, 12, 6);
		_rulesTabInactive = MakeSharpStyle(new Color(0.22f, 0.14f, 0.38f, 0.95f), new Color(0.48f, 0.38f, 0.72f, 0.65f), 2, 6, 16, 12, 6);

		_inputActiveStyle = MakeSharpStyle(new Color(0.10f, 0.05f, 0.22f, 0.98f), new Color(1.0f, 0.86f, 0.28f, 1f), 2, 6, 10, 10, 5);
		_inputInactiveStyle = MakeSharpStyle(new Color(0.18f, 0.11f, 0.34f, 0.96f), new Color(0.52f, 0.42f, 0.76f, 0.65f), 2, 6, 10, 10, 5);
	}

	private static StyleBoxFlat MakeSharpStyle(Color bg, Color border, int borderWidth, int radius, int padX, int padTop, int padBottom) => new()
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
		ContentMarginTop = padTop,
		ContentMarginRight = padX,
		ContentMarginBottom = padBottom
	};

	private static void StyleScrollbar(ScrollContainer scroll)
	{
		var vScroll = scroll?.GetVScrollBar();
		if (vScroll == null) return;

		vScroll.CustomMinimumSize = new Vector2(10, 0);
		var track = new StyleBoxFlat
		{
			BgColor = new Color(0.08f, 0.04f, 0.16f, 0.85f),
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomRight = 4,
			CornerRadiusBottomLeft = 4
		};
		var grabber = new StyleBoxFlat
		{
			BgColor = new Color(0.85f, 0.68f, 0.24f, 0.85f),
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomRight = 4,
			CornerRadiusBottomLeft = 4
		};
		var grabberHover = new StyleBoxFlat
		{
			BgColor = new Color(1.0f, 0.84f, 0.36f, 1f),
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomRight = 4,
			CornerRadiusBottomLeft = 4
		};
		vScroll.AddThemeStyleboxOverride("scroll", track);
		vScroll.AddThemeStyleboxOverride("grabber", grabber);
		vScroll.AddThemeStyleboxOverride("grabber_highlight", grabberHover);
		vScroll.AddThemeStyleboxOverride("grabber_pressed", grabberHover);
	}

	private void WireSignals()
	{
		if (_backBtn != null) _backBtn.Pressed += OnBackPressed;
		if (_rulesBtn != null) _rulesBtn.Pressed += OpenRulesModal;
		if (_confirmBtn != null) _confirmBtn.Pressed += OnConfirmPressed;

		if (_tabNormal != null) _tabNormal.Pressed += () => SelectPlayType(PlayMatchType.Normal);
		if (_tabRanked != null) _tabRanked.Pressed += () => SelectPlayType(PlayMatchType.Ranked);
		if (_tabBot != null) _tabBot.Pressed += () => SelectPlayType(PlayMatchType.Bot);
		if (_tabCustom != null) _tabCustom.Pressed += () => SelectPlayType(PlayMatchType.Custom);

		if (_btn1vs1 != null) _btn1vs1.Pressed += () => SelectTeamMode(TeamMode.Solo1v1);
		if (_btn2vs2 != null) _btn2vs2.Pressed += () => SelectTeamMode(TeamMode.Duo2v2);

		for (int i = 0; i < _botDiffBtns.Length; i++)
		{
			int diffIdx = i;
			if (_botDiffBtns[i] != null)
			{
				_botDiffBtns[i].Pressed += () => SelectBotDifficulty((BotDifficulty)diffIdx);
			}
		}

		// Ghế người xem (giới hạn tối đa 4 ghế để tránh nghẽn phòng)
		for (int i = 0; i < _specBtns.Length && i < GameCatalog.AllowedSpectatorPresets.Length; i++)
		{
			int val = GameCatalog.AllowedSpectatorPresets[i];
			if (_specBtns[i] != null) _specBtns[i].Pressed += () =>
			{
				_draftSpectators = val;
				RefreshAllUI(animateTabs: false);
			};
		}
		if (_inviteFriendsBtn != null) _inviteFriendsBtn.Pressed += OpenInviteModal;

		// Thời gian (3p, 5p, 7p, Không giới hạn=0, Input 1..99)
		int[] timeValues = { 3, 5, 7, 0 };
		for (int i = 0; i < _timeBtns.Length; i++)
		{
			int val = timeValues[i];
			if (_timeBtns[i] != null) _timeBtns[i].Pressed += () =>
			{
				_editingTimeCustom = false;
				_draftMinutes = val;
				ClearInput(_timeInput);
				RefreshAllUI(animateTabs: false);
			};
		}
		WireNumericInput(_timeInput, 1, 99, val => _draftMinutes = val, active => { _editingTimeCustom = active; UpdateCustomOptionVisuals(); });

		// Củ cải Đỏ (0, 3, 6, Không giới hạn=-1, Input 0..999)
		int[] redValues = { 0, 3, 6, -1 };
		for (int i = 0; i < _redRadishBtns.Length; i++)
		{
			int val = redValues[i];
			if (_redRadishBtns[i] != null) _redRadishBtns[i].Pressed += () =>
			{
				_editingRedCustom = false;
				_draftRedRadish = val;
				ClearInput(_redInput);
				RefreshAllUI(animateTabs: false);
			};
		}
		WireNumericInput(_redInput, 0, 999, val => _draftRedRadish = val, active => { _editingRedCustom = active; UpdateCustomOptionVisuals(); });

		// Củ cải Trắng (0, 2, 4, Không giới hạn=-1, Input 0..999)
		int[] whiteValues = { 0, 2, 4, -1 };
		for (int i = 0; i < _whiteRadishBtns.Length; i++)
		{
			int val = whiteValues[i];
			if (_whiteRadishBtns[i] != null) _whiteRadishBtns[i].Pressed += () =>
			{
				_editingWhiteCustom = false;
				_draftWhiteRadish = val;
				ClearInput(_whiteInput);
				RefreshAllUI(animateTabs: false);
			};
		}
		WireNumericInput(_whiteInput, 0, 999, val => _draftWhiteRadish = val, active => { _editingWhiteCustom = active; UpdateCustomOptionVisuals(); });

		if (_consumableToggleBtn != null)
		{
			_consumableToggleBtn.Pressed += () => { _draftAllowConsumables = !_draftAllowConsumables; RefreshAllUI(animateTabs: false); };
		}
		if (_privacyToggleBtn != null)
		{
			_privacyToggleBtn.Pressed += () => { _draftIsPrivate = !_draftIsPrivate; RefreshAllUI(animateTabs: false); };
		}

		if (_rulesCloseBtn != null) _rulesCloseBtn.Pressed += CloseRulesModal;
		if (_rulesOkBtn != null) _rulesOkBtn.Pressed += CloseRulesModal;
		WireBackdropClick(_rulesBackdrop, CloseRulesModal);
		for (int i = 0; i < _rulesTabBtns.Length; i++)
		{
			int idx = i;
			if (_rulesTabBtns[i] != null)
			{
				_rulesTabBtns[i].Pressed += () => SwitchRulesTab((PlayMatchType)idx);
			}
		}

		if (_inviteCloseBtn != null) _inviteCloseBtn.Pressed += CloseInviteModal;
		if (_inviteDoneBtn != null) _inviteDoneBtn.Pressed += CloseInviteModal;
		WireBackdropClick(_inviteBackdrop, CloseInviteModal);
	}

	private void WireNumericInput(LineEdit input, int minVal, int maxVal, Action<int> applyValue, Action<bool> setCustomActive)
	{
		if (input == null) return;

		input.FocusEntered += () =>
		{
			setCustomActive(true);
		};

		input.FocusExited += () =>
		{
			if (string.IsNullOrWhiteSpace(input.Text))
			{
				setCustomActive(false);
			}
		};

		input.TextChanged += (raw) =>
		{
			if (_suppressInputSignal) return;
			string digits = "";
			foreach (char ch in raw)
			{
				if (char.IsDigit(ch)) digits += ch;
			}
			if (digits != raw)
			{
				_suppressInputSignal = true;
				input.Text = digits;
				input.CaretColumn = digits.Length;
				_suppressInputSignal = false;
			}
			if (int.TryParse(digits, out int parsed))
			{
				int clamped = Mathf.Clamp(parsed, minVal, maxVal);
				if (clamped != parsed)
				{
					_suppressInputSignal = true;
					input.Text = clamped.ToString();
					input.CaretColumn = input.Text.Length;
					_suppressInputSignal = false;
				}
				applyValue(clamped);
				setCustomActive(true);
			}
		};
		input.TextSubmitted += (_) => input.ReleaseFocus();
	}

	private void ClearInput(LineEdit input)
	{
		if (input == null) return;
		_suppressInputSignal = true;
		input.Text = string.Empty;
		_suppressInputSignal = false;
		if (input.HasFocus()) input.ReleaseFocus();
	}

	public void OpenWithFocus(string focusSection = "type", bool animate = true)
	{
		LoadDraftFromData();
		Visible = true;
		if (_isOverlayMode)
		{
			CallDeferred(CanvasItem.MethodName.MoveToFront);
		}
		RefreshAllUI(animateTabs: false);

		if (animate)
		{
			Modulate = new Color(1, 1, 1, 0);
			_modalTween?.Kill();
			_modalTween = CreateTween();
			_modalTween.TweenProperty(this, "modulate:a", 1f, 0.16f)
				.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
		}
		else
		{
			Modulate = Colors.White;
		}

		Control highlightTarget = focusSection switch
		{
			"team" => _teamSection,
			"map" => _mapSection,
			_ => null
		};
		if (highlightTarget != null)
		{
			highlightTarget.PivotOffset = highlightTarget.Size / 2f;
			var pulse = CreateTween();
			pulse.TweenProperty(highlightTarget, "scale", new Vector2(1.02f, 1.02f), 0.14f)
				.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
			pulse.TweenProperty(highlightTarget, "scale", Vector2.One, 0.18f)
				.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		}
	}

	private void LoadDraftFromData()
	{
		if (_data == null) return;
		_draftPlayType = _data.SelectedPlayType;
		_draftTeamMode = _data.SelectedTeamMode;
		_draftMapId = _data.SelectedMapId;
		_draftBotDiff = _data.SelectedBotDifficulty;
		_draftSpectators = GameCatalog.NormalizeSpectatorCount(_data.CustomSpectatorCount);
		_draftMinutes = _data.CustomMatchMinutes;
		_draftRedRadish = _data.CustomStartingRedRadish;
		_draftWhiteRadish = _data.CustomStartingWhiteRadish;
		_draftAllowConsumables = _data.CustomAllowConsumables;
		_draftIsPrivate = _data.CustomIsPrivate;

		_editingTimeCustom = Array.IndexOf(new[] { 3, 5, 7, 0 }, _draftMinutes) < 0;
		_editingRedCustom = Array.IndexOf(new[] { 0, 3, 6, -1 }, _draftRedRadish) < 0;
		_editingWhiteCustom = Array.IndexOf(new[] { 0, 2, 4, -1 }, _draftWhiteRadish) < 0;
	}

	private void SelectPlayType(PlayMatchType playType)
	{
		if (playType == PlayMatchType.Ranked && (_data == null || !_data.IsRankedUnlocked))
		{
			ShakeControl(_tabRanked);
			ShowToast($"Chỉ mở khi bạn đạt Level {PlayerDataManager.RankRequiredLevel}!");
			return;
		}

		_draftPlayType = playType;

		var curMap = GameCatalog.FindMap(_draftMapId);
		if (playType == PlayMatchType.Ranked && (curMap == null || !curMap.AllowInRanked || curMap.IsComingSoon))
		{
			foreach (var m in GameCatalog.Maps)
			{
				if (m.AllowInRanked && !m.IsComingSoon)
				{
					_draftMapId = m.Id;
					break;
				}
			}
		}

		RefreshAllUI(animateTabs: true);
	}

	private void SelectTeamMode(TeamMode mode)
	{
		_draftTeamMode = mode;
		RefreshAllUI(animateTabs: false);
	}

	private void SelectBotDifficulty(BotDifficulty diff)
	{
		_draftBotDiff = diff;
		RefreshAllUI(animateTabs: false);
	}

	private void RefreshAllUI(bool animateTabs)
	{
		bool rankUnlocked = _data != null && _data.IsRankedUnlocked;

		UpdatePlayTabVisual(_tabNormal, TexTabNormal, _draftPlayType == PlayMatchType.Normal, animateTabs);
		UpdatePlayTabVisual(_tabRanked, rankUnlocked ? TexTabRank : TexTabLocked, _draftPlayType == PlayMatchType.Ranked, animateTabs);
		UpdatePlayTabVisual(_tabBot, TexTabBot, _draftPlayType == PlayMatchType.Bot, animateTabs);
		UpdatePlayTabVisual(_tabCustom, TexTabCustom, _draftPlayType == PlayMatchType.Custom, animateTabs);

		if (_rankLockIcon != null) _rankLockIcon.Visible = !rankUnlocked;
		if (_rankReqLabel != null) _rankReqLabel.Visible = !rankUnlocked;

		UpdateRankAtmosphere(_draftPlayType == PlayMatchType.Ranked);

		UpdateTeamBtnVisual(_btn1vs1, TexModeInactive, _marker1vs1, _draftTeamMode == TeamMode.Solo1v1);
		UpdateTeamBtnVisual(_btn2vs2, TexModeActive, _marker2vs2, _draftTeamMode == TeamMode.Duo2v2);

		RebuildMapsGrid();

		if (_botOptionsRow != null) _botOptionsRow.Visible = _draftPlayType == PlayMatchType.Bot;
		if (_customOptionsPanel != null) _customOptionsPanel.Visible = _draftPlayType == PlayMatchType.Custom;

		if (_draftPlayType == PlayMatchType.Bot)
		{
			UpdateBotDiffVisuals();
		}
		else if (_draftPlayType == PlayMatchType.Custom)
		{
			UpdateCustomOptionVisuals();
		}

		if (_confirmBtn != null)
		{
			_confirmBtn.AddThemeStyleboxOverride("normal", _confirmStyleGreen);
			_confirmBtn.AddThemeStyleboxOverride("hover", _confirmStyleGreen);
			_confirmBtn.AddThemeStyleboxOverride("pressed", _confirmStyleGreen);
		}
	}

	private static void UpdatePlayTabVisual(TextureButton btn, Texture2D tex, bool active, bool animate)
	{
		if (btn == null) return;
		if (tex != null) btn.TextureNormal = tex;

		float targetW = active ? ActiveTabWidth : InactiveTabWidth;
		if (animate)
		{
			var tw = btn.CreateTween().SetParallel(true);
			tw.TweenProperty(btn, "custom_minimum_size:x", targetW, 0.18f)
				.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
			tw.TweenProperty(btn, "size:x", targetW, 0.18f)
				.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		}
		else
		{
			btn.CustomMinimumSize = new Vector2(targetW, btn.CustomMinimumSize.Y);
			btn.Size = new Vector2(targetW, btn.Size.Y);
		}

		btn.Modulate = active ? Colors.White : new Color(0.84f, 0.84f, 0.90f, 0.94f);
	}

	private static void UpdateTeamBtnVisual(TextureButton btn, Texture2D dedicatedTex, Control activeMarker, bool active)
	{
		if (btn == null) return;
		if (dedicatedTex != null) btn.TextureNormal = dedicatedTex;
		btn.SelfModulate = active ? Colors.White : new Color(0.68f, 0.68f, 0.74f, 0.85f);
		if (activeMarker != null) activeMarker.Visible = active;

		var lbl = btn.GetNodeOrNull<Label>("Label");
		if (lbl != null)
		{
			lbl.AddThemeColorOverride("font_color", active ? new Color(1.0f, 0.94f, 0.45f, 1f) : new Color(0.92f, 0.92f, 0.96f, 0.85f));
		}
	}

	private void UpdateRankAtmosphere(bool isRanked)
	{
		if (_rankAtmosphere == null) return;

		_rankAuraTween?.Kill();
		_rankPulseLoopTween?.Kill();
		_rankAuraTween = CreateTween();

		if (isRanked)
		{
			_rankAuraTween.TweenProperty(_rankAtmosphere, "modulate:a", 1f, 0.28f)
				.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);

			if (_rankPulseAura != null)
			{
				_rankPulseLoopTween = CreateTween().SetLoops();
				_rankPulseLoopTween.TweenProperty(_rankPulseAura, "modulate:a", 0.92f, 0.9f);
				_rankPulseLoopTween.TweenProperty(_rankPulseAura, "modulate:a", 0.32f, 0.9f);
			}
		}
		else
		{
			_rankAuraTween.TweenProperty(_rankAtmosphere, "modulate:a", 0f, 0.20f)
				.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
		}
	}

	private void RebuildMapsGrid()
	{
		if (_mapsGrid == null) return;

		foreach (Node child in _mapsGrid.GetChildren())
		{
			child.QueueFree();
		}

		bool rankOnly = _draftPlayType == PlayMatchType.Ranked;
		foreach (var map in GameCatalog.Maps)
		{
			if (rankOnly && (!map.AllowInRanked || map.IsComingSoon))
				continue;

			bool isSelected = map.Id == _draftMapId && !map.IsComingSoon;
			_mapsGrid.AddChild(BuildMapCard(map, isSelected));
		}
	}

	private Control BuildMapCard(MapMeta map, bool isSelected)
	{
		var wrapper = new Control
		{
			CustomMinimumSize = new Vector2(0, 154),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Pass
		};

		var cardBody = new Panel
		{
			ClipChildren = ClipChildrenMode.AndDraw
		};
		cardBody.SetAnchorsPreset(LayoutPreset.FullRect);
		cardBody.AddThemeStyleboxOverride("panel", MakeSharpStyle(
			new Color(0.16f, 0.12f, 0.28f, 1f),
			Colors.Transparent,
			0, 6, 0, 0, 0));
		wrapper.AddChild(cardBody);

		var art = new TextureRect
		{
			Texture = LoadMapTexture(map.TexturePath),
			Modulate = map.IsComingSoon
				? new Color(1.0f, 1.0f, 1.0f, 0.48f)
				: map.BannerTint,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			MouseFilter = MouseFilterEnum.Ignore
		};
		art.SetAnchorsPreset(LayoutPreset.FullRect);
		cardBody.AddChild(art);

		var scrim = new ColorRect
		{
			Color = map.IsComingSoon
				? new Color(0.95f, 0.95f, 1.0f, 0.14f)
				: new Color(0.03f, 0.02f, 0.08f, 0.24f),
			MouseFilter = MouseFilterEnum.Ignore
		};
		scrim.SetAnchorsPreset(LayoutPreset.FullRect);
		cardBody.AddChild(scrim);

		var borderOverlay = new Panel
		{
			MouseFilter = MouseFilterEnum.Ignore
		};
		borderOverlay.SetAnchorsPreset(LayoutPreset.FullRect);
		borderOverlay.AddThemeStyleboxOverride("panel", isSelected ? _cardBorderSelected : _cardBorderNormal);
		cardBody.AddChild(borderOverlay);

		var textVBox = new VBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center,
			MouseFilter = MouseFilterEnum.Ignore
		};
		textVBox.AddThemeConstantOverride("separation", 2);
		textVBox.SetAnchorsPreset(LayoutPreset.FullRect);
		cardBody.AddChild(textVBox);

		var nameLabel = new Label
		{
			Text = map.Name,
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore
		};
		nameLabel.AddThemeFontOverride("font", OrcishFont);
		nameLabel.AddThemeFontSizeOverride("font_size", 46);
		nameLabel.AddThemeColorOverride("font_color", map.IsComingSoon ? new Color(0.98f, 0.98f, 1f, 0.96f) : map.TitleColor);
		nameLabel.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.95f));
		nameLabel.AddThemeConstantOverride("outline_size", 6);
		textVBox.AddChild(nameLabel);

		if (isSelected)
		{
			var selectedLabel = new Label
			{
				Text = "Đang chọn",
				HorizontalAlignment = HorizontalAlignment.Center,
				MouseFilter = MouseFilterEnum.Ignore
			};
			selectedLabel.AddThemeFontOverride("font", OrcishFont);
			selectedLabel.AddThemeFontSizeOverride("font_size", 26);
			selectedLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.90f, 0.30f, 1f));
			selectedLabel.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.95f));
			selectedLabel.AddThemeConstantOverride("outline_size", 4);
			textVBox.AddChild(selectedLabel);
		}

		if (map.IsComingSoon && TexLockedIcon != null)
		{
			var lockBadge = new TextureRect
			{
				Texture = TexLockedIcon,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				Position = new Vector2(-10, -10),
				Size = new Vector2(36, 44),
				MouseFilter = MouseFilterEnum.Ignore
			};
			wrapper.AddChild(lockBadge);
		}

		var clickBtn = new Button
		{
			Flat = true,
			FocusMode = FocusModeEnum.None,
			MouseDefaultCursorShape = CursorShape.PointingHand
		};
		clickBtn.SetAnchorsPreset(LayoutPreset.FullRect);
		var empty = new StyleBoxEmpty();
		clickBtn.AddThemeStyleboxOverride("normal", empty);
		clickBtn.AddThemeStyleboxOverride("hover", empty);
		clickBtn.AddThemeStyleboxOverride("pressed", empty);
		clickBtn.Pressed += () => OnMapClicked(map, wrapper);
		cardBody.AddChild(clickBtn);

		UIButtonHelper.AttachBounceEffect(clickBtn, targetControl: wrapper);
		return wrapper;
	}

	private void OnMapClicked(MapMeta map, Control cardPanel)
	{
		if (map.IsComingSoon)
		{
			ShakeControl(cardPanel);
			ShowToast("Bản đồ này sắp ra mắt ở bản cập nhật tới!");
			return;
		}
		_draftMapId = map.Id;
		RefreshAllUI(animateTabs: false);
	}

	private void UpdateBotDiffVisuals()
	{
		Color[] activeTints =
		{
			new Color(0.24f, 0.80f, 0.38f, 1f), // Dễ - Xanh lá
			new Color(0.98f, 0.76f, 0.16f, 1f), // Vừa - Vàng
			new Color(0.65f, 0.34f, 0.95f, 1f)  // Khó - Tím
		};

		for (int i = 0; i < _botDiffBtns.Length; i++)
		{
			var btn = _botDiffBtns[i];
			if (btn == null) continue;

			bool active = i == (int)_draftBotDiff;
			btn.Modulate = active ? activeTints[i] : new Color(0.84f, 0.86f, 0.90f, 1f);

			var lbl = btn.GetNodeOrNull<Label>("Label");
			if (lbl != null)
			{
				lbl.AddThemeColorOverride("font_color", active ? Colors.White : new Color(0.15f, 0.15f, 0.22f, 1f));
				lbl.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.90f));
				lbl.AddThemeConstantOverride("outline_size", active ? 4 : 0);
			}
		}
	}

	private void UpdateCustomOptionVisuals()
	{
		for (int i = 0; i < _specBtns.Length && i < GameCatalog.AllowedSpectatorPresets.Length; i++)
		{
			StylePill(_specBtns[i], _draftSpectators == GameCatalog.AllowedSpectatorPresets[i]);
		}
		UpdateInviteFriendsButton();

		UpdateRowWithCustomInput(_timeBtns, new[] { 3, 5, 7, 0 }, _draftMinutes, _editingTimeCustom, _timeInput);
		UpdateRowWithCustomInput(_redRadishBtns, new[] { 0, 3, 6, -1 }, _draftRedRadish, _editingRedCustom, _redInput);
		UpdateRowWithCustomInput(_whiteRadishBtns, new[] { 0, 2, 4, -1 }, _draftWhiteRadish, _editingWhiteCustom, _whiteInput);

		if (_consumableToggleBtn != null)
		{
			_consumableToggleBtn.Text = _draftAllowConsumables ? "Vật phẩm: Bật" : "Vật phẩm: Tắt";
			ApplyToggleStyle(_consumableToggleBtn, _draftAllowConsumables);
		}
		if (_privacyToggleBtn != null)
		{
			_privacyToggleBtn.Text = _draftIsPrivate ? "Phòng: Chỉ bạn bè" : "Phòng: Công khai";
			ApplyToggleStyle(_privacyToggleBtn, !_draftIsPrivate);
		}
	}

	private void UpdateInviteFriendsButton()
	{
		if (_inviteFriendsBtn == null) return;
		_inviteFriendsBtn.Text = "Mời bạn";
		ApplyButtonStyle(_inviteFriendsBtn, _inviteBtnStyle, Colors.White);
	}

	private void UpdateRowWithCustomInput(Button[] presetBtns, int[] presetValues, int currentValue, bool isCustomActive, LineEdit input)
	{
		for (int i = 0; i < presetBtns.Length; i++)
		{
			StylePill(presetBtns[i], !isCustomActive && currentValue == presetValues[i]);
		}

		if (input != null)
		{
			input.Visible = true;
			var style = isCustomActive ? _inputActiveStyle : _inputInactiveStyle;
			input.AddThemeStyleboxOverride("normal", style);
			input.AddThemeStyleboxOverride("focus", _inputActiveStyle);
			input.AddThemeColorOverride("font_color", isCustomActive ? new Color(1.0f, 0.90f, 0.35f, 1f) : new Color(0.95f, 0.94f, 1f, 1f));

			if (!input.HasFocus())
			{
				_suppressInputSignal = true;
				input.Text = (isCustomActive && currentValue >= 0) ? currentValue.ToString() : string.Empty;
				_suppressInputSignal = false;
			}
		}
	}

	private void StylePill(Button btn, bool active)
	{
		Color c = active ? new Color(0.20f, 0.10f, 0.04f, 1f) : new Color(0.92f, 0.90f, 0.98f, 1f);
		ApplyButtonStyle(btn, active ? _pillActive : _pillInactive, c);
	}

	private void ApplyToggleStyle(Button btn, bool isOn)
	{
		ApplyButtonStyle(btn, isOn ? _pillToggleOn : _pillToggleOff, Colors.White);
	}

	private static void ApplyButtonStyle(Button btn, StyleBoxFlat style, Color fontColor)
	{
		if (btn == null || style == null) return;
		btn.AddThemeStyleboxOverride("normal", style);
		btn.AddThemeStyleboxOverride("hover", style);
		btn.AddThemeStyleboxOverride("pressed", style);
		btn.AddThemeStyleboxOverride("disabled", style);
		btn.AddThemeColorOverride("font_color", fontColor);
		btn.AddThemeColorOverride("font_hover_color", fontColor);
		btn.AddThemeColorOverride("font_pressed_color", fontColor);
		btn.AddThemeColorOverride("font_disabled_color", fontColor);
	}

	private void OpenRulesModal()
	{
		SwitchRulesTab(_draftPlayType);
		OpenModalAnimated(_rulesModal, _rulesBackdrop, _rulesDialogBox, ref _rulesTween);
	}

	private void CloseRulesModal()
	{
		CloseModalAnimated(_rulesModal, _rulesBackdrop, _rulesDialogBox, ref _rulesTween);
	}

	private void OpenInviteModal()
	{
		RebuildInviteFriendsList();
		OpenModalAnimated(_inviteModal, _inviteBackdrop, _inviteDialogBox, ref _inviteTween);
	}

	private void CloseInviteModal()
	{
		CloseModalAnimated(_inviteModal, _inviteBackdrop, _inviteDialogBox, ref _inviteTween);
	}

	private void OpenModalAnimated(Control modal, Control backdrop, Control dialogBox, ref Tween tween)
	{
		if (modal == null) return;
		modal.Visible = true;
		modal.MoveToFront();
		if (backdrop != null) backdrop.Modulate = new Color(1, 1, 1, 0);
		if (dialogBox != null)
		{
			dialogBox.PivotOffset = dialogBox.Size / 2f;
			dialogBox.Scale = new Vector2(0.88f, 0.88f);
			dialogBox.Modulate = new Color(1, 1, 1, 0);
		}

		tween?.Kill();
		tween = CreateTween().SetParallel(true);
		if (backdrop != null) tween.TweenProperty(backdrop, "modulate:a", 1f, 0.16f);
		if (dialogBox != null)
		{
			tween.TweenProperty(dialogBox, "scale", Vector2.One, 0.20f)
				.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
			tween.TweenProperty(dialogBox, "modulate:a", 1f, 0.16f);
		}
	}

	private void CloseModalAnimated(Control modal, Control backdrop, Control dialogBox, ref Tween tween)
	{
		if (modal == null || !modal.Visible) return;

		tween?.Kill();
		tween = CreateTween().SetParallel(true);
		if (backdrop != null) tween.TweenProperty(backdrop, "modulate:a", 0f, 0.14f);
		if (dialogBox != null)
		{
			dialogBox.PivotOffset = dialogBox.Size / 2f;
			tween.TweenProperty(dialogBox, "scale", new Vector2(0.90f, 0.90f), 0.14f);
			tween.TweenProperty(dialogBox, "modulate:a", 0f, 0.14f);
		}
		tween.Chain().TweenCallback(Callable.From(() => modal.Visible = false));
	}

	private static void WireBackdropClick(Control backdrop, Action onClose)
	{
		if (backdrop == null || onClose == null) return;
		backdrop.GuiInput += (@event) =>
		{
			bool isClick = @event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left;
			bool isTouch = @event is InputEventScreenTouch st && st.Pressed;
			if (isClick || isTouch)
			{
				backdrop.AcceptEvent();
				onClose();
			}
		};
	}

	private void RebuildInviteFriendsList()
	{
		if (_inviteRoomSummaryLabel != null)
		{
			string teamTxt = GameCatalog.GetTeamModeLabel(_draftTeamMode);
			string specTxt = _draftSpectators == 0 ? "Không" : $"{_draftSpectators} ghế";
			_inviteRoomSummaryLabel.Text = $"Chế độ: {teamTxt} · Ghế người xem: {specTxt}";
		}

		_inviteRowMap.Clear();
		if (_inviteListVBox == null) return;
		foreach (Node child in _inviteListVBox.GetChildren())
		{
			child.QueueFree();
		}

		double now = Time.GetTicksMsec() / 1000.0;

		foreach (var friend in GameCatalog.SampleFriends)
		{
			var card = new PanelContainer
			{
				SizeFlagsHorizontal = SizeFlags.ExpandFill
			};
			card.AddThemeStyleboxOverride("panel", _friendCardStyle);

			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 14);
			card.AddChild(row);

			Color statusColor = !friend.IsOnline
				? new Color(0.54f, 0.56f, 0.64f, 1f)
				: friend.IsInMatch
					? new Color(0.98f, 0.72f, 0.22f, 1f)
					: new Color(0.24f, 0.88f, 0.48f, 1f);

			// Avatar nhân vật (58x58) + chấm trạng thái online ở góc dưới phải
			var avatarHolder = new Control
			{
				CustomMinimumSize = new Vector2(58, 58),
				SizeFlagsVertical = SizeFlags.ShrinkCenter,
				MouseFilter = MouseFilterEnum.Ignore
			};
			row.AddChild(avatarHolder);

			var avatarFrame = new PanelContainer
			{
				MouseFilter = MouseFilterEnum.Ignore
			};
			avatarFrame.SetAnchorsPreset(LayoutPreset.FullRect);
			avatarFrame.AddThemeStyleboxOverride("panel", _avatarFrameStyle);
			avatarHolder.AddChild(avatarFrame);

			var avatarTex = new TextureRect
			{
				Texture = TexFriendAvatar,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				Modulate = friend.IsOnline ? Colors.White : new Color(0.62f, 0.62f, 0.70f, 0.85f),
				MouseFilter = MouseFilterEnum.Ignore
			};
			avatarFrame.AddChild(avatarTex);

			var statusDot = new Panel
			{
				Position = new Vector2(44, 44),
				Size = new Vector2(16, 16),
				MouseFilter = MouseFilterEnum.Ignore
			};
			statusDot.AddThemeStyleboxOverride("panel", MakeSharpStyle(statusColor, new Color(0.14f, 0.08f, 0.28f, 1f), 2, 8, 0, 0, 0));
			avatarHolder.AddChild(statusDot);

			var infoVBox = new VBoxContainer
			{
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				SizeFlagsVertical = SizeFlags.ShrinkCenter
			};
			infoVBox.AddThemeConstantOverride("separation", 2);
			row.AddChild(infoVBox);

			var nameLbl = new Label
			{
				Text = friend.Name,
				MouseFilter = MouseFilterEnum.Ignore
			};
			nameLbl.AddThemeFontOverride("font", OrcishFont);
			nameLbl.AddThemeFontSizeOverride("font_size", 26);
			nameLbl.AddThemeColorOverride("font_color", friend.IsOnline ? Colors.White : new Color(0.72f, 0.70f, 0.80f, 0.9f));
			infoVBox.AddChild(nameLbl);

			var subLbl = new Label
			{
				MouseFilter = MouseFilterEnum.Ignore
			};
			subLbl.AddThemeFontOverride("font", OrcishFont);
			subLbl.AddThemeFontSizeOverride("font_size", 20);
			infoVBox.AddChild(subLbl);

			var actionBtn = new Button
			{
				CustomMinimumSize = new Vector2(158, 46),
				SizeFlagsVertical = SizeFlags.ShrinkCenter,
				FocusMode = FocusModeEnum.None
			};
			actionBtn.AddThemeFontOverride("font", OrcishFont);
			actionBtn.AddThemeFontSizeOverride("font_size", 22);

			var rowUi = new InviteRowUi
			{
				Friend = friend,
				ActionBtn = actionBtn,
				SubLbl = subLbl
			};
			_inviteRowMap[friend.Id] = rowUi;
			ApplyFriendRowState(rowUi, now);

			var captured = friend;
			actionBtn.Pressed += () => OnFriendInviteClicked(captured);
			UIButtonHelper.AttachBounceEffect(actionBtn);

			row.AddChild(actionBtn);
			_inviteListVBox.AddChild(card);
		}
	}

	private void ApplyFriendRowState(InviteRowUi row, double now)
	{
		if (row?.Friend == null || row.ActionBtn == null || row.SubLbl == null) return;
		var friend = row.Friend;

		if (!friend.IsOnline || friend.IsInMatch)
		{
			Color c = friend.IsInMatch ? new Color(0.98f, 0.72f, 0.22f, 1f) : new Color(0.54f, 0.56f, 0.64f, 1f);
			string st = friend.IsInMatch ? "Đang trong trận" : "Ngoại tuyến";
			row.SubLbl.Text = $"ID: {friend.Id} · {st}";
			row.SubLbl.AddThemeColorOverride("font_color", c);

			row.ActionBtn.Disabled = false;
			row.ActionBtn.MouseDefaultCursorShape = CursorShape.PointingHand;
			row.ActionBtn.Text = friend.IsInMatch ? "Đang đấu" : "Ngoại tuyến";
			ApplyButtonStyle(row.ActionBtn, _pillInactive, new Color(0.74f, 0.72f, 0.84f, 0.9f));
			row.LastSecShown = -1;
			return;
		}

		bool wasInvited = _invitedFriendIds.Contains(friend.Id);
		double rem = _inviteCooldownEndSec.TryGetValue(friend.Id, out double endSec) ? endSec - now : 0.0;

		if (rem > 0.0)
		{
			int secLeft = Mathf.Clamp((int)Math.Ceiling(rem), 1, (int)InviteCooldownSeconds);
			row.LastSecShown = secLeft;
			row.SubLbl.Text = $"ID: {friend.Id} · Đã mời";
			row.SubLbl.AddThemeColorOverride("font_color", new Color(1.0f, 0.84f, 0.36f, 1f));

			row.ActionBtn.Disabled = true;
			row.ActionBtn.MouseDefaultCursorShape = CursorShape.Arrow;
			row.ActionBtn.Text = $"Đã mời ({secLeft}s)";
			ApplyButtonStyle(row.ActionBtn, _inviteWaitingStyle, new Color(1.0f, 0.88f, 0.45f, 0.95f));
		}
		else if (wasInvited)
		{
			row.LastSecShown = 0;
			row.SubLbl.Text = $"ID: {friend.Id} · Đã mời";
			row.SubLbl.AddThemeColorOverride("font_color", new Color(1.0f, 0.84f, 0.36f, 1f));

			row.ActionBtn.Disabled = false;
			row.ActionBtn.MouseDefaultCursorShape = CursorShape.PointingHand;
			row.ActionBtn.Text = "Mời lại";
			ApplyButtonStyle(row.ActionBtn, _inviteResendStyle, new Color(0.18f, 0.10f, 0.02f, 1f));
		}
		else
		{
			row.LastSecShown = -1;
			row.SubLbl.Text = $"ID: {friend.Id} · Trực tuyến";
			row.SubLbl.AddThemeColorOverride("font_color", new Color(0.24f, 0.88f, 0.48f, 1f));

			row.ActionBtn.Disabled = false;
			row.ActionBtn.MouseDefaultCursorShape = CursorShape.PointingHand;
			row.ActionBtn.Text = "Mời";
			ApplyButtonStyle(row.ActionBtn, _inviteBtnStyle, Colors.White);
		}
	}

	private void OnFriendInviteClicked(FriendMeta friend)
	{
		if (!friend.IsOnline)
		{
			ShowToast($"{friend.Name} đang ngoại tuyến.");
			return;
		}
		if (friend.IsInMatch)
		{
			ShowToast($"{friend.Name} đang trong trận.");
			return;
		}

		double now = Time.GetTicksMsec() / 1000.0;
		if (_inviteCooldownEndSec.TryGetValue(friend.Id, out double endSec) && endSec > now)
		{
			return;
		}

		bool firstInvite = _invitedFriendIds.Add(friend.Id);
		_inviteCooldownEndSec[friend.Id] = now + InviteCooldownSeconds;

		ShowToast(firstInvite
			? $"Đã gửi lời mời tới {friend.Name}!"
			: $"Đã gửi lại lời mời tới {friend.Name}!");

		if (_inviteRowMap.TryGetValue(friend.Id, out var row))
		{
			ApplyFriendRowState(row, now);
		}
	}

	private void SwitchRulesTab(PlayMatchType tab)
	{
		for (int i = 0; i < _rulesTabBtns.Length; i++)
		{
			var btn = _rulesTabBtns[i];
			if (btn == null) continue;
			bool active = i == (int)tab;
			Color c = active ? new Color(0.20f, 0.10f, 0.04f, 1f) : new Color(0.92f, 0.90f, 0.98f, 1f);
			ApplyButtonStyle(btn, active ? _rulesTabActive : _rulesTabInactive, c);
		}

		if (_rulesBodyTitle == null || _rulesBodyLabel == null) return;
		switch (tab)
		{
			case PlayMatchType.Ranked:
				_rulesBodyTitle.Text = "Thể lệ Đấu Xếp hạng";
				_rulesBodyLabel.Text =
					$"• Mở khóa khi tài khoản đạt từ Level {PlayerDataManager.RankRequiredLevel} trở lên.\n" +
					"• Tính điểm Rank sau mỗi trận và nhận thưởng cuối mùa.\n" +
					"• Chỉ áp dụng bản đồ chuẩn thi đấu · Củ cải Đỏ (3s) & Trắng (5s) hồi theo chuẩn.";
				break;
			case PlayMatchType.Bot:
				_rulesBodyTitle.Text = "Thể lệ Đấu với máy";
				_rulesBodyLabel.Text =
					"• Chọn thoải mái mọi chế độ (1 VS 1, 2 VS 2) và toàn bộ bản đồ đã mở.\n" +
					"• Điều chỉnh cấp độ AI: Dễ (Xanh lá), Vừa (Vàng) hoặc Khó (Tím).\n" +
					"• Ra quân bằng Củ cải Đỏ & Trắng, hạ địch nhận Vàng trong trận.";
				break;
			case PlayMatchType.Custom:
				_rulesBodyTitle.Text = "Thể lệ Phòng Tùy Chỉnh (Custom)";
				_rulesBodyLabel.Text =
					$"• Ghế người xem giới hạn tối đa {GameCatalog.MaxCustomSpectators} ghế để tránh nghẽn phòng; hỗ trợ nút Mời bạn trực tiếp.\n" +
					"• Tự do chỉnh hoặc tự nhập thời gian trận, Củ cải Đỏ và Củ cải Trắng đầu trận (kể cả Không giới hạn).\n" +
					"• Bật/tắt vật phẩm và chọn phòng Công khai hoặc Chỉ bạn bè.";
				break;
			default:
				_rulesBodyTitle.Text = "Thể lệ Đấu Thường";
				_rulesBodyLabel.Text =
					"• Không tính hoặc trừ điểm Xếp hạng.\n" +
					"• Trong trận tích lũy Củ cải Đỏ (3s/1) & Trắng (5s/1) để gọi nhân vật ra trận, hạ địch nhận Vàng.\n" +
					"• Mở toàn bộ bản đồ đấu thường.";
				break;
		}
	}

	private void OnConfirmPressed()
	{
		if (_draftPlayType == PlayMatchType.Ranked && (_data == null || !_data.IsRankedUnlocked))
		{
			ShakeControl(_confirmBtn);
			ShowToast($"Chỉ mở khi bạn đạt Level {PlayerDataManager.RankRequiredLevel}!");
			return;
		}

		_data?.SetMatchConfiguration(
			_draftPlayType,
			_draftTeamMode,
			_draftMapId,
			_draftBotDiff,
			_draftSpectators,
			_draftMinutes,
			_draftRedRadish,
			_draftWhiteRadish,
			_draftAllowConsumables,
			_draftIsPrivate);

		EmitSignal(SignalName.SelectionConfirmed);
		CloseScreen();
	}

	private void OnBackPressed()
	{
		CloseScreen();
	}

	private void CloseScreen()
	{
		if (!_isOverlayMode)
		{
			GetTree().ChangeSceneToFile("res://scenes/ui/lobby_screen.tscn");
			return;
		}

		_modalTween?.Kill();
		_modalTween = CreateTween();
		_modalTween.TweenProperty(this, "modulate:a", 0f, 0.14f);
		_modalTween.TweenCallback(Callable.From(() =>
		{
			Visible = false;
			EmitSignal(SignalName.Closed);
		}));
	}

	private void ShowToast(string message)
	{
		if (_toastBanner == null || _toastLabel == null) return;
		_toastLabel.Text = message;
		_toastBanner.Visible = true;
		_toastBanner.MoveToFront();

		float targetRight = -32f;
		float width = _toastBanner.CustomMinimumSize.X > 0 ? _toastBanner.CustomMinimumSize.X : 580f;
		float startLeft = 20f;
		float targetLeft = targetRight - width;

		_toastBanner.OffsetLeft = startLeft;
		_toastBanner.OffsetRight = startLeft + width;
		_toastBanner.Modulate = new Color(1f, 1f, 1f, 0f);

		_toastTween?.Kill();
		_toastTween = _toastBanner.CreateTween();
		_toastTween.TweenProperty(_toastBanner, "offset_left", targetLeft, 0.20f)
			.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		_toastTween.Parallel().TweenProperty(_toastBanner, "offset_right", targetRight, 0.20f)
			.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		_toastTween.Parallel().TweenProperty(_toastBanner, "modulate:a", 1f, 0.16f);

		_toastTween.TweenInterval(2.3f);

		_toastTween.TweenProperty(_toastBanner, "offset_left", startLeft, 0.20f)
			.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
		_toastTween.Parallel().TweenProperty(_toastBanner, "offset_right", startLeft + width, 0.20f)
			.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
		_toastTween.Parallel().TweenProperty(_toastBanner, "modulate:a", 0f, 0.20f);
		_toastTween.TweenCallback(Callable.From(() => _toastBanner.Visible = false));
	}

	private void ShakeControl(Control target)
	{
		if (target == null) return;
		float startX = target.Position.X;
		var tw = CreateTween();
		tw.TweenProperty(target, "position:x", startX - 8f, 0.04f);
		tw.TweenProperty(target, "position:x", startX + 8f, 0.04f);
		tw.TweenProperty(target, "position:x", startX - 5f, 0.04f);
		tw.TweenProperty(target, "position:x", startX, 0.04f);
	}

	private Texture2D LoadMapTexture(string path)
	{
		if (string.IsNullOrEmpty(path)) return null;
		if (_mapTexCache.TryGetValue(path, out var tex)) return tex;
		var loaded = ResourceLoader.Load<Texture2D>(path);
		if (loaded != null) _mapTexCache[path] = loaded;
		return loaded;
	}
}
