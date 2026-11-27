using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MarketMobile.Core;

namespace MarketMobile.UI;

public partial class FriendsScreen : Control
{
    [Export] private PackedScene _friendItemScene;
    [Export] private PackedScene _findFriendItemScene;
    [Export] private PackedScene _friendRequestItemScene;

    private ColorRect _slidingHighlight;
    private ShaderMaterial _slidingHighlightMat;
    private ColorRect[] _tabHoverBgs;
    private Label[] _tabLabels;
    private Control[] _tabPanels;

    private Control _requestBadge;
    private Label _requestBadgeLabel;

    private VBoxContainer _friendsVBox;
    private Label _emptyLabel;

    private LineEdit _searchInput;
    private VBoxContainer _resultVBox;
    private Label _findEmptyLabel;

    private VBoxContainer _requestsVBox;
    private Label _requestsEmptyLabel;

    private Control _confirmDeleteOverlay;
    private Label _confirmDeleteLabel;
    private FriendChatDrawer _chatDrawer;

    private Tween _tabSlideTween;
    private Tween _tabSweepTween;
    private Tween _panelFadeTween;

    private int _currentTab;
    private string _pendingDeleteId;

    private const float TabStepY = 134f; // TabHeight (118) + Separation (16)

    private readonly List<(string Id, string Name)> _friends = GameCatalog.SampleFriends
        .Select(f => (f.Id, f.Name))
        .ToList();

    private readonly List<(string Id, string Name)> _searchablePlayers = GameCatalog.SearchablePlayers
        .Select(p => (p.Id, p.Name))
        .ToList();

    private readonly List<(string Id, string Name)> _friendRequests = GameCatalog.SampleFriendRequests
        .Select(r => (r.Id, r.Name))
        .ToList();

    public override void _Ready()
    {
        _friendItemScene ??= GD.Load<PackedScene>("res://prefabs/ui/friend_item.tscn");
        _findFriendItemScene ??= GD.Load<PackedScene>("res://prefabs/ui/find_friend_item.tscn");
        _friendRequestItemScene ??= GD.Load<PackedScene>("res://prefabs/ui/friend_request_item.tscn");

        _slidingHighlight = GetNodeOrNull<ColorRect>("LeftTabsArea/SlidingHighlight");
        _slidingHighlightMat = _slidingHighlight?.Material as ShaderMaterial;

        string[] tabNodes = { "TabFriends", "TabFind", "TabRequests" };
        _tabHoverBgs = tabNodes.Select(n => GetNodeOrNull<ColorRect>($"LeftTabsArea/LeftTabsVBox/{n}/HoverBg")).ToArray();
        _tabLabels = tabNodes.Select(n => GetNodeOrNull<Label>($"LeftTabsArea/LeftTabsVBox/{n}/Label")).ToArray();
        _tabPanels = new[]
        {
            GetNodeOrNull<Control>("RightContent/FriendsListPanel"),
            GetNodeOrNull<Control>("RightContent/FindFriendPanel"),
            GetNodeOrNull<Control>("RightContent/RequestsPanel")
        };

        _requestBadge = GetNodeOrNull<Control>("LeftTabsArea/LeftTabsVBox/TabRequests/Badge");
        _requestBadgeLabel = GetNodeOrNull<Label>("LeftTabsArea/LeftTabsVBox/TabRequests/Badge/CountLabel");

        _friendsVBox = GetNodeOrNull<VBoxContainer>("RightContent/FriendsListPanel/FriendsScroll/ContentMargin/FriendsVBox");
        _emptyLabel = GetNodeOrNull<Label>("RightContent/FriendsListPanel/EmptyLabel");

        _searchInput = GetNodeOrNull<LineEdit>("RightContent/FindFriendPanel/SearchHBox/SearchInput");
        _resultVBox = GetNodeOrNull<VBoxContainer>("RightContent/FindFriendPanel/ResultScroll/ContentMargin/ResultVBox");
        _findEmptyLabel = GetNodeOrNull<Label>("RightContent/FindFriendPanel/FindEmptyLabel");

        _requestsVBox = GetNodeOrNull<VBoxContainer>("RightContent/RequestsPanel/RequestsScroll/ContentMargin/RequestsVBox");
        _requestsEmptyLabel = GetNodeOrNull<Label>("RightContent/RequestsPanel/RequestsEmptyLabel");

        _confirmDeleteOverlay = GetNodeOrNull<Control>("ConfirmDeleteOverlay");
        _confirmDeleteLabel = GetNodeOrNull<Label>("ConfirmDeleteOverlay/DialogPanel/VBox/MessageLabel");
        _chatDrawer = GetNodeOrNull<FriendChatDrawer>("ChatOverlay");

        var backBtn = GetNodeOrNull<TextureButton>("HeaderHBox/BackBtn");
        if (backBtn != null)
            backBtn.Pressed += () => GetTree().ChangeSceneToFile("res://scenes/ui/lobby_screen.tscn");

        for (int i = 0; i < tabNodes.Length; i++)
        {
            int idx = i;
            var btn = GetNodeOrNull<Button>($"LeftTabsArea/LeftTabsVBox/{tabNodes[i]}/ClickBtn");
            if (btn == null) continue;
            btn.Pressed += () => SwitchTab(idx, true);
            btn.MouseEntered += () => AnimateHoverBg(_tabHoverBgs[idx], _currentTab != idx);
            btn.MouseExited += () => AnimateHoverBg(_tabHoverBgs[idx], false);
        }

        var searchBtn = GetNodeOrNull<Button>("RightContent/FindFriendPanel/SearchHBox/SearchBtn");
        if (searchBtn != null) searchBtn.Pressed += RefreshSearch;
        if (_searchInput != null) _searchInput.TextSubmitted += (_) => RefreshSearch();

        var cancelDelBtn = GetNodeOrNull<Button>("ConfirmDeleteOverlay/DialogPanel/VBox/ButtonsHBox/CancelBtn");
        var confirmDelBtn = GetNodeOrNull<Button>("ConfirmDeleteOverlay/DialogPanel/VBox/ButtonsHBox/ConfirmBtn");
        if (cancelDelBtn != null) cancelDelBtn.Pressed += CloseDeleteDialog;
        if (confirmDelBtn != null) confirmDelBtn.Pressed += OnConfirmDeletePressed;

        if (_confirmDeleteOverlay != null) _confirmDeleteOverlay.Visible = false;

        SwitchTab(0, false);
        RebuildFriendsList();
        RefreshSearch();
        RebuildRequestsList();
    }

    private void AnimateHoverBg(ColorRect hoverBg, bool show)
    {
        if (hoverBg == null) return;
        var tween = CreateTween().SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(hoverBg, "color:a", show ? 0.10f : 0.0f, 0.14f);
    }

    private void SwitchTab(int tabIndex, bool animate)
    {
        if (animate && tabIndex == _currentTab) return;

        _currentTab = tabIndex;

        for (int i = 0; i < _tabHoverBgs.Length; i++)
            AnimateHoverBg(_tabHoverBgs[i], false);

        float targetY = tabIndex * TabStepY;
        if (_slidingHighlight != null)
        {
            _tabSlideTween?.Kill();
            if (!animate)
            {
                _slidingHighlight.Position = new Vector2(0f, targetY);
            }
            else
            {
                _tabSlideTween = CreateTween().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
                _tabSlideTween.TweenProperty(_slidingHighlight, "position:y", targetY, 0.22f);
                TriggerTabSweep();
            }
        }

        for (int i = 0; i < _tabLabels.Length; i++)
            ApplyTabLabelState(_tabLabels[i], i == tabIndex, animate);

        for (int i = 0; i < _tabPanels.Length; i++)
        {
            var panel = _tabPanels[i];
            if (panel == null) continue;

            bool isActive = i == tabIndex;
            panel.Visible = isActive;
            if (!isActive) continue;

            if (animate)
            {
                _panelFadeTween?.Kill();
                panel.Modulate = new Color(1, 1, 1, 0);
                _panelFadeTween = CreateTween().SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
                _panelFadeTween.TweenProperty(panel, "modulate:a", 1.0f, 0.18f);
            }
            else
            {
                panel.Modulate = Colors.White;
            }
        }

        if (tabIndex == 1) RefreshSearch();
        else if (tabIndex == 2) RebuildRequestsList();
    }

    private void TriggerTabSweep()
    {
        if (_slidingHighlightMat == null) return;
        _tabSweepTween?.Kill();
        _slidingHighlightMat.SetShaderParameter("sweep_pos", -0.35f);
        _tabSweepTween = CreateTween().SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        _tabSweepTween.TweenMethod(
            Callable.From<float>(v => _slidingHighlightMat.SetShaderParameter("sweep_pos", v)),
            -0.35f,
            1.35f,
            0.38f
        );
    }

    private void ApplyTabLabelState(Label label, bool isActive, bool animate)
    {
        if (label == null) return;

        float alpha = isActive ? 1.0f : 0.72f;
        float offsetX = isActive ? 8f : 0f;
        Vector2 scale = isActive ? new Vector2(1.05f, 1.05f) : Vector2.One;

        if (!animate)
        {
            label.Modulate = new Color(1, 1, 1, alpha);
            label.OffsetLeft = offsetX;
            label.OffsetRight = offsetX;
            label.Scale = scale;
            return;
        }

        var tween = CreateTween().SetParallel(true).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(label, "modulate:a", alpha, 0.20f);
        tween.TweenProperty(label, "offset_left", offsetX, 0.20f);
        tween.TweenProperty(label, "offset_right", offsetX, 0.20f);
        tween.TweenProperty(label, "scale", scale, 0.20f);
    }

    private void RebuildFriendsList()
    {
        ClearChildren(_friendsVBox);
        if (_emptyLabel != null) _emptyLabel.Visible = _friends.Count == 0;
        if (_friendsVBox == null || _friendItemScene == null) return;

        foreach (var (id, name) in _friends)
        {
            var item = _friendItemScene.Instantiate<FriendItem>();
            _friendsVBox.AddChild(item);
            item.Setup(id, name);
            item.ChatRequested += (fId, fName) => _chatDrawer?.Open(_friends, fId, fName);
            item.DeleteRequested += OnFriendDeleteRequested;
        }
    }

    private void RefreshSearch()
    {
        ClearChildren(_resultVBox);
        string query = (_searchInput?.Text ?? string.Empty).Trim();

        var matches = _searchablePlayers.Where(p =>
            string.IsNullOrEmpty(query) ||
            p.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            p.Id.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

        if (_findEmptyLabel != null) _findEmptyLabel.Visible = matches.Count == 0;
        if (_resultVBox == null || _findFriendItemScene == null) return;

        foreach (var (id, name) in matches)
        {
            var item = _findFriendItemScene.Instantiate<FindFriendItem>();
            _resultVBox.AddChild(item);
            item.Setup(id, name, _friends.Any(f => f.Id == id));
            item.AddFriendRequested += OnAddFriendRequested;
        }
    }

    private void RebuildRequestsList()
    {
        ClearChildren(_requestsVBox);
        UpdateRequestBadge();

        if (_requestsEmptyLabel != null) _requestsEmptyLabel.Visible = _friendRequests.Count == 0;
        if (_requestsVBox == null || _friendRequestItemScene == null) return;

        foreach (var (id, name) in _friendRequests)
        {
            var item = _friendRequestItemScene.Instantiate<FriendRequestItem>();
            _requestsVBox.AddChild(item);
            item.Setup(id, name);
            item.Accepted += OnRequestAccepted;
            item.Declined += OnRequestDeclined;
        }
    }

    private void UpdateRequestBadge()
    {
        if (_requestBadge != null) _requestBadge.Visible = _friendRequests.Count > 0;
        if (_requestBadgeLabel != null) _requestBadgeLabel.Text = _friendRequests.Count.ToString();
    }

    private void OnRequestAccepted(string playerId)
    {
        int idx = _friendRequests.FindIndex(r => r.Id == playerId);
        if (idx < 0) return;

        var req = _friendRequests[idx];
        _friendRequests.RemoveAt(idx);

        if (!_friends.Any(f => f.Id == req.Id))
        {
            _friends.Insert(0, req);
            RebuildFriendsList();
        }
        RebuildRequestsList();
    }

    private void OnRequestDeclined(string playerId)
    {
        int idx = _friendRequests.FindIndex(r => r.Id == playerId);
        if (idx < 0) return;

        _friendRequests.RemoveAt(idx);
        RebuildRequestsList();
    }

    private void OnAddFriendRequested(string playerId)
    {
        var player = _searchablePlayers.FirstOrDefault(p => p.Id == playerId);
        if (string.IsNullOrEmpty(player.Id)) return;

        if (!_friends.Any(f => f.Id == player.Id))
        {
            _friends.Add(player);
            RebuildFriendsList();
        }
        RefreshSearch();
    }

    private void OnFriendDeleteRequested(string friendId, string friendName)
    {
        _pendingDeleteId = friendId;
        if (_confirmDeleteLabel != null)
            _confirmDeleteLabel.Text = $"Xóa {friendName} khỏi danh sách bạn bè?";
        if (_confirmDeleteOverlay != null)
            _confirmDeleteOverlay.Visible = true;
    }

    private void OnConfirmDeletePressed()
    {
        if (!string.IsNullOrEmpty(_pendingDeleteId))
        {
            int idx = _friends.FindIndex(f => f.Id == _pendingDeleteId);
            if (idx >= 0)
            {
                _friends.RemoveAt(idx);
                RebuildFriendsList();
            }
        }
        CloseDeleteDialog();
    }

    private void CloseDeleteDialog()
    {
        _pendingDeleteId = null;
        if (_confirmDeleteOverlay != null) _confirmDeleteOverlay.Visible = false;
    }

    private static void ClearChildren(Node parent)
    {
        if (parent == null) return;
        foreach (Node child in parent.GetChildren())
            child.QueueFree();
    }
}
