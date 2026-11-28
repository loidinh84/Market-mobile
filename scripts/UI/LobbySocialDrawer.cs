using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MarketMobile.Core;
using MarketMobile.Utils;

namespace MarketMobile.UI;

/// <summary>
/// Ngăn kéo Chat & Bạn bè theo bố cục sáng ấm (Warm Cream & Slate-Blue, không shadow, không viền vàng):
/// - Cột trái biến đổi mượt giữa Thu gọn (114px) và Mở rộng (396px) khi vuốt ngang hoặc bấm close_message.png.
/// - Thanh scroll ẩn hoàn toàn, hỗ trợ kéo chuột trực tiếp + quán tính ở mọi danh sách (cả chuột & cảm ứng).
/// - Icon góc phải (icon_social_group.png) mở dropdown 3 mục (Bạn bè, Thêm bạn, Lời mời).
/// </summary>
public partial class LobbySocialDrawer : Control
{
	[Signal]
	public delegate void PreviewUpdatedEventHandler();

	private enum SocialModalMode
	{
		FriendsHub,
		FriendsList,
		RequestsList,
		AddFriend
	}

	private enum GestureAxis
	{
		None,
		VerticalScroll,
		HorizontalSwipe,
		RowSwipe
	}

	private enum ThreadKind
	{
		FriendsFeed,
		World,
		Party,
		DirectMessage
	}

	private sealed class ChatEntry
	{
		public string Sender { get; init; }
		public string Text { get; init; }
		public string StickerId { get; init; }
		public bool IsSelf { get; init; }
		public string TimeLabel { get; init; }
	}

	private sealed class ChatThread
	{
		public string Id { get; init; }
		public string Title { get; init; }
		public string Subtitle { get; init; }
		public ThreadKind Kind { get; init; }
		public bool IsFriend => Kind == ThreadKind.DirectMessage;
		public bool CanPin => true;
		public bool CanDelete => Kind == ThreadKind.DirectMessage;
		public bool IsPinned { get; set; }
		public long PinnedOrder { get; set; }
		public bool IsHiddenInInbox { get; set; }
		public int UnreadCount { get; set; }
		public string DraftText { get; set; } = string.Empty;
		public List<ChatEntry> Messages { get; } = new();
	}

	private const float StripWidth = 144f;
	private const float ExpandedColWidth = 504f;
	private const float DrawerCollapsedWidth = 1180f;
	private const float DrawerExpandedWidth = 1540f;

	private const string FriendsChannelId = "friends_chan";
	private const string WorldThreadId = "world";
	private const string TeamThreadId = "team";

	private static readonly Font OrcishFont = GD.Load<Font>("res://assets/fonts/FZ-Orcish.ttf");
	private static readonly Texture2D TexAvatar = GD.Load<Texture2D>("res://assets/sprites/icons/avatar_character.png");
	private static readonly Texture2D TexSocialGroup = GD.Load<Texture2D>("res://assets/sprites/icons/social/icon_social_group.png");
	private static readonly Texture2D TexSocialAdd = GD.Load<Texture2D>("res://assets/sprites/icons/social/icon_social_add.png");
	private static readonly Texture2D TexSocialRequest = GD.Load<Texture2D>("res://assets/sprites/icons/social/icon_social_request.png");
	private static readonly Texture2D TexWorldIcon = GD.Load<Texture2D>("res://assets/sprites/icons/social/chat.png");
	private static readonly Texture2D TexTeamIcon = GD.Load<Texture2D>("res://assets/sprites/icons/role.png");
	private static readonly Texture2D TexChevronLeft = GD.Load<Texture2D>("res://assets/sprites/icons/chevron_left.svg");

	private ColorRect _backdrop;
	private Panel _drawerShell;
	private Button _collapseBtn;

	// Cột trái duy nhất (114px <-> 396px)
	private PanelContainer _leftThreadsCol;
	private Control _expandedHeader;
	private ScrollContainer _threadsScroll;
	private VBoxContainer _threadsVBox;

	// Khung chat chính
	private Button _toggleStripBtn;
	private TextureRect _toggleStripIcon;
	private Control _totalBadge;
	private Label _totalBadgeLabel;
	private Control _headerAvatarHost;
	private Label _chatHeaderTitle;
	private Button _menuIconBtn;
	private Control _reqBadge;
	private Label _reqBadgeLabel;
	private ScrollContainer _messagesScroll;
	private VBoxContainer _messagesVBox;
	private Control _stickerTrayPanel;
	private HBoxContainer _packTabsHBox;
	private Button _closeStickerTrayBtn;
	private ScrollContainer _stickerScroll;
	private GridContainer _stickerGrid;
	private LineEdit _chatInput;
	private TextureButton _emojiBtn;
	private Button _sendBtn;

	// Dropdown 3 mục góc phải
	private Control _dropdownBackdrop;
	private Control _menuDropdown;
	private Button _btnMenuFriends;
	private Button _btnMenuAddFriend;
	private Button _btnMenuRequests;

	// Popup Bạn Bè / Thêm Bạn / Lời Mời
	private Control _socialModal;
	private Control _socialModalBackdrop;
	private Control _socialDialogBox;
	private Button _backModalBtn;
	private Label _modalTitleLabel;
	private Button _closeModalBtn;
	private Control _searchRow;
	private LineEdit _searchInput;
	private Button _searchBtn;
	private ScrollContainer _modalContentScroll;
	private VBoxContainer _modalContentVBox;

	// Confirm xóa bạn
	private Control _confirmDeleteOverlay;
	private Label _confirmDeleteLabel;
	private Button _cancelDeleteBtn;
	private Button _confirmDeleteBtn;

	private readonly List<FriendMeta> _friends = new();
	private readonly List<FriendMeta> _requests = new();
	private readonly Dictionary<string, ChatThread> _threads = new();
	private readonly List<string> _threadOrder = new();
	private readonly List<(string ChannelTag, string Sender, string Text)> _recentFeed = new();
	private readonly Dictionary<string, Texture2D> _stickerTextures = new();
	private readonly Dictionary<string, (Control Holder, Control Card, float RevealWidth)> _rowWidgets = new();
	private Texture2D _texThreadPin;
	private Texture2D _texThreadDelete;

	private bool _isListExpanded = false;
	private bool _isInParty = true;
	private string _activeThreadId = FriendsChannelId;
	private SocialModalMode _modalMode = SocialModalMode.FriendsHub;
	private string _pendingDeleteTargetId;
	private bool _pendingDeleteIsConversation = false;
	private bool _isStickerTrayOpen = false;
	private bool _stickerTrayBuilt = false;
	private int _activeStickerTabIndex = 0;
	private string _swipedThreadId;
	private string _dragRowThreadId;
	private float _dragRowStartOffset = 0f;
	private float _dragRowCurrentOffset = 0f;
	private long _pinCounter = 0;

	// Drag-to-scroll & swipe state
	private bool _pointerDown = false;
	private Vector2 _dragStartPos;
	private Vector2 _lastDragPos;
	private GestureAxis _gestureAxis = GestureAxis.None;
	private ScrollContainer _dragScrollTarget;
	private float _dragAccumDy = 0f;
	private float _scrollVelocity = 0f;
	private ScrollContainer _inertiaScroll;
	private bool _suppressClickAfterDrag = false;
	private bool _stickerTrayTapOutsideCandidate = false;
	private int _pendingScrollBottomFrames = 0;

	private Tween _slideTween;
	private Tween _expandTween;
	private Tween _modalTween;
	private Tween _rowSwipeTween;

	private StyleBoxFlat _tileActiveCream;
	private StyleBoxFlat _tileInactivePlum;
	private StyleBoxFlat _bubbleOtherLight;
	private StyleBoxFlat _bubbleSelfTeal;
	private StyleBoxFlat _modalRowCardCream;
	private StyleBoxFlat _stickerTabActiveStyle;
	private StyleBoxFlat _stickerTabIdleStyle;
	private StyleBoxFlat _stickerTileStyle;
	private StyleBoxFlat _quickChatBtnStyle;
	private StyleBoxFlat _btnBlue;
	private StyleBoxFlat _btnGreen;
	private StyleBoxFlat _btnRed;
	private StyleBoxFlat _btnMuted;
	private StyleBoxFlat _avatarFrame;
	private StyleBoxFlat _badgeStyle;

	public override void _Ready()
	{
		_backdrop = GetNodeOrNull<ColorRect>("Backdrop");
		_drawerShell = GetNodeOrNull<Panel>("DrawerShell");
		_collapseBtn = GetNodeOrNull<Button>("DrawerShell/CollapseBtn");

		_leftThreadsCol = GetNodeOrNull<PanelContainer>("DrawerShell/BodyHBox/LeftThreadsCol");
		_expandedHeader = GetNodeOrNull<Control>("DrawerShell/BodyHBox/LeftThreadsCol/VBox/ExpandedHeader");
		_threadsScroll = GetNodeOrNull<ScrollContainer>("DrawerShell/BodyHBox/LeftThreadsCol/VBox/ThreadsScroll");
		_threadsVBox = GetNodeOrNull<VBoxContainer>("DrawerShell/BodyHBox/LeftThreadsCol/VBox/ThreadsScroll/ThreadsVBox");

		_toggleStripBtn = GetNodeOrNull<Button>("DrawerShell/BodyHBox/ChatPanel/HeaderPanel/HBox/ToggleStripWrap/ToggleStripBtn");
		_toggleStripIcon = GetNodeOrNull<TextureRect>("DrawerShell/BodyHBox/ChatPanel/HeaderPanel/HBox/ToggleStripWrap/ToggleStripBtn/Icon");
		_totalBadge = GetNodeOrNull<Control>("DrawerShell/BodyHBox/ChatPanel/HeaderPanel/HBox/ToggleStripWrap/TotalBadge");
		_totalBadgeLabel = GetNodeOrNull<Label>("DrawerShell/BodyHBox/ChatPanel/HeaderPanel/HBox/ToggleStripWrap/TotalBadge/CountLabel");
		_headerAvatarHost = GetNodeOrNull<Control>("DrawerShell/BodyHBox/ChatPanel/HeaderPanel/HBox/HeaderAvatarHost");
		_chatHeaderTitle = GetNodeOrNull<Label>("DrawerShell/BodyHBox/ChatPanel/HeaderPanel/HBox/HeaderTitle");
		_menuIconBtn = GetNodeOrNull<Button>("DrawerShell/BodyHBox/ChatPanel/HeaderPanel/HBox/MenuBtnWrap/MenuIconBtn");
		_reqBadge = GetNodeOrNull<Control>("DrawerShell/BodyHBox/ChatPanel/HeaderPanel/HBox/MenuBtnWrap/ReqBadge");
		_reqBadgeLabel = GetNodeOrNull<Label>("DrawerShell/BodyHBox/ChatPanel/HeaderPanel/HBox/MenuBtnWrap/ReqBadge/CountLabel");

		_messagesScroll = GetNodeOrNull<ScrollContainer>("DrawerShell/BodyHBox/ChatPanel/MessagesArea/MessagesScroll");
		_messagesVBox = GetNodeOrNull<VBoxContainer>("DrawerShell/BodyHBox/ChatPanel/MessagesArea/MessagesScroll/MessagesVBox");
		_stickerTrayPanel = GetNodeOrNull<Control>("DrawerShell/BodyHBox/ChatPanel/StickerTrayPanel");
		_packTabsHBox = GetNodeOrNull<HBoxContainer>("DrawerShell/BodyHBox/ChatPanel/StickerTrayPanel/Margin/VBox/HeaderRow/PackTabsHBox");
		_closeStickerTrayBtn = GetNodeOrNull<Button>("DrawerShell/BodyHBox/ChatPanel/StickerTrayPanel/Margin/VBox/HeaderRow/CloseTrayBtn");
		_stickerScroll = GetNodeOrNull<ScrollContainer>("DrawerShell/BodyHBox/ChatPanel/StickerTrayPanel/Margin/VBox/StickerScroll");
		_stickerGrid = GetNodeOrNull<GridContainer>("DrawerShell/BodyHBox/ChatPanel/StickerTrayPanel/Margin/VBox/StickerScroll/StickerGrid");
		_chatInput = GetNodeOrNull<LineEdit>("DrawerShell/BodyHBox/ChatPanel/InputPanel/HBox/ChatInput");
		_emojiBtn = GetNodeOrNull<TextureButton>("DrawerShell/BodyHBox/ChatPanel/InputPanel/HBox/EmojiBtn");
		_sendBtn = GetNodeOrNull<Button>("DrawerShell/BodyHBox/ChatPanel/InputPanel/HBox/SendBtn");

		_dropdownBackdrop = GetNodeOrNull<Control>("DrawerShell/MenuDropdownBackdrop");
		_menuDropdown = GetNodeOrNull<Control>("DrawerShell/MenuDropdown");
		_btnMenuFriends = GetNodeOrNull<Button>("DrawerShell/MenuDropdown/VBox/BtnMenuFriends");
		_btnMenuAddFriend = GetNodeOrNull<Button>("DrawerShell/MenuDropdown/VBox/BtnMenuAddFriend");
		_btnMenuRequests = GetNodeOrNull<Button>("DrawerShell/MenuDropdown/VBox/BtnMenuRequests");

		_socialModal = GetNodeOrNull<Control>("SocialModal");
		_socialModalBackdrop = GetNodeOrNull<Control>("SocialModal/Backdrop");
		_socialDialogBox = GetNodeOrNull<Control>("SocialModal/Center/DialogBox");
		_backModalBtn = GetNodeOrNull<Button>("SocialModal/Center/DialogBox/VBox/HeaderPanel/HBox/BackModalBtn");
		_modalTitleLabel = GetNodeOrNull<Label>("SocialModal/Center/DialogBox/VBox/HeaderPanel/HBox/TitleLabel");
		_closeModalBtn = GetNodeOrNull<Button>("SocialModal/Center/DialogBox/VBox/HeaderPanel/HBox/CloseModalBtn");
		_searchRow = GetNodeOrNull<Control>("SocialModal/Center/DialogBox/VBox/BodyMargin/BodyVBox/SearchRow");
		_searchInput = GetNodeOrNull<LineEdit>("SocialModal/Center/DialogBox/VBox/BodyMargin/BodyVBox/SearchRow/SearchInput");
		_searchBtn = GetNodeOrNull<Button>("SocialModal/Center/DialogBox/VBox/BodyMargin/BodyVBox/SearchRow/SearchBtn");
		_modalContentScroll = GetNodeOrNull<ScrollContainer>("SocialModal/Center/DialogBox/VBox/BodyMargin/BodyVBox/ContentScroll");
		_modalContentVBox = GetNodeOrNull<VBoxContainer>("SocialModal/Center/DialogBox/VBox/BodyMargin/BodyVBox/ContentScroll/ContentVBox");

		_confirmDeleteOverlay = GetNodeOrNull<Control>("ConfirmDeleteOverlay");
		_confirmDeleteLabel = GetNodeOrNull<Label>("ConfirmDeleteOverlay/Center/ConfirmBox/Margin/VBox/MsgLabel");
		_cancelDeleteBtn = GetNodeOrNull<Button>("ConfirmDeleteOverlay/Center/ConfirmBox/Margin/VBox/BtnsHBox/CancelBtn");
		_confirmDeleteBtn = GetNodeOrNull<Button>("ConfirmDeleteOverlay/Center/ConfirmBox/Margin/VBox/BtnsHBox/ConfirmBtn");

		InitStyles();
		InitSampleData();

		HideScrollbar(_threadsScroll);
		HideScrollbar(_messagesScroll);
		HideScrollbar(_stickerScroll);
		HideScrollbar(_modalContentScroll);

		var msgVBar = _messagesScroll?.GetVScrollBar();
		if (msgVBar != null) msgVBar.Changed += OnMessagesScrollBarChanged;

		WireSignals();

		if (_dropdownBackdrop != null) _dropdownBackdrop.Visible = false;
		if (_menuDropdown != null) _menuDropdown.Visible = false;
		if (_stickerTrayPanel != null) _stickerTrayPanel.Visible = false;
		if (_socialModal != null) _socialModal.Visible = false;
		if (_confirmDeleteOverlay != null) _confirmDeleteOverlay.Visible = false;
		Visible = false;

		UIButtonHelper.AttachToAllButtons(this);
		RefreshAllDrawerUI();
	}

	public override void _ExitTree()
	{
		_slideTween?.Kill();
		_expandTween?.Kill();
		_modalTween?.Kill();
		_rowSwipeTween?.Kill();
	}

	public override void _Process(double delta)
	{
		if (_pendingScrollBottomFrames > 0)
		{
			ScrollMessagesToBottom();
			_pendingScrollBottomFrames--;
		}

		if (!_pointerDown && _inertiaScroll != null && Mathf.Abs(_scrollVelocity) > 12f)
		{
			int step = Mathf.RoundToInt(_scrollVelocity * (float)delta);
			if (step != 0)
			{
				_inertiaScroll.ScrollVertical += step;
			}
			_scrollVelocity = Mathf.Lerp(_scrollVelocity, 0f, Mathf.Min(1f, (float)delta * 9.5f));
			if (Mathf.Abs(_scrollVelocity) <= 12f)
			{
				_scrollVelocity = 0f;
				_inertiaScroll = null;
			}
		}
	}

	public override void _Input(InputEvent @event)
	{
		if (!Visible || (_confirmDeleteOverlay != null && _confirmDeleteOverlay.Visible))
		{
			if (_pointerDown || _gestureAxis != GestureAxis.None) ResetDragGesture();
			return;
		}

		if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Escape)
		{
			if (_isStickerTrayOpen && (_socialModal == null || !_socialModal.Visible))
			{
				SetStickerTrayOpen(false);
				GetViewport()?.SetInputAsHandled();
				return;
			}
		}

		if (@event is InputEventMouseButton wheel && wheel.Pressed && wheel.ButtonIndex == MouseButton.WheelUp)
		{
			_pendingScrollBottomFrames = 0;
		}

		// Dùng sự kiện chuột trái làm nguồn thống nhất cho kéo cuộn & vuốt ngang (tránh đụng với emulate_touch_from_mouse)
		if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
		{
			if (mb.Pressed)
			{
				_scrollVelocity = 0f;
				_inertiaScroll = null;
				_dragStartPos = mb.Position;
				_lastDragPos = mb.Position;
				_gestureAxis = GestureAxis.None;
				_dragAccumDy = 0f;
				_dragScrollTarget = FindScrollAtPosition(mb.Position);

				if (HandleExpandedRowPointerDown(mb.Position))
				{
					ResetDragGesture();
					return;
				}
				bool insideTray = _isStickerTrayOpen && _stickerTrayPanel != null && _stickerTrayPanel.GetGlobalRect().HasPoint(mb.Position);
				bool onEmojiBtn = _emojiBtn != null && _emojiBtn.GetGlobalRect().HasPoint(mb.Position);
				bool modalClosed = _socialModal == null || !_socialModal.Visible;
				bool menuClosed = _menuDropdown == null || !_menuDropdown.Visible;
				_stickerTrayTapOutsideCandidate = _isStickerTrayOpen && modalClosed && menuClosed && !insideTray && !onEmojiBtn
					&& _drawerShell != null && _drawerShell.GetGlobalRect().HasPoint(mb.Position);

				_pointerDown = _dragScrollTarget != null || !string.IsNullOrEmpty(_dragRowThreadId) || CanStartHorizontalSwipe(mb.Position) || insideTray;
			}
			else if (_pointerDown || _stickerTrayTapOutsideCandidate)
			{
				if (_gestureAxis == GestureAxis.RowSwipe)
				{
					FinishRowSwipe();
					_suppressClickAfterDrag = true;
					CallDeferred(MethodName.ResetDragClickSuppression);
				}
				else if (_gestureAxis == GestureAxis.VerticalScroll || _gestureAxis == GestureAxis.HorizontalSwipe)
				{
					_suppressClickAfterDrag = true;
					CallDeferred(MethodName.ResetDragClickSuppression);
				}
				else if (_stickerTrayTapOutsideCandidate && _isStickerTrayOpen && _dragStartPos.DistanceTo(mb.Position) <= 12f)
				{
					SetStickerTrayOpen(false);
				}
				ResetDragGesture();
			}
		}
		else if (_pointerDown && @event is InputEventMouseMotion mm)
		{
			// Nếu chuột trái thực tế đã nhả (ví dụ nhả ngoài cửa sổ hoặc sau khi đóng popup/backdrop), hủy ngay trạng thái kéo để hover không tự cuộn
			if ((mm.ButtonMask & MouseButtonMask.Left) == 0)
			{
				if (_gestureAxis == GestureAxis.RowSwipe) FinishRowSwipe();
				ResetDragGesture();
				return;
			}

			Vector2 totalDelta = mm.Position - _dragStartPos;
			float stepDy = mm.Position.Y - _lastDragPos.Y;
			_lastDragPos = mm.Position;

			if (_gestureAxis == GestureAxis.None)
			{
				if (!string.IsNullOrEmpty(_dragRowThreadId) && _rowWidgets.TryGetValue(_dragRowThreadId, out var dr) &&
					Mathf.Abs(totalDelta.X) > 12f && Mathf.Abs(totalDelta.X) > Mathf.Abs(totalDelta.Y) * 1.25f)
				{
					bool canDragRight = totalDelta.X > 0f && _dragRowStartOffset < dr.RevealWidth - 1f;
					bool canDragLeft = totalDelta.X < 0f && _dragRowStartOffset > 1f;
					if (canDragRight || canDragLeft)
					{
						_gestureAxis = GestureAxis.RowSwipe;
						_rowSwipeTween?.Kill();
					}
				}

				if (_gestureAxis == GestureAxis.None && CanStartHorizontalSwipe(_dragStartPos) && Mathf.Abs(totalDelta.X) > 48f && Mathf.Abs(totalDelta.X) > Mathf.Abs(totalDelta.Y) * 1.5f)
				{
					_gestureAxis = GestureAxis.HorizontalSwipe;
					_pointerDown = false;
					if (totalDelta.X > 0 && !_isListExpanded) SetListExpanded(true);
					else if (totalDelta.X < 0 && _isListExpanded) SetListExpanded(false);
					GetViewport()?.SetInputAsHandled();
					return;
				}
				if (_gestureAxis == GestureAxis.None && _dragScrollTarget == null && _isStickerTrayOpen && _stickerTrayPanel != null
					&& _stickerTrayPanel.GetGlobalRect().HasPoint(_dragStartPos) && totalDelta.Y > 36f && Mathf.Abs(totalDelta.Y) > Mathf.Abs(totalDelta.X) * 1.25f)
				{
					SetStickerTrayOpen(false);
					ResetDragGesture();
					GetViewport()?.SetInputAsHandled();
					return;
				}
				if (_gestureAxis == GestureAxis.None && _dragScrollTarget != null && Mathf.Abs(totalDelta.Y) > 10f && Mathf.Abs(totalDelta.Y) >= Mathf.Abs(totalDelta.X))
				{
					if (_dragScrollTarget == _threadsScroll && !string.IsNullOrEmpty(_swipedThreadId))
					{
						SnapRowSwipe(_swipedThreadId, 0f, clearSwiped: true);
					}
					_gestureAxis = GestureAxis.VerticalScroll;
					_inertiaScroll = _dragScrollTarget;
					if (_dragScrollTarget == _messagesScroll) _pendingScrollBottomFrames = 0;
				}
			}

			if (_gestureAxis == GestureAxis.RowSwipe && !string.IsNullOrEmpty(_dragRowThreadId) && _rowWidgets.TryGetValue(_dragRowThreadId, out var activeRow))
			{
				float next = Mathf.Clamp(_dragRowStartOffset + totalDelta.X, 0f, activeRow.RevealWidth);
				ApplyRowCardOffset(_dragRowThreadId, next);
				GetViewport()?.SetInputAsHandled();
			}
			else if (_gestureAxis == GestureAxis.VerticalScroll && _dragScrollTarget != null)
			{
				_dragAccumDy -= stepDy;
				int pixels = Mathf.RoundToInt(_dragAccumDy);
				if (pixels != 0)
				{
					_dragScrollTarget.ScrollVertical += pixels;
					_dragAccumDy -= pixels;
				}
				_scrollVelocity = -stepDy * 52f;
				GetViewport()?.SetInputAsHandled();
			}
		}
		else if (@event is InputEventScreenDrag sd)
		{
			// Trên PC (emulate_touch_from_mouse=true), chuột kéo đồng thời phát ScreenDrag; chặn để ScrollContainer không cuộn đè lên
			if (_gestureAxis != GestureAxis.None)
			{
				GetViewport()?.SetInputAsHandled();
			}
		}
	}

	private bool HandleExpandedRowPointerDown(Vector2 globalPos)
	{
		_dragRowThreadId = null;
		_dragRowStartOffset = 0f;
		_dragRowCurrentOffset = 0f;

		if (!_isListExpanded || (_socialModal != null && _socialModal.Visible) || _threadsScroll == null || !_threadsScroll.IsVisibleInTree() || !_threadsScroll.GetGlobalRect().HasPoint(globalPos))
		{
			if (!string.IsNullOrEmpty(_swipedThreadId))
			{
				SnapRowSwipe(_swipedThreadId, 0f, clearSwiped: true);
			}
			return false;
		}

		foreach (var kv in _rowWidgets)
		{
			var rw = kv.Value;
			if (!IsInstanceValid(rw.Holder) || !rw.Holder.IsVisibleInTree()) continue;
			if (!rw.Holder.GetGlobalRect().HasPoint(globalPos)) continue;

			// Nếu bấm trúng vùng khay Ghim/Xóa đang lộ ra bên trái card thì nhường cho Button xử lý
			if (_swipedThreadId == kv.Key && IsInstanceValid(rw.Card) && globalPos.X < rw.Card.GetGlobalRect().Position.X)
			{
				return true;
			}

			if (!string.IsNullOrEmpty(_swipedThreadId) && _swipedThreadId != kv.Key)
			{
				SnapRowSwipe(_swipedThreadId, 0f, clearSwiped: true);
			}

			if (rw.RevealWidth > 0f && IsInstanceValid(rw.Card))
			{
				_dragRowThreadId = kv.Key;
				_dragRowStartOffset = rw.Card.OffsetLeft;
				_dragRowCurrentOffset = _dragRowStartOffset;
			}
			return false;
		}

		if (!string.IsNullOrEmpty(_swipedThreadId))
		{
			SnapRowSwipe(_swipedThreadId, 0f, clearSwiped: true);
		}
		return false;
	}

	private void FinishRowSwipe()
	{
		if (string.IsNullOrEmpty(_dragRowThreadId) || !_rowWidgets.TryGetValue(_dragRowThreadId, out var rw)) return;
		bool shouldOpen = _dragRowCurrentOffset >= rw.RevealWidth * 0.42f;
		SnapRowSwipe(_dragRowThreadId, shouldOpen ? rw.RevealWidth : 0f, clearSwiped: !shouldOpen);
	}

	private void SnapRowSwipe(string threadId, float targetOffset, bool clearSwiped)
	{
		_swipedThreadId = clearSwiped ? null : threadId;
		if (string.IsNullOrEmpty(threadId) || !_rowWidgets.TryGetValue(threadId, out var rw) || !IsInstanceValid(rw.Card)) return;

		_rowSwipeTween?.Kill();
		_rowSwipeTween = CreateTween().SetParallel(true).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		_rowSwipeTween.TweenProperty(rw.Card, "offset_left", targetOffset, 0.16f);
		_rowSwipeTween.TweenProperty(rw.Card, "offset_right", targetOffset, 0.16f);
	}

	private void ApplyRowCardOffset(string threadId, float offset)
	{
		if (string.IsNullOrEmpty(threadId) || !_rowWidgets.TryGetValue(threadId, out var rw) || !IsInstanceValid(rw.Card)) return;
		_dragRowCurrentOffset = offset;
		rw.Card.OffsetLeft = offset;
		rw.Card.OffsetRight = offset;
	}

	private bool CanStartHorizontalSwipe(Vector2 globalPos)
	{
		if (_socialModal != null && _socialModal.Visible) return false;
		if (_drawerShell == null || !_drawerShell.GetGlobalRect().HasPoint(globalPos)) return false;
		if (_isListExpanded && _threadsScroll != null && _threadsScroll.IsVisibleInTree() && _threadsScroll.GetGlobalRect().HasPoint(globalPos)) return false;
		bool insideTray = _isStickerTrayOpen && _stickerTrayPanel != null && _stickerTrayPanel.GetGlobalRect().HasPoint(globalPos);
		return !insideTray;
	}

	private void ResetDragGesture()
	{
		_pointerDown = false;
		_stickerTrayTapOutsideCandidate = false;
		_gestureAxis = GestureAxis.None;
		_dragScrollTarget = null;
		_dragAccumDy = 0f;
		_dragRowThreadId = null;
		_dragRowStartOffset = 0f;
		_dragRowCurrentOffset = 0f;
	}

	private void ResetDragClickSuppression()
	{
		_suppressClickAfterDrag = false;
	}

	private ScrollContainer FindScrollAtPosition(Vector2 globalPos)
	{
		if (_socialModal != null && _socialModal.Visible)
		{
			if (_modalContentScroll != null && _modalContentScroll.IsVisibleInTree() && _modalContentScroll.GetGlobalRect().HasPoint(globalPos))
				return _modalContentScroll;
			return null;
		}

		if (_threadsScroll != null && _threadsScroll.IsVisibleInTree() && _threadsScroll.GetGlobalRect().HasPoint(globalPos))
			return _threadsScroll;
		if (_stickerScroll != null && _stickerScroll.IsVisibleInTree() && _stickerScroll.GetGlobalRect().HasPoint(globalPos))
			return _stickerScroll;
		if (_messagesScroll != null && _messagesScroll.IsVisibleInTree() && _messagesScroll.GetGlobalRect().HasPoint(globalPos))
			return _messagesScroll;
		return null;
	}

	private void InitStyles()
	{
		_tileActiveCream = MakeBox(new Color(0.976f, 0.945f, 0.886f, 1f), Colors.Transparent, 0, 12, 12, 12, 12);
		_tileInactivePlum = MakeBox(new Color(0.30f, 0.26f, 0.43f, 0.94f), Colors.Transparent, 0, 12, 12, 12, 12);

		_bubbleOtherLight = MakeBox(new Color(1f, 0.992f, 0.976f, 1f), new Color(0.84f, 0.78f, 0.68f, 0.65f), 1, 13, 22, 16, 12);
		_bubbleSelfTeal = MakeBox(new Color(0.11f, 0.52f, 0.47f, 1f), new Color(0.08f, 0.40f, 0.36f, 0.7f), 1, 13, 22, 16, 12);

		_modalRowCardCream = MakeBox(new Color(0.937f, 0.894f, 0.816f, 1f), new Color(0.83f, 0.76f, 0.65f, 0.75f), 1, 12, 22, 18, 18);
		_stickerTabActiveStyle = MakeBox(new Color(0.28f, 0.38f, 0.64f, 1f), new Color(0.19f, 0.27f, 0.48f, 0.9f), 1, 12, 16, 8, 8);
		_stickerTabIdleStyle = MakeBox(new Color(0.980f, 0.960f, 0.918f, 1f), new Color(0.80f, 0.74f, 0.63f, 0.8f), 1, 12, 16, 8, 8);
		_stickerTileStyle = MakeBox(new Color(0.988f, 0.972f, 0.941f, 1f), new Color(0.85f, 0.79f, 0.69f, 0.65f), 1, 14, 10, 10, 10);
		_quickChatBtnStyle = MakeBox(new Color(0.984f, 0.968f, 0.933f, 1f), new Color(0.76f, 0.69f, 0.57f, 0.8f), 1, 12, 20, 12, 8);

		_btnBlue = MakeBox(new Color(0.24f, 0.48f, 0.84f, 1f), new Color(0.16f, 0.34f, 0.65f, 1f), 2, 10, 18, 12, 8);
		_btnGreen = MakeBox(new Color(0.18f, 0.64f, 0.36f, 1f), new Color(0.12f, 0.46f, 0.25f, 1f), 2, 10, 18, 12, 8);
		_btnRed = MakeBox(new Color(0.82f, 0.24f, 0.24f, 1f), new Color(0.58f, 0.14f, 0.14f, 1f), 2, 10, 18, 12, 8);
		_btnMuted = MakeBox(new Color(0.62f, 0.58f, 0.54f, 1f), new Color(0.48f, 0.44f, 0.40f, 1f), 1, 10, 18, 12, 8);

		_avatarFrame = MakeBox(new Color(0.86f, 0.88f, 0.90f, 1f), new Color(0.64f, 0.58f, 0.50f, 0.65f), 2, 12, 5, 5, 5);
		_badgeStyle = MakeBox(new Color(0.88f, 0.20f, 0.18f, 1f), Colors.Transparent, 0, 14, 7, 4, 1);
	}

	private static StyleBoxFlat MakeBox(Color bg, Color border, int bw, int radius, int padX, int padTop, int padBottom) => new()
	{
		BgColor = bg,
		BorderWidthLeft = bw,
		BorderWidthTop = bw,
		BorderWidthRight = bw,
		BorderWidthBottom = bw,
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

	private static void HideScrollbar(ScrollContainer scroll)
	{
		var vScroll = scroll?.GetVScrollBar();
		if (vScroll == null) return;

		var empty = new StyleBoxEmpty();
		vScroll.CustomMinimumSize = Vector2.Zero;
		vScroll.Modulate = new Color(1, 1, 1, 0);
		vScroll.MouseFilter = MouseFilterEnum.Ignore;
		vScroll.AddThemeStyleboxOverride("scroll", empty);
		vScroll.AddThemeStyleboxOverride("grabber", empty);
		vScroll.AddThemeStyleboxOverride("grabber_highlight", empty);
		vScroll.AddThemeStyleboxOverride("grabber_pressed", empty);
	}

	private void InitSampleData()
	{
		_friends.Clear();
		_friends.AddRange(GameCatalog.SampleFriends);

		_requests.Clear();
		_requests.AddRange(GameCatalog.SampleFriendRequests);

		_threads.Clear();
		_threadOrder.Clear();
		_recentFeed.Clear();

		var friendsChan = new ChatThread
		{
			Id = FriendsChannelId,
			Title = "Bạn Bè",
			Subtitle = "Trò chuyện bạn bè",
			Kind = ThreadKind.FriendsFeed,
			UnreadCount = 2
		};
		friendsChan.Messages.Add(new ChatEntry { Sender = "Nguyễn Trọng Nhân", Text = "Nay làm vài ván Custom thử map mới nhé!", TimeLabel = "21:20" });
		friendsChan.Messages.Add(new ChatEntry { Sender = "Nguyễn Trọng Nhân", StickerId = "capybara_04", TimeLabel = "21:21" });
		friendsChan.Messages.Add(new ChatEntry { Sender = "Trần Minh Khoa", Text = "Mình đang đợi bạn ở sảnh nè!", TimeLabel = "21:22" });
		AddThread(friendsChan);

		var world = new ChatThread
		{
			Id = WorldThreadId,
			Title = "Thế Giới",
			Subtitle = "Kênh chung toàn máy chủ",
			Kind = ThreadKind.World,
			UnreadCount = 3
		};
		world.Messages.Add(new ChatEntry { Sender = "Anh Tèo", Text = "Có ai set kèo Chợ Đầu Mối đánh không?", TimeLabel = "21:14" });
		world.Messages.Add(new ChatEntry { Sender = "Chị Hằng", Text = "Mọi người kết bạn với mình lập team 2v2 nha!", TimeLabel = "21:16" });
		world.Messages.Add(new ChatEntry { Sender = "Hai Lúa Miền Tây", Text = "Hello mọi người, tối nay làm vài ván chợ đêm!", TimeLabel = "21:21" });
		AddThread(world);

		var team = new ChatThread
		{
			Id = TeamThreadId,
			Title = "Tổ Đội",
			Subtitle = "Kênh tổ đội hiện tại",
			Kind = ThreadKind.Party,
			UnreadCount = 1
		};
		team.Messages.Add(new ChatEntry { Sender = "Lê Hoàng Nam", Text = "Sẵn sàng bấm bắt đầu chưa bạn ơi?", TimeLabel = "21:18" });
		AddThread(team);

		for (int i = 0; i < _friends.Count; i++)
		{
			var f = _friends[i];
			var dm = new ChatThread
			{
				Id = f.Id,
				Title = f.Name,
				Subtitle = f.IsOnline ? (f.IsInMatch ? "Đang trong trận" : "Trực tuyến") : "Ngoại tuyến",
				Kind = ThreadKind.DirectMessage,
				UnreadCount = i == 0 ? 1 : 0
			};
			if (i == 0)
				dm.Messages.Add(new ChatEntry { Sender = f.Name, Text = "Lập phòng 2v2 Chợ Bến Thành không?", TimeLabel = "21:23" });
			else if (i == 1)
				dm.Messages.Add(new ChatEntry { Sender = f.Name, Text = "Nhớ chọn 2v2 để mình kéo phụ cho.", TimeLabel = "20:55" });
			AddThread(dm);
		}

		_recentFeed.Add(("[Bạn bè]", "Trần Minh Khoa", "Mình đang đợi bạn ở sảnh nè!"));
		_recentFeed.Add(("[Thế giới]", "Hai Lúa Miền Tây", "Hello mọi người, tối nay làm vài ván chợ đêm!"));
	}

	private void AddThread(ChatThread thread)
	{
		_threads[thread.Id] = thread;
		_threadOrder.Add(thread.Id);
	}

	private void WireSignals()
	{
		if (_backdrop != null)
		{
			_backdrop.GuiInput += (@event) =>
			{
				if (IsPointerPress(@event))
				{
					_backdrop.AcceptEvent();
					CloseDrawer();
				}
			};
		}

		if (_collapseBtn != null) _collapseBtn.Pressed += CloseDrawer;
		if (_toggleStripBtn != null) _toggleStripBtn.Pressed += () => SetListExpanded(!_isListExpanded);

		if (_menuIconBtn != null) _menuIconBtn.Pressed += ToggleMenuDropdown;
		if (_dropdownBackdrop != null)
		{
			_dropdownBackdrop.GuiInput += (@event) =>
			{
				if (IsPointerPress(@event))
				{
					_dropdownBackdrop.AcceptEvent();
					CloseMenuDropdown();
				}
			};
		}

		if (_btnMenuFriends != null) _btnMenuFriends.Pressed += () => { CloseMenuDropdown(); OpenSocialModal(SocialModalMode.FriendsHub); };
		if (_btnMenuAddFriend != null) _btnMenuAddFriend.Pressed += () => { CloseMenuDropdown(); OpenSocialModal(SocialModalMode.AddFriend); };
		if (_btnMenuRequests != null) _btnMenuRequests.Pressed += () => { CloseMenuDropdown(); OpenSocialModal(SocialModalMode.RequestsList); };

		if (_socialModalBackdrop != null)
		{
			_socialModalBackdrop.GuiInput += (@event) =>
			{
				if (IsPointerPress(@event))
				{
					_socialModalBackdrop.AcceptEvent();
					CloseSocialModal();
				}
			};
		}
		if (_closeModalBtn != null) _closeModalBtn.Pressed += CloseSocialModal;
		if (_backModalBtn != null) _backModalBtn.Pressed += () => SwitchSocialModalMode(SocialModalMode.FriendsHub);

		if (_searchBtn != null) _searchBtn.Pressed += RebuildSocialModalBody;
		if (_searchInput != null)
		{
			_searchInput.TextChanged += (_) => RebuildSocialModalBody();
			_searchInput.TextSubmitted += (_) => RebuildSocialModalBody();
		}

		if (_sendBtn != null) _sendBtn.Pressed += OnSendPressed;
		if (_chatInput != null)
		{
			_chatInput.TextChanged += (newText) =>
			{
				if (_threads.TryGetValue(_activeThreadId, out var cur))
				{
					cur.DraftText = newText ?? string.Empty;
				}
			};
			_chatInput.TextSubmitted += (_) => OnSendPressed();
			_chatInput.FocusEntered += () => SetStickerTrayOpen(false);
		}
		if (_emojiBtn != null) _emojiBtn.Pressed += OnEmojiPressed;
		if (_closeStickerTrayBtn != null) _closeStickerTrayBtn.Pressed += () => SetStickerTrayOpen(false);

		if (_cancelDeleteBtn != null) _cancelDeleteBtn.Pressed += CloseDeleteConfirm;
		if (_confirmDeleteBtn != null) _confirmDeleteBtn.Pressed += ConfirmPendingDelete;
		var confirmDim = _confirmDeleteOverlay?.GetNodeOrNull<Control>("Dim");
		if (confirmDim != null)
		{
			confirmDim.GuiInput += (@event) =>
			{
				if (IsPointerPress(@event))
				{
					confirmDim.AcceptEvent();
					CloseDeleteConfirm();
				}
			};
		}
	}

	private static bool IsPointerPress(InputEvent @event)
	{
		bool isClick = @event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left;
		bool isTouch = @event is InputEventScreenTouch st && st.Pressed;
		return isClick || isTouch;
	}

	public (string Line1, string Line2, int TotalUnread) GetPreviewSnapshot()
	{
		string l1 = _recentFeed.Count >= 2
			? $"{_recentFeed[^2].ChannelTag} {_recentFeed[^2].Sender}: {_recentFeed[^2].Text}"
			: (_recentFeed.Count == 1 ? $"{_recentFeed[0].ChannelTag} {_recentFeed[0].Sender}: {_recentFeed[0].Text}" : "");
		string l2 = _recentFeed.Count >= 1
			? $"{_recentFeed[^1].ChannelTag} {_recentFeed[^1].Sender}: {_recentFeed[^1].Text}"
			: "";

		return (l1, l2, GetTotalUnreadAndRequests());
	}

	private int GetTotalUnreadAndRequests()
	{
		int count = _requests.Count;
		foreach (var t in _threads.Values) count += t.UnreadCount;
		return count;
	}

	public void OpenDrawer(string initialThreadId = null)
	{
		if (!string.IsNullOrEmpty(initialThreadId) && _threads.TryGetValue(initialThreadId, out var initThread))
		{
			if (initialThreadId != _activeThreadId) SaveCurrentDraft();
			_activeThreadId = initialThreadId;
			initThread.UnreadCount = 0;
			initThread.IsHiddenInInbox = false;
		}
		else if (_threads.TryGetValue(_activeThreadId, out var cur))
		{
			cur.UnreadCount = 0;
		}

		_swipedThreadId = null;
		ResetDragGesture();
		_scrollVelocity = 0f;
		_inertiaScroll = null;
		CloseMenuDropdown();
		SetStickerTrayOpen(false);
		RefreshAllDrawerUI();
		Visible = true;
		CallDeferred(CanvasItem.MethodName.MoveToFront);
		EmitSignal(SignalName.PreviewUpdated);

		float targetW = _isListExpanded ? DrawerExpandedWidth : DrawerCollapsedWidth;
		if (_drawerShell != null)
		{
			_drawerShell.OffsetLeft = -targetW;
			_drawerShell.OffsetRight = 0f;
		}
		if (_backdrop != null) _backdrop.Modulate = new Color(1, 1, 1, 0);

		_slideTween?.Kill();
		_slideTween = CreateTween().SetParallel(true);
		if (_backdrop != null) _slideTween.TweenProperty(_backdrop, "modulate:a", 1f, 0.20f);
		if (_drawerShell != null)
		{
			_slideTween.TweenProperty(_drawerShell, "offset_left", 0f, 0.26f)
				.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
			_slideTween.TweenProperty(_drawerShell, "offset_right", targetW, 0.26f)
				.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		}
	}

	public void CloseDrawer()
	{
		_swipedThreadId = null;
		ResetDragGesture();
		_scrollVelocity = 0f;
		_inertiaScroll = null;
		SaveCurrentDraft();
		CloseDeleteConfirm();
		CloseMenuDropdown();
		SetStickerTrayOpen(false);
		if (!Visible || _drawerShell == null) return;
		float targetW = _isListExpanded ? DrawerExpandedWidth : DrawerCollapsedWidth;

		_slideTween?.Kill();
		_slideTween = CreateTween().SetParallel(true);
		if (_backdrop != null) _slideTween.TweenProperty(_backdrop, "modulate:a", 0f, 0.18f);
		_slideTween.TweenProperty(_drawerShell, "offset_left", -targetW, 0.22f)
			.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
		_slideTween.TweenProperty(_drawerShell, "offset_right", 0f, 0.22f)
			.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
		_slideTween.Chain().TweenCallback(Callable.From(() => Visible = false));
	}

	public void SetPartyActive(bool isInParty)
	{
		if (_isInParty == isInParty && (!isInParty || _threads.ContainsKey(TeamThreadId))) return;
		_isInParty = isInParty;
		if (!isInParty)
		{
			DisbandPartyConversation();
			return;
		}

		if (!_threads.ContainsKey(TeamThreadId))
		{
			var team = new ChatThread
			{
				Id = TeamThreadId,
				Title = "Tổ Đội",
				Subtitle = "Kênh tổ đội hiện tại",
				Kind = ThreadKind.Party,
				UnreadCount = 0
			};
			_threads[TeamThreadId] = team;
			int worldIdx = _threadOrder.IndexOf(WorldThreadId);
			if (worldIdx >= 0) _threadOrder.Insert(worldIdx + 1, TeamThreadId);
			else _threadOrder.Insert(0, TeamThreadId);
		}
		RefreshAllDrawerUI();
		EmitSignal(SignalName.PreviewUpdated);
	}

	public void DisbandPartyConversation()
	{
		_isInParty = false;
		if (_swipedThreadId == TeamThreadId) _swipedThreadId = null;
		_threads.Remove(TeamThreadId);
		_threadOrder.Remove(TeamThreadId);
		_recentFeed.RemoveAll(item => item.ChannelTag == "[Tổ đội]");

		if (_activeThreadId == TeamThreadId)
		{
			_activeThreadId = FriendsChannelId;
		}
		RefreshAllDrawerUI();
		EmitSignal(SignalName.PreviewUpdated);
	}

	private void SetListExpanded(bool expanded)
	{
		_isListExpanded = expanded;
		_swipedThreadId = null;
		CloseMenuDropdown();

		float colTargetW = expanded ? ExpandedColWidth : StripWidth;
		float drawerTargetW = expanded ? DrawerExpandedWidth : DrawerCollapsedWidth;

		if (_expandedHeader != null) _expandedHeader.Visible = expanded;
		if (_toggleStripIcon != null) _toggleStripIcon.FlipH = expanded;
		RebuildLeftThreadsColumn();

		_expandTween?.Kill();
		_expandTween = CreateTween().SetParallel(true)
			.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		if (_leftThreadsCol != null)
		{
			_expandTween.TweenProperty(_leftThreadsCol, "custom_minimum_size:x", colTargetW, 0.22f);
		}
		if (_drawerShell != null)
		{
			_expandTween.TweenProperty(_drawerShell, "offset_right", drawerTargetW, 0.22f);
		}
	}

	private void SelectThread(string threadId)
	{
		if (_suppressClickAfterDrag) return;
		if (!_threads.TryGetValue(threadId, out var thread)) return;

		bool hadSwipe = !string.IsNullOrEmpty(_swipedThreadId);
		_swipedThreadId = null;
		thread.IsHiddenInInbox = false;

		if (threadId == _activeThreadId)
		{
			if (thread.UnreadCount > 0 || hadSwipe)
			{
				thread.UnreadCount = 0;
				UpdateBadges();
				RebuildLeftThreadsColumn();
				EmitSignal(SignalName.PreviewUpdated);
			}
			return;
		}

		SaveCurrentDraft();
		_activeThreadId = threadId;
		thread.UnreadCount = 0;

		RefreshAllDrawerUI();
		EmitSignal(SignalName.PreviewUpdated);
	}

	private void TogglePinThread(string threadId)
	{
		if (!_threads.TryGetValue(threadId, out var thread) || !thread.CanPin) return;
		thread.IsPinned = !thread.IsPinned;
		thread.PinnedOrder = thread.IsPinned ? ++_pinCounter : 0;
		_swipedThreadId = null;
		RebuildLeftThreadsColumn();
	}

	private void DeleteConversation(string threadId)
	{
		if (!_threads.TryGetValue(threadId, out var thread) || !thread.CanDelete) return;
		thread.Messages.Clear();
		thread.DraftText = string.Empty;
		thread.UnreadCount = 0;
		thread.IsPinned = false;
		thread.PinnedOrder = 0;
		thread.IsHiddenInInbox = true;
		if (_swipedThreadId == threadId) _swipedThreadId = null;

		if (_activeThreadId == threadId)
		{
			_activeThreadId = FriendsChannelId;
		}

		RefreshAllDrawerUI();
		EmitSignal(SignalName.PreviewUpdated);
	}

	private void SaveCurrentDraft()
	{
		if (_chatInput != null && _threads.TryGetValue(_activeThreadId, out var curThread))
		{
			curThread.DraftText = _chatInput.Text ?? string.Empty;
		}
	}

	private void RestoreActiveDraft()
	{
		if (_chatInput == null) return;
		string draft = _threads.TryGetValue(_activeThreadId, out var curThread)
			? (curThread.DraftText ?? string.Empty)
			: string.Empty;
		if (_chatInput.Text != draft)
		{
			_chatInput.Text = draft;
		}
		_chatInput.CaretColumn = _chatInput.Text.Length;
	}

	private void RefreshAllDrawerUI()
	{
		if (_expandedHeader != null) _expandedHeader.Visible = _isListExpanded;
		if (_toggleStripIcon != null) _toggleStripIcon.FlipH = _isListExpanded;

		UpdateBadges();
		RebuildLeftThreadsColumn();
		RefreshActiveChatConversation();
	}

	private void UpdateBadges()
	{
		int totalUnread = 0;
		foreach (var t in _threads.Values) totalUnread += t.UnreadCount;

		if (_totalBadge != null) _totalBadge.Visible = totalUnread > 0;
		if (_totalBadgeLabel != null) _totalBadgeLabel.Text = totalUnread > 99 ? "99+" : totalUnread.ToString();

		if (_reqBadge != null) _reqBadge.Visible = _requests.Count > 0;
		if (_reqBadgeLabel != null) _reqBadgeLabel.Text = _requests.Count.ToString();

		if (_btnMenuRequests != null)
		{
			_btnMenuRequests.Text = _requests.Count > 0 ? $"Lời mời ({_requests.Count})" : "Lời mời";
		}
	}

	private List<string> GetOrderedVisibleThreadIds()
	{
		var visible = new List<ChatThread>();
		foreach (string id in _threadOrder)
		{
			if (!_threads.TryGetValue(id, out var t)) continue;
			if (t.IsHiddenInInbox) continue;
			if (t.Kind == ThreadKind.Party && !_isInParty) continue;
			visible.Add(t);
		}

		var result = new List<string>(visible.Count);

		// Mọi cuộc trò chuyện được ghim (kể cả Thế Giới, Tổ Đội, Bạn bè...) luôn lên top đầu theo thứ tự ghim mới nhất
		var pinnedAll = visible.Where(t => t.IsPinned)
			.OrderByDescending(t => t.PinnedOrder);
		foreach (var t in pinnedAll) result.Add(t.Id);

		foreach (var t in visible)
		{
			if (!t.IsPinned && t.Kind == ThreadKind.FriendsFeed) result.Add(t.Id);
		}
		foreach (var t in visible)
		{
			if (!t.IsPinned && (t.Kind == ThreadKind.World || t.Kind == ThreadKind.Party)) result.Add(t.Id);
		}
		foreach (var t in visible)
		{
			if (!t.IsPinned && t.Kind == ThreadKind.DirectMessage) result.Add(t.Id);
		}
		return result;
	}

	private void RebuildLeftThreadsColumn()
	{
		if (_threadsVBox == null) return;
		_rowWidgets.Clear();
		ClearChildren(_threadsVBox);

		foreach (string id in GetOrderedVisibleThreadIds())
		{
			if (!_threads.TryGetValue(id, out var thread)) continue;
			bool active = _activeThreadId == id;

			if (_isListExpanded)
				_threadsVBox.AddChild(BuildExpandedThreadRow(thread, active));
			else
				_threadsVBox.AddChild(BuildCollapsedAvatarTile(thread, active));
		}
	}

	private Control BuildCollapsedAvatarTile(ChatThread thread, bool active)
	{
		var holder = new Control { CustomMinimumSize = new Vector2(0, 112), MouseFilter = MouseFilterEnum.Pass };
		var card = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
		card.SetAnchorsPreset(LayoutPreset.FullRect);
		card.AddThemeStyleboxOverride("panel", active ? _tileActiveCream : _tileInactivePlum);
		holder.AddChild(card);

		var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
		card.AddChild(center);
		var avatarNode = BuildThreadAvatarNode(thread, 84, active);
		center.AddChild(avatarNode);
		AttachBadgeToAvatarCorner(avatarNode, thread.UnreadCount);

		WrapTileWithClick(holder, card, thread);
		return holder;
	}

	private Control BuildExpandedThreadRow(ChatThread thread, bool active)
	{
		var holder = new Control
		{
			CustomMinimumSize = new Vector2(0, 120),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			ClipContents = true,
			MouseFilter = MouseFilterEnum.Pass
		};

		bool canSwipe = thread.CanPin || thread.CanDelete;
		float revealWidth = (thread.CanPin && thread.CanDelete) ? 162f : (canSwipe ? 84f : 0f);

		if (canSwipe)
		{
			var tray = new HBoxContainer
			{
				AnchorLeft = 0f,
				AnchorTop = 0f,
				AnchorRight = 0f,
				AnchorBottom = 1f,
				OffsetLeft = 6f,
				OffsetRight = revealWidth - 6f,
				Alignment = BoxContainer.AlignmentMode.Center,
				MouseFilter = MouseFilterEnum.Pass
			};
			tray.AddThemeConstantOverride("separation", 8);
			holder.AddChild(tray);

			if (thread.CanPin)
			{
				tray.AddChild(BuildRowActionIconButton(
					GetThreadActionIcon(isPin: true),
					thread.IsPinned ? "Bỏ ghim" : "Ghim lên trên",
					thread.IsPinned ? new Color(1.0f, 0.86f, 0.42f, 1f) : Colors.White,
					() => TogglePinThread(thread.Id)));
			}
			if (thread.CanDelete)
			{
				tray.AddChild(BuildRowActionIconButton(
					GetThreadActionIcon(isPin: false),
					"Xóa cuộc trò chuyện",
					Colors.White,
					() => RequestDeleteConversation(thread.Id)));
			}
		}

		float initialOffset = (_swipedThreadId == thread.Id && canSwipe) ? revealWidth : 0f;
		var card = new PanelContainer
		{
			MouseFilter = MouseFilterEnum.Ignore
		};
		card.SetAnchorsPreset(LayoutPreset.FullRect);
		card.OffsetLeft = initialOffset;
		card.OffsetRight = initialOffset;
		card.AddThemeStyleboxOverride("panel", active ? _tileActiveCream : _tileInactivePlum);
		holder.AddChild(card);

		var hbox = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		hbox.AddThemeConstantOverride("separation", 18);
		card.AddChild(hbox);

		var avatarNode = BuildThreadAvatarNode(thread, 86, active);
		hbox.AddChild(avatarNode);
		AttachBadgeToAvatarCorner(avatarNode, thread.UnreadCount);

		var info = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			MouseFilter = MouseFilterEnum.Ignore
		};
		info.AddThemeConstantOverride("separation", 4);
		hbox.AddChild(info);

		var topRow = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		topRow.AddThemeConstantOverride("separation", 8);
		info.AddChild(topRow);

		Color titleColor = active ? new Color(0.18f, 0.12f, 0.09f, 1f) : new Color(0.98f, 0.96f, 0.92f, 1f);
		Color subColor = active ? new Color(0.42f, 0.32f, 0.24f, 0.95f) : new Color(0.84f, 0.82f, 0.95f, 0.88f);

		var titleLbl = new Label
		{
			Text = thread.Title,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			ClipText = true,
			MouseFilter = MouseFilterEnum.Ignore
		};
		titleLbl.AddThemeFontOverride("font", OrcishFont);
		titleLbl.AddThemeFontSizeOverride("font_size", 36);
		titleLbl.AddThemeColorOverride("font_color", titleColor);
		topRow.AddChild(titleLbl);

		if (thread.IsPinned)
		{
			var pinBadge = GetThreadActionIcon(isPin: true);
			if (pinBadge != null)
			{
				topRow.AddChild(new TextureRect
				{
					Texture = pinBadge,
					CustomMinimumSize = new Vector2(26, 26),
					SizeFlagsVertical = SizeFlags.ShrinkCenter,
					ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
					StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
					MouseFilter = MouseFilterEnum.Ignore
				});
			}
		}

		string lastTime = thread.Messages.Count > 0 ? thread.Messages[^1].TimeLabel : "";
		if (!string.IsNullOrEmpty(lastTime))
		{
			var timeLbl = new Label { Text = lastTime, MouseFilter = MouseFilterEnum.Ignore };
			timeLbl.AddThemeFontOverride("font", OrcishFont);
			timeLbl.AddThemeFontSizeOverride("font_size", 26);
			timeLbl.AddThemeColorOverride("font_color", subColor);
			topRow.AddChild(timeLbl);
		}

		bool hasDraft = !active && !string.IsNullOrWhiteSpace(thread.DraftText);
		string lastMsg = hasDraft
			? $"[Nháp] {thread.DraftText.Trim()}"
			: (thread.Messages.Count > 0 ? $"{thread.Messages[^1].Sender}: {FormatEntryPreviewText(thread.Messages[^1])}" : thread.Subtitle);
		Color draftColor = active ? new Color(0.76f, 0.22f, 0.18f, 1f) : new Color(1.0f, 0.62f, 0.45f, 1f);
		var subLbl = new Label { Text = lastMsg, ClipText = true, MouseFilter = MouseFilterEnum.Ignore };
		subLbl.AddThemeFontOverride("font", OrcishFont);
		subLbl.AddThemeFontSizeOverride("font_size", 28);
		subLbl.AddThemeColorOverride("font_color", hasDraft ? draftColor : subColor);
		info.AddChild(subLbl);

		WrapTileWithClick(card, card, thread);
		_rowWidgets[thread.Id] = (holder, card, revealWidth);
		return holder;
	}

	private Button BuildRowActionIconButton(Texture2D iconTex, string tooltip, Color tint, Action onPressed)
	{
		var btn = new Button
		{
			Flat = true,
			CustomMinimumSize = new Vector2(72, 72),
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			FocusMode = FocusModeEnum.None,
			MouseFilter = MouseFilterEnum.Pass,
			MouseDefaultCursorShape = CursorShape.PointingHand,
			TooltipText = tooltip
		};
		var empty = new StyleBoxEmpty();
		btn.AddThemeStyleboxOverride("normal", empty);
		btn.AddThemeStyleboxOverride("hover", empty);
		btn.AddThemeStyleboxOverride("pressed", empty);
		if (iconTex != null)
		{
			var texRect = new TextureRect
			{
				Texture = iconTex,
				Modulate = tint,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				MouseFilter = MouseFilterEnum.Ignore
			};
			texRect.SetAnchorsPreset(LayoutPreset.FullRect);
			btn.AddChild(texRect);
		}
		btn.Pressed += onPressed;
		UIButtonHelper.AttachBounceEffect(btn);
		return btn;
	}

	private Control BuildThreadAvatarNode(ChatThread thread, int sizePx, bool onLightBg = false)
	{
		if (thread.IsFriend)
		{
			var f = _friends.FirstOrDefault(x => x.Id == thread.Id) ?? new FriendMeta { Id = thread.Id, Name = thread.Title, IsOnline = true };
			return BuildAvatarWithDot(f, sizePx);
		}

		(Texture2D tex, Color bg, Color tint) = thread.Id switch
		{
			FriendsChannelId => (TexSocialGroup, new Color(0.20f, 0.62f, 0.44f, 1f), Colors.White),
			WorldThreadId => (TexWorldIcon, new Color(0.20f, 0.50f, 0.86f, 1f), Colors.White),
			_ => (TexTeamIcon, new Color(0.80f, 0.44f, 0.18f, 1f), Colors.White)
		};

		var box = new PanelContainer
		{
			CustomMinimumSize = new Vector2(sizePx, sizePx),
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			MouseFilter = MouseFilterEnum.Ignore
		};
		Color borderCol = onLightBg ? new Color(0.70f, 0.62f, 0.50f, 0.7f) : new Color(1, 1, 1, 0.32f);
		box.AddThemeStyleboxOverride("panel", MakeBox(bg, borderCol, 2, 14, 12, 12, 12));

		if (tex != null)
		{
			box.AddChild(new TextureRect
			{
				Texture = tex,
				Modulate = tint,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				MouseFilter = MouseFilterEnum.Ignore
			});
		}

		var wrapper = new Control
		{
			CustomMinimumSize = new Vector2(sizePx, sizePx),
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			MouseFilter = MouseFilterEnum.Ignore
		};
		box.SetAnchorsPreset(LayoutPreset.FullRect);
		wrapper.AddChild(box);
		return wrapper;
	}

	private void AttachBadgeToAvatarCorner(Control avatarHolder, int count)
	{
		if (avatarHolder == null || count <= 0) return;
		var badge = CreateUnreadBadge(count);
		badge.SetAnchorsPreset(LayoutPreset.TopRight);
		badge.GrowHorizontal = GrowDirection.Begin;
		badge.OffsetLeft = -22;
		badge.OffsetTop = -6;
		badge.OffsetRight = 6;
		badge.OffsetBottom = 20;
		avatarHolder.AddChild(badge);
	}

	private void WrapTileWithClick(Control host, Control bounceTarget, ChatThread thread)
	{
		string id = thread.Id;
		var btn = new Button
		{
			Flat = true,
			FocusMode = FocusModeEnum.None,
			MouseFilter = MouseFilterEnum.Pass,
			MouseDefaultCursorShape = CursorShape.PointingHand
		};
		btn.SetAnchorsPreset(LayoutPreset.FullRect);
		var empty = new StyleBoxEmpty();
		btn.AddThemeStyleboxOverride("normal", empty);
		btn.AddThemeStyleboxOverride("hover", empty);
		btn.AddThemeStyleboxOverride("pressed", empty);
		btn.Pressed += () => SelectThread(id);
		host.AddChild(btn);
		UIButtonHelper.AttachBounceEffect(btn, targetControl: bounceTarget);
	}

	private void RefreshActiveChatConversation()
	{
		if (!_threads.TryGetValue(_activeThreadId, out var thread)) return;

		if (_headerAvatarHost != null)
		{
			ClearChildren(_headerAvatarHost);
			_headerAvatarHost.AddChild(BuildThreadAvatarNode(thread, 64));
		}

		if (_chatHeaderTitle != null)
		{
			_chatHeaderTitle.Text = thread.IsFriend ? $"{thread.Title} · {thread.Subtitle}" : thread.Title;
		}

		RestoreActiveDraft();

		if (_messagesVBox == null) return;
		ClearChildren(_messagesVBox);

		if (thread.Messages.Count == 0)
		{
			var emptyLbl = new Label
			{
				Text = "Chưa có tin nhắn nào. Hãy gửi lời chào!",
				HorizontalAlignment = HorizontalAlignment.Center,
				MouseFilter = MouseFilterEnum.Ignore
			};
			emptyLbl.AddThemeFontOverride("font", OrcishFont);
			emptyLbl.AddThemeFontSizeOverride("font_size", 36);
			emptyLbl.AddThemeColorOverride("font_color", new Color(0.46f, 0.39f, 0.33f, 0.85f));
			_messagesVBox.AddChild(emptyLbl);
			return;
		}

		foreach (var msg in thread.Messages)
		{
			_messagesVBox.AddChild(BuildChatBubbleRow(msg));
		}

		RequestScrollMessagesToBottom();
	}

	private static string FormatEntryPreviewText(ChatEntry entry)
	{
		if (entry == null) return string.Empty;
		return !string.IsNullOrEmpty(entry.StickerId) ? "[Nhãn dán]" : entry.Text;
	}

	private Control BuildChatBubbleRow(ChatEntry msg)
	{
		var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Pass };
		row.AddThemeConstantOverride("separation", 18);

		if (msg.IsSelf)
		{
			row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });

			var col = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.End, MouseFilter = MouseFilterEnum.Pass };
			col.AddThemeConstantOverride("separation", 6);

			var metaLbl = new Label
			{
				Text = $"{msg.Sender} · {msg.TimeLabel}",
				HorizontalAlignment = HorizontalAlignment.Right,
				MouseFilter = MouseFilterEnum.Ignore
			};
			metaLbl.AddThemeFontOverride("font", OrcishFont);
			metaLbl.AddThemeFontSizeOverride("font_size", 28);
			metaLbl.AddThemeColorOverride("font_color", new Color(0.14f, 0.48f, 0.43f, 1f));
			col.AddChild(metaLbl);
			col.AddChild(BuildChatMessageBody(msg));

			row.AddChild(col);
			return row;
		}

		var avatarFrame = new PanelContainer { CustomMinimumSize = new Vector2(76, 76), SizeFlagsVertical = SizeFlags.ShrinkBegin, MouseFilter = MouseFilterEnum.Ignore };
		avatarFrame.AddThemeStyleboxOverride("panel", _avatarFrame);
		avatarFrame.AddChild(new TextureRect { Texture = TexAvatar, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore });
		row.AddChild(avatarFrame);

		var otherCol = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Pass };
		otherCol.AddThemeConstantOverride("separation", 6);

		var otherMetaLbl = new Label { Text = $"{msg.Sender} · {msg.TimeLabel}", MouseFilter = MouseFilterEnum.Ignore };
		otherMetaLbl.AddThemeFontOverride("font", OrcishFont);
		otherMetaLbl.AddThemeFontSizeOverride("font_size", 28);
		otherMetaLbl.AddThemeColorOverride("font_color", new Color(0.45f, 0.32f, 0.22f, 1f));
		otherCol.AddChild(otherMetaLbl);
		otherCol.AddChild(BuildChatMessageBody(msg));

		row.AddChild(otherCol);
		row.AddChild(new Control { CustomMinimumSize = new Vector2(54, 0), MouseFilter = MouseFilterEnum.Ignore });
		return row;
	}

	private Control BuildChatMessageBody(ChatEntry msg)
	{
		Texture2D stickerTex = GetStickerTexture(msg.StickerId);
		if (stickerTex != null)
		{
			var wrap = new MarginContainer
			{
				SizeFlagsHorizontal = msg.IsSelf ? SizeFlags.ShrinkEnd : SizeFlags.ShrinkBegin,
				MouseFilter = MouseFilterEnum.Ignore
			};
			wrap.AddThemeConstantOverride("margin_top", 4);
			wrap.AddThemeConstantOverride("margin_bottom", 4);
			wrap.AddChild(new TextureRect
			{
				Texture = stickerTex,
				CustomMinimumSize = new Vector2(164, 164),
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				MouseFilter = MouseFilterEnum.Ignore
			});
			return wrap;
		}

		var bubble = new PanelContainer
		{
			SizeFlagsHorizontal = msg.IsSelf ? SizeFlags.ShrinkEnd : SizeFlags.ShrinkBegin,
			MouseFilter = MouseFilterEnum.Ignore
		};
		bubble.AddThemeStyleboxOverride("panel", msg.IsSelf ? _bubbleSelfTeal : _bubbleOtherLight);
		string safeText = msg.Text ?? string.Empty;
		var textLbl = new Label
		{
			Text = safeText,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(Mathf.Min(msg.IsSelf ? 620 : 640, safeText.Length * 20 + 50), 0),
			MouseFilter = MouseFilterEnum.Ignore
		};
		textLbl.AddThemeFontOverride("font", OrcishFont);
		textLbl.AddThemeFontSizeOverride("font_size", 35);
		textLbl.AddThemeColorOverride("font_color", msg.IsSelf ? Colors.White : new Color(0.18f, 0.13f, 0.11f, 1f));
		bubble.AddChild(textLbl);
		return bubble;
	}

	private Texture2D GetThreadActionIcon(bool isPin)
	{
		if (isPin)
		{
			_texThreadPin ??= LoadTextureWithFallback("res://assets/sprites/icons/social/icon_thread_pin.png");
			return _texThreadPin;
		}
		_texThreadDelete ??= LoadTextureWithFallback("res://assets/sprites/icons/social/icon_thread_delete.png");
		return _texThreadDelete;
	}

	private static Texture2D LoadTextureWithFallback(string resPath)
	{
		if (string.IsNullOrEmpty(resPath)) return null;
		Texture2D tex = ResourceLoader.Exists(resPath) ? GD.Load<Texture2D>(resPath) : null;
		if (tex != null) return tex;

		string absPath = ProjectSettings.GlobalizePath(resPath);
		if (System.IO.File.Exists(absPath))
		{
			var img = Image.LoadFromFile(absPath);
			if (img != null) return ImageTexture.CreateFromImage(img);
		}
		return null;
	}

	private Texture2D GetStickerTexture(string stickerId)
	{
		if (string.IsNullOrEmpty(stickerId)) return null;
		if (_stickerTextures.TryGetValue(stickerId, out var cached)) return cached;

		var meta = GameCatalog.FindSticker(stickerId);
		if (meta == null) return null;

		Texture2D tex = LoadTextureWithFallback(meta.TexturePath);
		if (tex != null) _stickerTextures[stickerId] = tex;
		return tex;
	}

	private void OnEmojiPressed()
	{
		SetStickerTrayOpen(!_isStickerTrayOpen);
	}

	private void SetStickerTrayOpen(bool open)
	{
		_isStickerTrayOpen = open;
		if (_stickerTrayPanel == null) return;

		if (open)
		{
			CloseMenuDropdown();
			if (!_stickerTrayBuilt)
			{
				RebuildStickerTabs();
				RebuildStickerGrid();
				_stickerTrayBuilt = true;
			}
			_stickerTrayPanel.Visible = true;
			RequestScrollMessagesToBottom();
		}
		else
		{
			_stickerTrayPanel.Visible = false;
		}
	}

	private void RebuildStickerTabs()
	{
		if (_packTabsHBox == null) return;
		ClearChildren(_packTabsHBox);

		int quickChatIdx = GameCatalog.StickerPacks.Length;
		for (int i = 0; i <= quickChatIdx; i++)
		{
			int tabIdx = i;
			bool active = _activeStickerTabIndex == tabIdx;
			bool isQuickChat = tabIdx == quickChatIdx;

			string title = isQuickChat ? "Chat nhanh" : GameCatalog.StickerPacks[tabIdx].Name;
			Texture2D iconTex = isQuickChat
				? TexWorldIcon
				: GetStickerTexture(GameCatalog.StickerPacks[tabIdx].Items[0].Id);
			Color iconTint = (isQuickChat && !active) ? new Color(0.28f, 0.35f, 0.56f, 1f) : Colors.White;

			var tabBtn = new Button
			{
				CustomMinimumSize = new Vector2(0, 58),
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				FocusMode = FocusModeEnum.None,
				MouseDefaultCursorShape = CursorShape.PointingHand
			};
			Color textCol = active ? new Color(1f, 0.98f, 0.92f, 1f) : new Color(0.24f, 0.18f, 0.14f, 1f);
			ApplyButtonStyle(tabBtn, active ? _stickerTabActiveStyle : _stickerTabIdleStyle, textCol);

			var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
			center.SetAnchorsPreset(LayoutPreset.FullRect);
			tabBtn.AddChild(center);

			var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
			row.AddThemeConstantOverride("separation", 8);
			center.AddChild(row);

			if (iconTex != null)
			{
				row.AddChild(new TextureRect
				{
					Texture = iconTex,
					Modulate = iconTint,
					CustomMinimumSize = new Vector2(isQuickChat ? 36 : 38, isQuickChat ? 36 : 38),
					SizeFlagsVertical = SizeFlags.ShrinkCenter,
					ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
					StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
					MouseFilter = MouseFilterEnum.Ignore
				});
			}

			var lbl = new Label
			{
				Text = title,
				SizeFlagsVertical = SizeFlags.ShrinkCenter,
				MouseFilter = MouseFilterEnum.Ignore
			};
			lbl.AddThemeFontOverride("font", OrcishFont);
			lbl.AddThemeFontSizeOverride("font_size", 28);
			lbl.AddThemeColorOverride("font_color", textCol);
			row.AddChild(lbl);

			tabBtn.Pressed += () =>
			{
				_activeStickerTabIndex = tabIdx;
				RebuildStickerTabs();
				RebuildStickerGrid();
			};
			_packTabsHBox.AddChild(tabBtn);
			UIButtonHelper.AttachBounceEffect(tabBtn);
		}
	}

	private void RebuildStickerGrid()
	{
		if (_stickerGrid == null) return;
		ClearChildren(_stickerGrid);
		if (_stickerScroll != null) _stickerScroll.ScrollVertical = 0;

		if (_activeStickerTabIndex >= GameCatalog.StickerPacks.Length)
		{
			_stickerGrid.Columns = 2;
			foreach (string phrase in GameCatalog.QuickChatPhrases)
			{
				string chosen = phrase;
				var qBtn = new Button
				{
					CustomMinimumSize = new Vector2(0, 72),
					SizeFlagsHorizontal = SizeFlags.ExpandFill,
					FocusMode = FocusModeEnum.None,
					MouseFilter = MouseFilterEnum.Pass,
					MouseDefaultCursorShape = CursorShape.PointingHand,
					ClipText = true,
					Text = chosen
				};
				qBtn.AddThemeFontOverride("font", OrcishFont);
				qBtn.AddThemeFontSizeOverride("font_size", 31);
				ApplyButtonStyle(qBtn, _quickChatBtnStyle, new Color(0.20f, 0.14f, 0.11f, 1f));
				qBtn.Pressed += () =>
				{
					if (_suppressClickAfterDrag) return;
					AppendChatMessage(_activeThreadId, "Bạn", chosen, isSelf: true);
				};
				_stickerGrid.AddChild(qBtn);
				UIButtonHelper.AttachBounceEffect(qBtn);
			}
			return;
		}

		_stickerGrid.Columns = 6;
		var pack = GameCatalog.StickerPacks[_activeStickerTabIndex];
		foreach (var st in pack.Items)
		{
			string stickerId = st.Id;
			var tileBtn = new Button
			{
				CustomMinimumSize = new Vector2(0, 124),
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				FocusMode = FocusModeEnum.None,
				MouseFilter = MouseFilterEnum.Pass,
				MouseDefaultCursorShape = CursorShape.PointingHand,
				TooltipText = st.Label
			};
			ApplyButtonStyle(tileBtn, _stickerTileStyle, Colors.White);

			var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
			margin.SetAnchorsPreset(LayoutPreset.FullRect);
			margin.AddThemeConstantOverride("margin_left", 8);
			margin.AddThemeConstantOverride("margin_top", 8);
			margin.AddThemeConstantOverride("margin_right", 8);
			margin.AddThemeConstantOverride("margin_bottom", 8);
			tileBtn.AddChild(margin);

			margin.AddChild(new TextureRect
			{
				Texture = GetStickerTexture(stickerId),
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				MouseFilter = MouseFilterEnum.Ignore
			});

			tileBtn.Pressed += () =>
			{
				if (_suppressClickAfterDrag) return;
				AppendChatMessage(_activeThreadId, "Bạn", "", isSelf: true, stickerId: stickerId);
			};

			_stickerGrid.AddChild(tileBtn);
			UIButtonHelper.AttachBounceEffect(tileBtn);
		}
	}

	private void OnSendPressed()
	{
		if (_chatInput == null || string.IsNullOrWhiteSpace(_chatInput.Text)) return;
		string txt = _chatInput.Text.Trim();
		_chatInput.Text = string.Empty;
		if (_threads.TryGetValue(_activeThreadId, out var curThread))
		{
			curThread.DraftText = string.Empty;
		}
		AppendChatMessage(_activeThreadId, "Bạn", txt, isSelf: true);
	}

	private void AppendChatMessage(string threadId, string sender, string text, bool isSelf, string stickerId = null)
	{
		if (!_threads.TryGetValue(threadId, out var thread)) return;

		bool wasHidden = thread.IsHiddenInInbox;
		thread.IsHiddenInInbox = false;
		string nowStr = DateTime.Now.ToString("HH:mm");
		var entry = new ChatEntry
		{
			Sender = sender,
			Text = text,
			StickerId = stickerId,
			IsSelf = isSelf,
			TimeLabel = nowStr
		};
		thread.Messages.Add(entry);

		string tag = threadId switch
		{
			FriendsChannelId => "[Bạn bè]",
			WorldThreadId => "[Thế giới]",
			TeamThreadId => "[Tổ đội]",
			_ => $"[{thread.Title}]"
		};
		_recentFeed.Add((tag, sender, FormatEntryPreviewText(entry)));
		if (_recentFeed.Count > 20) _recentFeed.RemoveAt(0);

		if (threadId == _activeThreadId)
		{
			if (_messagesVBox != null && thread.Messages.Count > 1)
			{
				_messagesVBox.AddChild(BuildChatBubbleRow(entry));
				RequestScrollMessagesToBottom();
			}
			else
			{
				RefreshActiveChatConversation();
			}
		}
		if (_isListExpanded || wasHidden) RebuildLeftThreadsColumn();
		EmitSignal(SignalName.PreviewUpdated);
	}

	private void RequestScrollMessagesToBottom()
	{
		_pendingScrollBottomFrames = 3;
		CallDeferred(MethodName.ScrollMessagesToBottom);
	}

	private void OnMessagesScrollBarChanged()
	{
		if (_pendingScrollBottomFrames > 0)
		{
			ScrollMessagesToBottom();
		}
	}

	private void ScrollMessagesToBottom()
	{
		var vBar = _messagesScroll?.GetVScrollBar();
		if (vBar != null) _messagesScroll.ScrollVertical = (int)vBar.MaxValue;
	}

	// ============================================================
	// Dropdown 3 mục góc phải (Bạn bè, Thêm bạn, Lời mời)
	// ============================================================

	private void ToggleMenuDropdown()
	{
		if (_menuDropdown == null) return;
		if (_menuDropdown.Visible)
			CloseMenuDropdown();
		else
		{
			UpdateBadges();
			if (_dropdownBackdrop != null) _dropdownBackdrop.Visible = true;
			_menuDropdown.Visible = true;
			_menuDropdown.MoveToFront();
		}
	}

	private void CloseMenuDropdown()
	{
		if (_dropdownBackdrop != null) _dropdownBackdrop.Visible = false;
		if (_menuDropdown != null) _menuDropdown.Visible = false;
	}

	// ============================================================
	// Popup Bạn Bè / Thêm Bạn / Lời Mời (Không shadow, sạch sẽ)
	// ============================================================

	private void OpenSocialModal(SocialModalMode mode)
	{
		SwitchSocialModalMode(mode);
		if (_socialModal == null) return;

		_socialModal.Visible = true;
		_socialModal.MoveToFront();
		if (_socialModalBackdrop != null) _socialModalBackdrop.Modulate = new Color(1, 1, 1, 0);
		if (_socialDialogBox != null)
		{
			_socialDialogBox.PivotOffset = _socialDialogBox.Size / 2f;
			_socialDialogBox.Scale = new Vector2(0.88f, 0.88f);
			_socialDialogBox.Modulate = new Color(1, 1, 1, 0);
		}

		_modalTween?.Kill();
		_modalTween = CreateTween().SetParallel(true);
		if (_socialModalBackdrop != null) _modalTween.TweenProperty(_socialModalBackdrop, "modulate:a", 1f, 0.16f);
		if (_socialDialogBox != null)
		{
			_modalTween.TweenProperty(_socialDialogBox, "scale", Vector2.One, 0.20f)
				.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
			_modalTween.TweenProperty(_socialDialogBox, "modulate:a", 1f, 0.16f);
		}
	}

	private void CloseSocialModal()
	{
		CloseDeleteConfirm();
		if (_socialModal == null || !_socialModal.Visible) return;

		_modalTween?.Kill();
		_modalTween = CreateTween().SetParallel(true);
		if (_socialModalBackdrop != null) _modalTween.TweenProperty(_socialModalBackdrop, "modulate:a", 0f, 0.14f);
		if (_socialDialogBox != null)
		{
			_socialDialogBox.PivotOffset = _socialDialogBox.Size / 2f;
			_modalTween.TweenProperty(_socialDialogBox, "scale", new Vector2(0.90f, 0.90f), 0.14f);
			_modalTween.TweenProperty(_socialDialogBox, "modulate:a", 0f, 0.14f);
		}
		_modalTween.Chain().TweenCallback(Callable.From(() => _socialModal.Visible = false));
	}

	private void SwitchSocialModalMode(SocialModalMode mode)
	{
		_modalMode = mode;

		if (_modalTitleLabel != null)
		{
			_modalTitleLabel.Text = mode switch
			{
				SocialModalMode.FriendsHub => "Bạn Bè",
				SocialModalMode.FriendsList => "Danh Sách Bạn Bè",
				SocialModalMode.RequestsList => "Yêu Cầu Kết Bạn",
				SocialModalMode.AddFriend => "Tìm Bạn Bè",
				_ => "Bạn Bè"
			};
		}

		if (_backModalBtn != null)
		{
			_backModalBtn.Visible = mode == SocialModalMode.FriendsList || mode == SocialModalMode.RequestsList;
		}
		if (_searchRow != null)
		{
			_searchRow.Visible = mode == SocialModalMode.AddFriend;
		}

		RebuildSocialModalBody();
	}

	private void RebuildSocialModalBody()
	{
		if (_modalContentVBox == null) return;
		ClearChildren(_modalContentVBox);

		switch (_modalMode)
		{
			case SocialModalMode.FriendsHub:
				AddHubMenuRow("Yêu Cầu Kết Bạn", TexSocialRequest, _requests.Count, () => SwitchSocialModalMode(SocialModalMode.RequestsList));
				AddHubMenuRow("Danh Sách Bạn Bè", TexSocialGroup, 0, () => SwitchSocialModalMode(SocialModalMode.FriendsList), $"({_friends.Count})");
				break;

			case SocialModalMode.FriendsList:
				foreach (var friend in _friends)
				{
					AddFriendManageCard(friend);
				}
				break;

			case SocialModalMode.RequestsList:
				if (_requests.Count == 0)
				{
					var emptyLbl = new Label { Text = "Không có lời mời kết bạn nào.", HorizontalAlignment = HorizontalAlignment.Center };
					emptyLbl.AddThemeFontOverride("font", OrcishFont);
					emptyLbl.AddThemeFontSizeOverride("font_size", 36);
					emptyLbl.AddThemeColorOverride("font_color", new Color(0.42f, 0.34f, 0.28f, 0.9f));
					_modalContentVBox.AddChild(emptyLbl);
				}
				else
				{
					foreach (var req in _requests.ToArray())
					{
						AddFriendRequestCard(req);
					}
				}
				break;

			case SocialModalMode.AddFriend:
				string q = (_searchInput?.Text ?? string.Empty).Trim();
				var matches = GameCatalog.SearchablePlayers
					.Where(p => string.IsNullOrEmpty(q) || p.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || p.Id.Contains(q, StringComparison.OrdinalIgnoreCase))
					.ToList();

				if (matches.Count == 0)
				{
					var emptyLbl = new Label { Text = "Không tìm thấy người chơi phù hợp.", HorizontalAlignment = HorizontalAlignment.Center };
					emptyLbl.AddThemeFontOverride("font", OrcishFont);
					emptyLbl.AddThemeFontSizeOverride("font_size", 36);
					emptyLbl.AddThemeColorOverride("font_color", new Color(0.42f, 0.34f, 0.28f, 0.9f));
					_modalContentVBox.AddChild(emptyLbl);
				}
				else
				{
					foreach (var player in matches)
					{
						AddFindPlayerCard(player);
					}
				}
				break;
		}
	}

	private void AddHubMenuRow(string title, Texture2D iconTex, int badgeCount, Action onPressed, string suffix = "")
	{
		var holder = new Control { CustomMinimumSize = new Vector2(0, 108), SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Pass };
		var card = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
		card.SetAnchorsPreset(LayoutPreset.FullRect);
		card.AddThemeStyleboxOverride("panel", _modalRowCardCream);
		holder.AddChild(card);

		var hbox = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		hbox.AddThemeConstantOverride("separation", 22);
		card.AddChild(hbox);

		if (iconTex != null)
		{
			var icon = new TextureRect
			{
				Texture = iconTex,
				CustomMinimumSize = new Vector2(56, 56),
				Modulate = new Color(0.28f, 0.35f, 0.56f, 1f),
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				SizeFlagsVertical = SizeFlags.ShrinkCenter,
				MouseFilter = MouseFilterEnum.Ignore
			};
			hbox.AddChild(icon);
		}

		string display = string.IsNullOrEmpty(suffix) ? title : $"{title} {suffix}";
		var lbl = new Label { Text = display, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore };
		lbl.AddThemeFontOverride("font", OrcishFont);
		lbl.AddThemeFontSizeOverride("font_size", 38);
		lbl.AddThemeColorOverride("font_color", new Color(0.22f, 0.16f, 0.12f, 1f));
		hbox.AddChild(lbl);

		if (badgeCount > 0)
		{
			var badge = CreateUnreadBadge(badgeCount);
			badge.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			hbox.AddChild(badge);
		}

		if (TexChevronLeft != null)
		{
			var arrow = new TextureRect
			{
				Texture = TexChevronLeft,
				FlipH = true,
				CustomMinimumSize = new Vector2(30, 30),
				Modulate = new Color(0.45f, 0.36f, 0.28f, 0.85f),
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				SizeFlagsVertical = SizeFlags.ShrinkCenter,
				MouseFilter = MouseFilterEnum.Ignore
			};
			hbox.AddChild(arrow);
		}

		var btn = new Button { Flat = true, FocusMode = FocusModeEnum.None, MouseFilter = MouseFilterEnum.Pass, MouseDefaultCursorShape = CursorShape.PointingHand };
		btn.SetAnchorsPreset(LayoutPreset.FullRect);
		var empty = new StyleBoxEmpty();
		btn.AddThemeStyleboxOverride("normal", empty);
		btn.AddThemeStyleboxOverride("hover", empty);
		btn.AddThemeStyleboxOverride("pressed", empty);
		btn.Pressed += () =>
		{
			if (_suppressClickAfterDrag) return;
			onPressed();
		};
		holder.AddChild(btn);
		UIButtonHelper.AttachBounceEffect(btn, targetControl: holder);

		_modalContentVBox.AddChild(holder);
	}

	private void AddFriendManageCard(FriendMeta friend)
	{
		var (card, row) = CreateFriendCardScaffold(friend);

		var chatBtn = CreatePillButton("Nhắn tin", _btnBlue, Colors.White, 170);
		chatBtn.Pressed += () =>
		{
			if (_suppressClickAfterDrag) return;
			CloseSocialModal();
			SelectThread(friend.Id);
		};
		row.AddChild(chatBtn);

		var delBtn = CreatePillButton("Xóa", _btnRed, Colors.White, 116);
		delBtn.Pressed += () =>
		{
			if (_suppressClickAfterDrag) return;
			RequestDeleteFriend(friend);
		};
		row.AddChild(delBtn);

		_modalContentVBox.AddChild(card);
	}

	private void AddFindPlayerCard(FriendMeta player)
	{
		var (card, row) = CreateFriendCardScaffold(player);
		bool alreadyFriend = _friends.Any(f => f.Id == player.Id);

		var addBtn = CreatePillButton(alreadyFriend ? "Đã kết bạn" : "Kết bạn", alreadyFriend ? _btnMuted : _btnGreen, Colors.White, 194);
		addBtn.Disabled = alreadyFriend;
		addBtn.Pressed += () =>
		{
			if (_suppressClickAfterDrag) return;
			if (!_friends.Any(f => f.Id == player.Id))
			{
				_friends.Insert(0, player);
				AddThread(new ChatThread { Id = player.Id, Title = player.Name, Subtitle = player.IsOnline ? "Trực tuyến" : "Ngoại tuyến", Kind = ThreadKind.DirectMessage });
			}
			RefreshAllDrawerUI();
			RebuildSocialModalBody();
		};
		row.AddChild(addBtn);

		_modalContentVBox.AddChild(card);
	}

	private void AddFriendRequestCard(FriendMeta req)
	{
		var (card, row) = CreateFriendCardScaffold(req);

		var acceptBtn = CreatePillButton("Đồng ý", _btnGreen, Colors.White, 156);
		acceptBtn.Pressed += () =>
		{
			if (_suppressClickAfterDrag) return;
			_requests.RemoveAll(r => r.Id == req.Id);
			if (!_friends.Any(f => f.Id == req.Id))
			{
				_friends.Insert(0, req);
				AddThread(new ChatThread { Id = req.Id, Title = req.Name, Subtitle = req.IsOnline ? "Trực tuyến" : "Ngoại tuyến", Kind = ThreadKind.DirectMessage });
			}
			RefreshAllDrawerUI();
			RebuildSocialModalBody();
			EmitSignal(SignalName.PreviewUpdated);
		};
		row.AddChild(acceptBtn);

		var declineBtn = CreatePillButton("Từ chối", _btnRed, Colors.White, 156);
		declineBtn.Pressed += () =>
		{
			if (_suppressClickAfterDrag) return;
			_requests.RemoveAll(r => r.Id == req.Id);
			RefreshAllDrawerUI();
			RebuildSocialModalBody();
			EmitSignal(SignalName.PreviewUpdated);
		};
		row.AddChild(declineBtn);

		_modalContentVBox.AddChild(card);
	}

	private (PanelContainer Card, HBoxContainer Row) CreateFriendCardScaffold(FriendMeta friend)
	{
		var card = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Pass };
		card.AddThemeStyleboxOverride("panel", _modalRowCardCream);

		var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Pass };
		row.AddThemeConstantOverride("separation", 20);
		card.AddChild(row);

		row.AddChild(BuildAvatarWithDot(friend, 84));

		var info = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore };
		info.AddThemeConstantOverride("separation", 4);
		row.AddChild(info);

		var nameLbl = new Label { Text = friend.Name, MouseFilter = MouseFilterEnum.Ignore };
		nameLbl.AddThemeFontOverride("font", OrcishFont);
		nameLbl.AddThemeFontSizeOverride("font_size", 36);
		nameLbl.AddThemeColorOverride("font_color", new Color(0.18f, 0.13f, 0.11f, 1f));
		info.AddChild(nameLbl);

		Color stColor = !friend.IsOnline ? new Color(0.45f, 0.42f, 0.40f, 1f) : friend.IsInMatch ? new Color(0.76f, 0.46f, 0.06f, 1f) : new Color(0.12f, 0.54f, 0.28f, 1f);
		string stTxt = !friend.IsOnline ? "Ngoại tuyến" : friend.IsInMatch ? "Đang trong trận" : "Trực tuyến";
		var subLbl = new Label { Text = $"ID: {friend.Id} · {stTxt}", MouseFilter = MouseFilterEnum.Ignore };
		subLbl.AddThemeFontOverride("font", OrcishFont);
		subLbl.AddThemeFontSizeOverride("font_size", 28);
		subLbl.AddThemeColorOverride("font_color", stColor);
		info.AddChild(subLbl);

		return (card, row);
	}

	private Control BuildAvatarWithDot(FriendMeta friend, int sizePx)
	{
		var holder = new Control { CustomMinimumSize = new Vector2(sizePx, sizePx), SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore };
		var frame = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
		frame.SetAnchorsPreset(LayoutPreset.FullRect);
		frame.AddThemeStyleboxOverride("panel", _avatarFrame);
		holder.AddChild(frame);

		var tex = new TextureRect
		{
			Texture = TexAvatar,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			Modulate = friend.IsOnline ? Colors.White : new Color(0.62f, 0.62f, 0.70f, 0.85f),
			MouseFilter = MouseFilterEnum.Ignore
		};
		frame.AddChild(tex);

		Color dotCol = !friend.IsOnline ? new Color(0.54f, 0.56f, 0.64f, 1f) : friend.IsInMatch ? new Color(0.98f, 0.72f, 0.22f, 1f) : new Color(0.24f, 0.88f, 0.48f, 1f);
		int dotSize = Mathf.Max(18, sizePx / 4);
		var dot = new Panel
		{
			Position = new Vector2(sizePx - dotSize + 2, sizePx - dotSize + 2),
			Size = new Vector2(dotSize, dotSize),
			MouseFilter = MouseFilterEnum.Ignore
		};
		dot.AddThemeStyleboxOverride("panel", MakeBox(dotCol, Colors.White, 2, dotSize / 2, 0, 0, 0));
		holder.AddChild(dot);

		return holder;
	}

	private Button CreatePillButton(string text, StyleBoxFlat style, Color fontColor, int minWidth)
	{
		var btn = new Button
		{
			CustomMinimumSize = new Vector2(minWidth, 62),
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			FocusMode = FocusModeEnum.None,
			MouseFilter = MouseFilterEnum.Pass,
			MouseDefaultCursorShape = CursorShape.PointingHand,
			Text = text
		};
		btn.AddThemeFontOverride("font", OrcishFont);
		btn.AddThemeFontSizeOverride("font_size", 31);
		ApplyButtonStyle(btn, style, fontColor);
		UIButtonHelper.AttachBounceEffect(btn);
		return btn;
	}

	private PanelContainer CreateUnreadBadge(int count)
	{
		var badge = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
		badge.AddThemeStyleboxOverride("panel", _badgeStyle);
		var lbl = new Label
		{
			Text = count > 99 ? "99+" : count.ToString(),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore
		};
		lbl.AddThemeFontOverride("font", OrcishFont);
		lbl.AddThemeFontSizeOverride("font_size", 28);
		lbl.AddThemeColorOverride("font_color", Colors.White);
		badge.AddChild(lbl);
		return badge;
	}

	private void RequestDeleteFriend(FriendMeta friend)
	{
		if (friend == null) return;
		ShowDeleteConfirm(friend.Id, $"Xóa {friend.Name} khỏi danh sách bạn bè?", isConversation: false);
	}

	private void RequestDeleteConversation(string threadId)
	{
		if (!_threads.TryGetValue(threadId, out var thread) || !thread.CanDelete) return;
		ShowDeleteConfirm(thread.Id, $"Xóa cuộc trò chuyện với {thread.Title}?", isConversation: true);
	}

	private void ShowDeleteConfirm(string targetId, string message, bool isConversation)
	{
		_pendingDeleteTargetId = targetId;
		_pendingDeleteIsConversation = isConversation;
		if (_confirmDeleteLabel != null)
			_confirmDeleteLabel.Text = message;
		if (_confirmDeleteOverlay != null)
		{
			_confirmDeleteOverlay.Visible = true;
			_confirmDeleteOverlay.MoveToFront();
		}
	}

	private void ConfirmPendingDelete()
	{
		string targetId = _pendingDeleteTargetId;
		bool isConversation = _pendingDeleteIsConversation;
		CloseDeleteConfirm();
		if (string.IsNullOrEmpty(targetId)) return;

		if (isConversation)
		{
			DeleteConversation(targetId);
			return;
		}

		_friends.RemoveAll(f => f.Id == targetId);
		_threads.Remove(targetId);
		_threadOrder.Remove(targetId);
		if (_activeThreadId == targetId) _activeThreadId = FriendsChannelId;
		RefreshAllDrawerUI();
		RebuildSocialModalBody();
		EmitSignal(SignalName.PreviewUpdated);
	}

	private void CloseDeleteConfirm()
	{
		_pendingDeleteTargetId = null;
		_pendingDeleteIsConversation = false;
		if (_confirmDeleteOverlay != null) _confirmDeleteOverlay.Visible = false;
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

	private static void ClearChildren(Node parent)
	{
		if (parent == null) return;
		foreach (Node child in parent.GetChildren())
		{
			parent.RemoveChild(child);
			child.QueueFree();
		}
	}
}
