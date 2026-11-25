using System.Collections.Generic;
using Godot;

namespace MarketMobile.UI;

public partial class FriendChatDrawer : Control
{
    [Export] private PackedScene _chatFriendItemScene;

    private Control _drawer;
    private VBoxContainer _friendsVBox;
    private Control _conversationArea;
    private Label _partnerNameLabel;
    private ScrollContainer _messagesScroll;
    private RichTextLabel _messagesLabel;
    private LineEdit _chatInput;

    private string _activeFriendId;
    private Tween _drawerTween;
    private Tween _fadeTween;

    private const float DrawerWidth = 1100f;

    private readonly List<ChatFriendItem> _rows = new();
    private readonly Dictionary<string, List<string>> _chatHistory = new();

    public override void _Ready()
    {
        _chatFriendItemScene ??= GD.Load<PackedScene>("res://prefabs/ui/chat_friend_item.tscn");

        _drawer = GetNodeOrNull<Control>("ChatDrawer");
        _friendsVBox = GetNodeOrNull<VBoxContainer>("ChatDrawer/ChatFriendsScroll/ChatFriendsVBox");
        _conversationArea = GetNodeOrNull<Control>("ChatDrawer/ConversationArea");
        _partnerNameLabel = GetNodeOrNull<Label>("ChatDrawer/ConversationArea/PartnerHeaderHBox/PartnerNameLabel");
        _messagesScroll = GetNodeOrNull<ScrollContainer>("ChatDrawer/ConversationArea/MessagesScroll");
        _messagesLabel = GetNodeOrNull<RichTextLabel>("ChatDrawer/ConversationArea/MessagesScroll/MessagesLabel");
        _chatInput = GetNodeOrNull<LineEdit>("ChatDrawer/InputHBox/ChatInput");

        var backdrop = GetNodeOrNull<Control>("Backdrop");
        if (backdrop != null) backdrop.GuiInput += OnBackdropInput;

        var collapseBtn = GetNodeOrNull<Button>("ChatDrawer/CollapseBtn");
        if (collapseBtn != null) collapseBtn.Pressed += Close;

        var sendBtn = GetNodeOrNull<Button>("ChatDrawer/InputHBox/SendBtn");
        if (sendBtn != null) sendBtn.Pressed += OnSendPressed;

        if (_chatInput != null) _chatInput.TextSubmitted += (_) => OnSendPressed();

        var emojiBtn = GetNodeOrNull<TextureButton>("ChatDrawer/InputHBox/EmojiBtn");
        if (emojiBtn != null) emojiBtn.Pressed += OnEmojiPressed;

        Visible = false;
    }

    public void Open(IReadOnlyList<(string Id, string Name)> friends, string selectedId, string selectedName)
    {
        RebuildFriendList(friends);
        SelectFriend(selectedId, selectedName, false);

        if (_drawer == null) return;
        bool wasVisible = Visible;
        Visible = true;

        if (!wasVisible)
        {
            _drawerTween?.Kill();
            _drawer.OffsetLeft = -DrawerWidth;
            _drawer.OffsetRight = 0f;

            _drawerTween = CreateTween().SetParallel(true)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.Out);
            _drawerTween.TweenProperty(_drawer, "offset_left", 0f, 0.24f);
            _drawerTween.TweenProperty(_drawer, "offset_right", DrawerWidth, 0.24f);
        }

        _chatInput?.GrabFocus();
    }

    public void Close()
    {
        if (_drawer == null) return;

        _drawerTween?.Kill();
        _drawerTween = CreateTween().SetParallel(true)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.In);
        _drawerTween.TweenProperty(_drawer, "offset_left", -DrawerWidth, 0.2f);
        _drawerTween.TweenProperty(_drawer, "offset_right", 0f, 0.2f);
        _drawerTween.Chain().TweenCallback(Callable.From(() => Visible = false));
    }

    private void RebuildFriendList(IReadOnlyList<(string Id, string Name)> friends)
    {
        if (_friendsVBox == null || _chatFriendItemScene == null) return;

        _rows.Clear();
        foreach (Node child in _friendsVBox.GetChildren())
            child.QueueFree();

        foreach (var (id, name) in friends)
        {
            var item = _chatFriendItemScene.Instantiate<ChatFriendItem>();
            _friendsVBox.AddChild(item);
            item.Setup(id, name);
            item.Selected += (fId, fName) => SelectFriend(fId, fName, true);
            _rows.Add(item);
        }
    }

    private void SelectFriend(string friendId, string friendName, bool animate)
    {
        if (animate && friendId == _activeFriendId) return;

        _activeFriendId = friendId;
        if (!_chatHistory.ContainsKey(friendId))
            _chatHistory[friendId] = new List<string>();

        if (_partnerNameLabel != null)
            _partnerNameLabel.Text = friendName;

        foreach (var row in _rows)
            row.SetSelected(row.FriendId == _activeFriendId, animate);

        RefreshMessages();

        if (animate && _conversationArea != null)
        {
            _fadeTween?.Kill();
            _conversationArea.Modulate = new Color(1, 1, 1, 0.25f);
            _fadeTween = CreateTween().SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            _fadeTween.TweenProperty(_conversationArea, "modulate:a", 1.0f, 0.16f);
        }
    }

    private void OnBackdropInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton btn && btn.Pressed && btn.ButtonIndex == MouseButton.Left)
            Close();
    }

    private void OnEmojiPressed()
    {
        if (_chatInput == null) return;
        _chatInput.Text += "😊";
        _chatInput.GrabFocus();
        _chatInput.CaretColumn = _chatInput.Text.Length;
    }

    private void OnSendPressed()
    {
        if (_chatInput == null || string.IsNullOrWhiteSpace(_chatInput.Text) || string.IsNullOrEmpty(_activeFriendId))
            return;

        if (!_chatHistory.ContainsKey(_activeFriendId))
            _chatHistory[_activeFriendId] = new List<string>();

        _chatHistory[_activeFriendId].Add($"[b]Bạn:[/b] {_chatInput.Text.Trim()}");
        _chatInput.Text = string.Empty;
        RefreshMessages();
    }

    private void RefreshMessages()
    {
        if (_messagesLabel == null || string.IsNullOrEmpty(_activeFriendId)) return;

        _messagesLabel.Text = _chatHistory.TryGetValue(_activeFriendId, out var lines)
            ? string.Join("\n", lines)
            : string.Empty;

        if (_messagesScroll != null)
            CallDeferred(MethodName.ScrollToBottom);
    }

    private void ScrollToBottom()
    {
        var vBar = _messagesScroll?.GetVScrollBar();
        if (vBar != null)
            _messagesScroll.ScrollVertical = (int)vBar.MaxValue;
    }
}
