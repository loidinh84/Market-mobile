using Godot;

namespace MarketMobile.UI;

public partial class LobbyScreen : Control
{
    [Export] private SidebarTab[] _sidebarTabs;
    [Export] private Control _nameBanner;
    [Export] private Control _dropdownContent;
    [Export] private Button _headerBtn;
    [Export] private Control _dismissBackdrop;
    [Export] private Button _copyIdBtn;
    [Export] private Button _changeNameBtn;
    [Export] private Button _logoutBtn;

    private SettingsPanel _settingsPanel;
    private Node2D _characterDisplay;
    private Control _topBar;
    private Control _rightColumn;
    private Control _chatBox;

    private int _currentTabIndex = 0;
    private bool _isDropdownOpen = false;
    private Tween _bannerTween;

    private const float CollapsedHeight = 150f;
    private const float ExpandedHeight = 460f;
    private const float AnimDuration = 0.28f;

    public override void _Ready()
    {
        // Setup Left Sidebar Tabs
        _sidebarTabs = new SidebarTab[]
        {
            GetNodeOrNull<SidebarTab>("LeftSidebar/TabsVBox/Tab0_Sanh"),
            GetNodeOrNull<SidebarTab>("LeftSidebar/TabsVBox/Tab1_NhanVat"),
            GetNodeOrNull<SidebarTab>("LeftSidebar/TabsVBox/Tab2_SuKien"),
            GetNodeOrNull<SidebarTab>("LeftSidebar/TabsVBox/Tab3_CuaHang"),
            GetNodeOrNull<SidebarTab>("LeftSidebar/TabsVBox/Tab4_CaiDat")
        };

        for (int i = 0; i < _sidebarTabs.Length; i++)
        {
            if (_sidebarTabs[i] != null)
            {
                _sidebarTabs[i].TabIndex = i;
                _sidebarTabs[i].TabSelected += OnTabSelected;
                _sidebarTabs[i].SetActive(i == _currentTabIndex, false);
            }
        }

        // Setup Name Banner Dropdown (if present)
        _nameBanner ??= GetNodeOrNull<Control>("NameBanner");
        _dropdownContent ??= GetNodeOrNull<Control>("NameBanner/DropdownContent");
        _headerBtn ??= GetNodeOrNull<Button>("NameBanner/HeaderBtn");
        _dismissBackdrop ??= GetNodeOrNull<Control>("DismissBackdrop");
        _copyIdBtn ??= GetNodeOrNull<Button>("NameBanner/DropdownContent/CopyIdBtn");
        _changeNameBtn ??= GetNodeOrNull<Button>("NameBanner/DropdownContent/ChangeNameBtn");
        _logoutBtn ??= GetNodeOrNull<Button>("NameBanner/DropdownContent/LogoutBtn");

        if (_headerBtn != null)
        {
            _headerBtn.Pressed += ToggleDropdown;
        }

        if (_dismissBackdrop != null)
        {
            _dismissBackdrop.GuiInput += OnBackdropInput;
            _dismissBackdrop.Visible = false;
        }

        if (_copyIdBtn != null)
        {
            _copyIdBtn.Pressed += OnCopyIdPressed;
        }

        if (_logoutBtn != null)
        {
            _logoutBtn.Pressed += OnLogoutPressed;
        }

        if (_dropdownContent != null)
        {
            _dropdownContent.Visible = false;
            _dropdownContent.Modulate = new Color(1, 1, 1, 0);
        }

        // Setup Right Column Buttons
        var friendsBtn = GetNodeOrNull<BaseButton>("RightColumn/FriendsBtn");
        var btn1vs1 = GetNodeOrNull<BaseButton>("RightColumn/ModeCard/ButtonsRow/Btn1vs1");
        var btnStyle = GetNodeOrNull<BaseButton>("RightColumn/ModeCard/ButtonsRow/BtnStyle");
        var startBtn = GetNodeOrNull<BaseButton>("RightColumn/StartBtn");

        if (friendsBtn != null) friendsBtn.Pressed += OnFriendsPressed;
        if (btn1vs1 != null) btn1vs1.Pressed += OnMode1vs1Pressed;
        if (btnStyle != null) btnStyle.Pressed += OnModeStylePressed;
        if (startBtn != null) startBtn.Pressed += OnStartPressed;

        // Setup ChatBox
        var chatInput = GetNodeOrNull<LineEdit>("ChatBox/VBox/InputHBox/InputField");
        var chatSendBtn = GetNodeOrNull<Button>("ChatBox/VBox/InputHBox/SendBtn");
        var chatEmojiBtn = GetNodeOrNull<BaseButton>("ChatBox/VBox/InputHBox/EmojiBtn");
        var messagesLabel = GetNodeOrNull<RichTextLabel>("ChatBox/VBox/MessagesScroll/MessagesLabel");

        if (chatSendBtn != null && chatInput != null)
        {
            chatSendBtn.Pressed += () => SendChatMessage(chatInput, messagesLabel);
            chatInput.TextSubmitted += (_) => SendChatMessage(chatInput, messagesLabel);
        }

        if (chatEmojiBtn != null && chatInput != null)
        {
            chatEmojiBtn.Pressed += () =>
            {
                chatInput.Text += "😊";
                chatInput.GrabFocus();
                chatInput.CaretColumn = chatInput.Text.Length;
            };
        }

        // Setup Channel Tabs
        var tabWorld = GetNodeOrNull<Button>("ChatBox/VBox/ChannelsHBox/TabWorld");
        var tabFriends = GetNodeOrNull<Button>("ChatBox/VBox/ChannelsHBox/TabFriends");
        var tabTeam = GetNodeOrNull<Button>("ChatBox/VBox/ChannelsHBox/TabTeam");

        if (tabWorld != null) tabWorld.Pressed += () => SwitchChatChannel(0, tabWorld, tabFriends, tabTeam);
        if (tabFriends != null) tabFriends.Pressed += () => SwitchChatChannel(1, tabWorld, tabFriends, tabTeam);
        if (tabTeam != null) tabTeam.Pressed += () => SwitchChatChannel(2, tabWorld, tabFriends, tabTeam);

        // Setup Lobby content refs & SettingsPanel
        _characterDisplay = GetNodeOrNull<Node2D>("CharacterDisplay");
        _topBar = GetNodeOrNull<Control>("TopBar");
        _rightColumn = GetNodeOrNull<Control>("RightColumn");
        _chatBox = GetNodeOrNull<Control>("ChatBox");

        _settingsPanel = GetNodeOrNull<SettingsPanel>("SettingsPanel");
        if (_settingsPanel != null)
        {
            _settingsPanel.Visible = false;
            _settingsPanel.LogoutRequested += OnLogoutPressed;
        }
    }

    private void SendChatMessage(LineEdit input, RichTextLabel messages)
    {
        if (input == null || string.IsNullOrWhiteSpace(input.Text) || messages == null)
            return;

        messages.AppendText($"\n[b]Bạn:[/b] {input.Text}");
        input.Text = string.Empty;
    }

    private void SwitchChatChannel(int channel, Button tabWorld, Button tabFriends, Button tabTeam)
    {
        var activeColor = new Color(1, 1, 1, 1);
        var inactiveColor = new Color(0.8f, 0.8f, 0.85f, 0.8f);

        if (tabWorld != null) tabWorld.AddThemeColorOverride("font_color", channel == 0 ? activeColor : inactiveColor);
        if (tabFriends != null) tabFriends.AddThemeColorOverride("font_color", channel == 1 ? activeColor : inactiveColor);
        if (tabTeam != null) tabTeam.AddThemeColorOverride("font_color", channel == 2 ? activeColor : inactiveColor);

        string channelName = channel switch { 0 => "Thế giới", 1 => "Bạn bè", 2 => "Tổ đội", _ => "" };
        GD.Print($"[LobbyScreen] Chuyển kênh chat sang: {channelName}");
    }

    private void OnFriendsPressed()
    {
        GetTree().ChangeSceneToFile("res://scenes/ui/friends_screen.tscn");
    }

    private void OnMode1vs1Pressed()
    {
        GD.Print("[LobbyScreen] Đổi số người chơi (1 VS 1)!");
    }

    private void OnModeStylePressed()
    {
        GD.Print("[LobbyScreen] Đổi thể loại đấu (thường / rank)!");
    }

    private void OnStartPressed()
    {
        GD.Print("[LobbyScreen] Bắt đầu ghép trận!");
    }

    private void OnTabSelected(int tabIndex)
    {
        if (tabIndex == _currentTabIndex || tabIndex < 0 || tabIndex >= _sidebarTabs.Length)
            return;

        GD.Print($"Switching from tab {_currentTabIndex} to {tabIndex}");

        // Shrink previous tab
        if (_sidebarTabs[_currentTabIndex] != null)
        {
            _sidebarTabs[_currentTabIndex].SetActive(false, true);
        }

        // Expand new tab
        _currentTabIndex = tabIndex;
        if (_sidebarTabs[_currentTabIndex] != null)
        {
            _sidebarTabs[_currentTabIndex].SetActive(true, true);
        }

        // Toggle visibility between Lobby UI and SettingsPanel
        bool isLobby = tabIndex == 0;
        bool isSettings = tabIndex == 4;

        if (!isLobby && _isDropdownOpen)
        {
            CloseDropdown();
        }

        if (_characterDisplay != null) _characterDisplay.Visible = isLobby;
        if (_topBar != null) _topBar.Visible = isLobby;
        if (_nameBanner != null) _nameBanner.Visible = isLobby;
        if (_rightColumn != null) _rightColumn.Visible = isLobby;
        if (_chatBox != null) _chatBox.Visible = isLobby;
        if (_settingsPanel != null) _settingsPanel.Visible = isSettings;
    }

    private void ToggleDropdown()
    {
        if (_isDropdownOpen)
            CloseDropdown();
        else
            OpenDropdown();
    }

    private void OpenDropdown()
    {
        if (_nameBanner == null) return;
        _isDropdownOpen = true;

        if (_dismissBackdrop != null)
            _dismissBackdrop.Visible = true;

        if (_dropdownContent != null)
            _dropdownContent.Visible = true;

        _bannerTween?.Kill();
        _bannerTween = CreateTween().SetParallel(true);
        _bannerTween.TweenProperty(_nameBanner, "custom_minimum_size:y", ExpandedHeight, AnimDuration)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        _bannerTween.TweenProperty(_nameBanner, "size:y", ExpandedHeight, AnimDuration)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);

        if (_dropdownContent != null)
        {
            _bannerTween.TweenProperty(_dropdownContent, "modulate:a", 1.0f, AnimDuration * 0.8f)
                .SetDelay(AnimDuration * 0.2f);
        }
    }

    private void CloseDropdown()
    {
        if (_nameBanner == null) return;
        _isDropdownOpen = false;

        if (_dismissBackdrop != null)
            _dismissBackdrop.Visible = false;

        _bannerTween?.Kill();
        _bannerTween = CreateTween().SetParallel(true);
        _bannerTween.TweenProperty(_nameBanner, "custom_minimum_size:y", CollapsedHeight, AnimDuration * 0.8f)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        _bannerTween.TweenProperty(_nameBanner, "size:y", CollapsedHeight, AnimDuration * 0.8f)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);

        if (_dropdownContent != null)
        {
            _bannerTween.TweenProperty(_dropdownContent, "modulate:a", 0.0f, AnimDuration * 0.5f);
            _bannerTween.Chain().TweenCallback(Callable.From(() =>
            {
                if (!_isDropdownOpen && _dropdownContent != null)
                    _dropdownContent.Visible = false;
            }));
        }
    }

    private void OnBackdropInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseBtn && mouseBtn.Pressed && mouseBtn.ButtonIndex == MouseButton.Left)
        {
            CloseDropdown();
        }
    }

    private void OnCopyIdPressed()
    {
        DisplayServer.ClipboardSet("123467");
        GD.Print("Copied ID to clipboard!");
    }

    private void OnLogoutPressed()
    {
        GetTree().ChangeSceneToFile("res://scenes/ui/login_screen.tscn");
    }
}

