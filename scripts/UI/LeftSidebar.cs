using Godot;

namespace MarketMobile.UI;

public partial class LeftSidebar : Control
{
    [Signal]
    public delegate void TabSelectedEventHandler(int tabIndex);

    [Export] public int ActiveTabIndex { get; set; } = 0;

    private SidebarTab[] _tabs;
    private bool _isSwitchingScene = false;

    public override void _Ready()
    {
        _tabs = new SidebarTab[]
        {
            GetNodeOrNull<SidebarTab>("TabsVBox/Tab0_Sanh"),
            GetNodeOrNull<SidebarTab>("TabsVBox/Tab1_NhanVat"),
            GetNodeOrNull<SidebarTab>("TabsVBox/Tab2_SuKien"),
            GetNodeOrNull<SidebarTab>("TabsVBox/Tab3_CuaHang"),
            GetNodeOrNull<SidebarTab>("TabsVBox/Tab4_CaiDat")
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
    }

    private async void OnTabClicked(int index)
    {
        if (index == ActiveTabIndex || _isSwitchingScene) return;

        int oldIndex = ActiveTabIndex;
        ActiveTabIndex = index;

        // Animate state transition between old tab and new tab smoothly
        if (oldIndex >= 0 && oldIndex < _tabs.Length && _tabs[oldIndex] != null)
            _tabs[oldIndex].SetActive(false, true);

        if (index >= 0 && index < _tabs.Length && _tabs[index] != null)
            _tabs[index].SetActive(true, true);

        EmitSignal(SignalName.TabSelected, index);

        switch (index)
        {
            case 0:
                _isSwitchingScene = true;
                // Wait briefly for the tab expansion & icon drop animation to play out
                await ToSignal(GetTree().CreateTimer(0.24f), SceneTreeTimer.SignalName.Timeout);
                GetTree().ChangeSceneToFile("res://scenes/ui/lobby_screen.tscn");
                break;
            case 1:
                _isSwitchingScene = true;
                // Wait briefly for the tab expansion & icon drop animation to play out
                await ToSignal(GetTree().CreateTimer(0.24f), SceneTreeTimer.SignalName.Timeout);
                GetTree().ChangeSceneToFile("res://scenes/ui/CharacterScreen.tscn");
                break;
            case 2:
                GD.Print("[LeftSidebar] Chuyển đến màn hình Sự kiện");
                break;
            case 3:
                GD.Print("[LeftSidebar] Chuyển đến màn hình Cửa hàng");
                break;
            case 4:
                GD.Print("[LeftSidebar] Mở Cài đặt");
                break;
        }
    }
}
