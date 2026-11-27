using Godot;
using MarketMobile.Core;
using MarketMobile.Utils;

namespace MarketMobile.UI;

public partial class LeftSidebar : Control
{
    [Signal]
    public delegate void TabSelectedEventHandler(int tabIndex);

    [Signal]
    public delegate void TabReselectedEventHandler(int tabIndex);

    [Signal]
    public delegate void SettingsRequestedEventHandler();

    [Export] public int ActiveTabIndex { get; set; } = 0;

    /// <summary>
    /// Khi true, LeftSidebar thuộc về Shell chính (lobby_screen) nên chuyển tab nội bộ ngay lập tức
    /// thay vì chờ timer rồi gọi ChangeSceneToFile.
    /// </summary>
    public bool IsShellHost { get; set; } = false;

    private SidebarTab[] _tabs;
    private TextureButton _settingBtn;
    private SettingsModal _localSettingsModal;
    private bool _isSwitchingScene = false;

    public override void _Ready()
    {
        _tabs = new SidebarTab[]
        {
            GetNodeOrNull<SidebarTab>("TabsVBox/Tab0_Sanh"),
            GetNodeOrNull<SidebarTab>("TabsVBox/Tab1_NhanVat"),
            GetNodeOrNull<SidebarTab>("TabsVBox/Tab2_SuKien"),
            GetNodeOrNull<SidebarTab>("TabsVBox/Tab3_CuaHang"),
            GetNodeOrNull<SidebarTab>("TabsVBox/Tab4_TuiDo")
        };

        for (int i = 0; i < _tabs.Length; i++)
        {
            if (_tabs[i] != null)
            {
                _tabs[i].TabIndex = i;
                _tabs[i].TabSelected += OnTabClicked;
                _tabs[i].SetActive(i == ActiveTabIndex, false);
            }
        }

        _settingBtn = GetNodeOrNull<TextureButton>("SettingBtn");
        if (_settingBtn != null)
        {
            _settingBtn.Pressed += OnSettingBtnPressed;
            UIButtonHelper.AttachBounceEffect(_settingBtn);
        }
    }

    public override void _ExitTree()
    {
        if (_settingBtn != null)
        {
            _settingBtn.Pressed -= OnSettingBtnPressed;
        }
    }

    private void OnSettingBtnPressed()
    {
        if (_settingBtn != null)
        {
            _settingBtn.PivotOffset = _settingBtn.Size / 2f;
            var spinTween = CreateTween();
            spinTween.TweenProperty(_settingBtn, "rotation", Mathf.DegToRad(35f), 0.14f)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.Out);
            spinTween.TweenProperty(_settingBtn, "rotation", 0f, 0.18f)
                .SetTrans(Tween.TransitionType.Back)
                .SetEase(Tween.EaseType.Out);
        }

        if (IsShellHost)
        {
            EmitSignal(SignalName.SettingsRequested);
            return;
        }

        // Mở popup Cài đặt ngay trên scene hiện tại (ví dụ friends_screen) nếu chạy ngoài shell
        var host = GetTree().CurrentScene;
        if (host == null) return;

        if (!IsInstanceValid(_localSettingsModal))
        {
            var packed = ResourceLoader.Load<PackedScene>("res://prefabs/ui/settings_modal.tscn");
            _localSettingsModal = packed?.Instantiate<SettingsModal>();
            if (_localSettingsModal != null)
            {
                _localSettingsModal.LogoutRequested += () =>
                {
                    GetTree().ChangeSceneToFile("res://scenes/ui/login_screen.tscn");
                };
                host.AddChild(_localSettingsModal);
            }
        }

        _localSettingsModal?.OpenModal();
    }

    /// <summary>
    /// Cập nhật visual active-tab từ bên ngoài mà không kích hoạt điều hướng.
    /// Dùng khi màn hình lobby cần đồng bộ tab Cài đặt qua PendingOpenSettings.
    /// </summary>
    public void SetActiveTabVisual(int index)
    {
        if (_tabs == null) return;
        ActiveTabIndex = index;
        for (int i = 0; i < _tabs.Length; i++)
        {
            if (_tabs[i] != null)
                _tabs[i].SetActive(i == index, false);
        }
    }

    /// <summary>
    /// Chuyển sang tab đích theo luồng click chuẩn (dùng khi nút trong nội dung yêu cầu đổi tab).
    /// </summary>
    public void SelectTab(int index)
    {
        OnTabClicked(index);
    }

    /// <summary>
    /// Vô hiệu hóa LeftSidebar cục bộ của scene con khi được nhúng trong Persistent Shell.
    /// </summary>
    public void DisableAsEmbedded()
    {
        Visible = false;
        MouseFilter = MouseFilterEnum.Ignore;
        ProcessMode = ProcessModeEnum.Disabled;
    }

    private async void OnTabClicked(int index)
    {
        if (_isSwitchingScene) return;
        if (index == ActiveTabIndex)
        {
            EmitSignal(SignalName.TabReselected, index);
            return;
        }

        int oldIndex = ActiveTabIndex;
        ActiveTabIndex = index;

        // Animate state transition between old tab and new tab smoothly
        if (oldIndex >= 0 && oldIndex < _tabs.Length && _tabs[oldIndex] != null)
            _tabs[oldIndex].SetActive(false, true);

        if (index >= 0 && index < _tabs.Length && _tabs[index] != null)
            _tabs[index].SetActive(true, true);

        EmitSignal(SignalName.TabSelected, index);

        // Trong chế độ Shell, LobbyScreen đã cache sẵn các màn hình nên không đổi scene
        if (IsShellHost)
        {
            if (index == 2)
                GD.Print("[LeftSidebar] Chuyển đến màn hình Sự kiện");
            return;
        }

        string currentScenePath = GetTree().CurrentScene?.SceneFilePath ?? "";

        switch (index)
        {
            case 0:
                if (currentScenePath != "res://scenes/ui/lobby_screen.tscn")
                {
                    _isSwitchingScene = true;
                    // Wait briefly for the tab expansion & icon drop animation to play out
                    await ToSignal(GetTree().CreateTimer(0.24f), SceneTreeTimer.SignalName.Timeout);
                    GetTree().ChangeSceneToFile("res://scenes/ui/lobby_screen.tscn");
                }
                break;
            case 1:
                if (currentScenePath != "res://scenes/ui/character_screen.tscn")
                {
                    _isSwitchingScene = true;
                    // Wait briefly for the tab expansion & icon drop animation to play out
                    await ToSignal(GetTree().CreateTimer(0.24f), SceneTreeTimer.SignalName.Timeout);
                    GetTree().ChangeSceneToFile("res://scenes/ui/character_screen.tscn");
                }
                break;
            case 2:
                if (currentScenePath != "res://scenes/ui/events_screen.tscn")
                {
                    _isSwitchingScene = true;
                    await ToSignal(GetTree().CreateTimer(0.24f), SceneTreeTimer.SignalName.Timeout);
                    GetTree().ChangeSceneToFile("res://scenes/ui/events_screen.tscn");
                }
                break;
            case 3:
                if (currentScenePath != "res://scenes/ui/shop_screen.tscn")
                {
                    _isSwitchingScene = true;
                    // Wait briefly for the tab expansion & icon drop animation to play out
                    await ToSignal(GetTree().CreateTimer(0.24f), SceneTreeTimer.SignalName.Timeout);
                    GetTree().ChangeSceneToFile("res://scenes/ui/shop_screen.tscn");
                }
                break;
            case 4:
                GD.Print("[LeftSidebar] Mở Túi đồ");
                if (currentScenePath != "res://scenes/ui/lobby_screen.tscn")
                {
                    _isSwitchingScene = true;
                    var data = GetNodeOrNull<PlayerDataManager>("/root/PlayerDataManager");
                    if (data != null) data.PendingOpenInventory = true;
                    await ToSignal(GetTree().CreateTimer(0.24f), SceneTreeTimer.SignalName.Timeout);
                    GetTree().ChangeSceneToFile("res://scenes/ui/lobby_screen.tscn");
                }
                // Nếu đang ở lobby_screen rồi thì LobbyScreen.OnTabSelected(4) sẽ tự xử lý
                break;
        }
    }
}
