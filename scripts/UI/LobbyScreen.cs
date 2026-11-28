using Godot;
using MarketMobile.Core;
using MarketMobile.Utils;

namespace MarketMobile.UI;

public partial class LobbyScreen : Control
{
	[Export] private SidebarTab[] _sidebarTabs;
	[Export] private Control _nameBanner;

	private SettingsPanel _settingsPanel;
	private SettingsModal _settingsModal;
	private ModeSelectScreen _modeSelectScreen;
	private LobbySocialDrawer _socialDrawer;
	private ProfileModal _profileModal;
	private Button _profileBtn;
	private LeftSidebar _leftSidebar;
	private Node2D _characterDisplay;
	private Control _topBar;
	private Control _rightColumn;
	private Control _chatBox;
	private Label _previewLine1;
	private Label _previewLine2;
	private Control _previewUnreadBadge;
	private Label _previewUnreadLabel;
	private CoinBar _coinBar;
	private CoinBar _rauBar;
	private TextureRect _modeMapTexture;
	private TextureButton _modeTeamBtn;
	private TextureButton _modeStyleBtn;
	private Label _modeTeamLabel;
	private Label _modeStyleLabel;
	private PlayerDataManager _data;

	private static readonly Texture2D TexLobbyTeamSlot = GD.Load<Texture2D>("res://assets/sprites/ui/buttons/mode_1vs1.png");
	private static readonly Texture2D TexLobbyStyleNormal = GD.Load<Texture2D>("res://assets/sprites/ui/buttons/mode_style_play.png");

	// Persistent Shell: giữ cố định Background & LeftSidebar, cache sẵn Nhân vật, Sự kiện & Cửa hàng
	private const string CharacterScenePath = "res://scenes/ui/character_screen.tscn";
	private const string EventsScenePath = "res://scenes/ui/events_screen.tscn";
	private const string ShopScenePath = "res://scenes/ui/shop_screen.tscn";
	private const string SettingsModalScenePath = "res://prefabs/ui/settings_modal.tscn";
	private const string ModeSelectScenePath = "res://scenes/ui/mode_select_screen.tscn";
	private const string SocialDrawerScenePath = "res://prefabs/ui/lobby_social_drawer.tscn";
	private const string ProfileModalScenePath = "res://prefabs/ui/profile_modal.tscn";
	private Control _embeddedHost;
	private Control _modalOverlayHost;
	private PackedScene _characterScene;
	private PackedScene _eventsScene;
	private PackedScene _shopScene;
	private CharacterScreen _characterScreen;
	private EventsScreen _eventsScreen;
	private ShopScreen _shopScreen;
	private Tween _tabContentTween;

	private int _currentTabIndex = 0;

	public override void _Ready()
	{
		// Setup Left Sidebar Tabs (để LeftSidebar quản lý animation tab, LobbyScreen lắng nghe sự kiện chuyển tab)
		_leftSidebar = GetNodeOrNull<LeftSidebar>("LeftSidebar");
		_sidebarTabs = new SidebarTab[]
		{
			GetNodeOrNull<SidebarTab>("LeftSidebar/TabsVBox/Tab0_Sanh"),
			GetNodeOrNull<SidebarTab>("LeftSidebar/TabsVBox/Tab1_NhanVat"),
			GetNodeOrNull<SidebarTab>("LeftSidebar/TabsVBox/Tab2_SuKien"),
			GetNodeOrNull<SidebarTab>("LeftSidebar/TabsVBox/Tab3_CuaHang"),
			GetNodeOrNull<SidebarTab>("LeftSidebar/TabsVBox/Tab4_TuiDo")
		};

		if (_leftSidebar != null)
		{
			_leftSidebar.IsShellHost = true;
			_leftSidebar.TabSelected += OnTabSelected;
			_leftSidebar.TabReselected += OnTabReselected;
			_leftSidebar.SettingsRequested += OnSettingsRequested;
		}
		else
		{
			for (int i = 0; i < _sidebarTabs.Length; i++)
			{
				if (_sidebarTabs[i] != null)
				{
					_sidebarTabs[i].TabIndex = i;
					_sidebarTabs[i].TabSelected += OnTabSelected;
					_sidebarTabs[i].SetActive(i == _currentTabIndex, false);
				}
			}
		}

		// Tạo host cho màn hình con (nằm dưới LeftSidebar) và modal overlay (nằm trên LeftSidebar)
		SetupShellHosts();

		// Setup Name Banner
		_nameBanner ??= GetNodeOrNull<Control>("NameBanner");
		_profileBtn = GetNodeOrNull<Button>("NameBanner/ProfileBtn");
		if (_profileBtn != null)
		{
			_profileBtn.Pressed += OpenProfileModal;
		}

		// Setup Right Column Buttons
		var mapBtn = GetNodeOrNull<BaseButton>("RightColumn/ModeCard/MapBtn");
		_modeTeamBtn = GetNodeOrNull<TextureButton>("RightColumn/ModeCard/ButtonsRow/Btn1vs1");
		_modeStyleBtn = GetNodeOrNull<TextureButton>("RightColumn/ModeCard/ButtonsRow/BtnStyle");
		var startBtn = GetNodeOrNull<BaseButton>("RightColumn/StartBtn");
		_modeMapTexture = GetNodeOrNull<TextureRect>("RightColumn/ModeCard/MapTexture");
		_modeTeamLabel = GetNodeOrNull<Label>("RightColumn/ModeCard/ButtonsRow/Btn1vs1/Label");
		_modeStyleLabel = GetNodeOrNull<Label>("RightColumn/ModeCard/ButtonsRow/BtnStyle/Label");

		if (mapBtn != null) mapBtn.Pressed += OnMapCardPressed;
		if (_modeTeamBtn != null) _modeTeamBtn.Pressed += OnMode1vs1Pressed;
		if (_modeStyleBtn != null) _modeStyleBtn.Pressed += OnModeStylePressed;
		if (startBtn != null) startBtn.Pressed += OnStartPressed;

		// Setup Lobby content refs, compact ChatPreviewBar & SettingsPanel
		_characterDisplay = GetNodeOrNull<Node2D>("CharacterDisplay");
		_topBar = GetNodeOrNull<Control>("TopBar");
		_rightColumn = GetNodeOrNull<Control>("RightColumn");
		_chatBox = GetNodeOrNull<Control>("ChatPreviewBar");
		_previewLine1 = GetNodeOrNull<Label>("ChatPreviewBar/HBox/LinesVBox/Line1");
		_previewLine2 = GetNodeOrNull<Label>("ChatPreviewBar/HBox/LinesVBox/Line2");
		_previewUnreadBadge = GetNodeOrNull<Control>("ChatPreviewBar/HBox/IconWrap/UnreadBadge");
		_previewUnreadLabel = GetNodeOrNull<Label>("ChatPreviewBar/HBox/IconWrap/UnreadBadge/CountLabel");
		var openSocialBtn = GetNodeOrNull<Button>("ChatPreviewBar/ClickBtn");
		if (openSocialBtn != null)
		{
			openSocialBtn.Pressed += OnChatPreviewPressed;
		}

		_settingsPanel = GetNodeOrNull<SettingsPanel>("SettingsPanel");
		if (_settingsPanel != null)
		{
			_settingsPanel.Visible = false;
			_settingsPanel.ConfigureModalHost(_modalOverlayHost);
			_settingsPanel.GoToShopRequested += OnInventoryGoToShop;
			_settingsPanel.GoToEventsRequested += OnInventoryGoToEvents;
		}

		// Responsive character layout
		GetViewport().SizeChanged += UpdateLobbyLayout;
		UpdateLobbyLayout();

		// Đồng bộ kinh tế & nhân vật dùng chung qua Autoload PlayerDataManager
		_coinBar = GetNodeOrNull<CoinBar>("TopBar/TopHBox/CurrenciesHBox/CoinBar");
		_rauBar = GetNodeOrNull<CoinBar>("TopBar/TopHBox/CurrenciesHBox/GemBar");
		_data = PlayerDataManager.Instance ?? GetNodeOrNull<PlayerDataManager>("/root/PlayerDataManager");
		if (_data != null)
		{
			_coinBar?.SetCoins(_data.Coins, animate: false);
			_rauBar?.SetCoins(_data.Rau, animate: false);
			_data.CurrenciesChanged += OnSharedCurrenciesChanged;
			_data.MatchConfigChanged += SyncModeCardVisual;
			SyncModeCardVisual();

			var activeCharacter = _data.ActiveCharacter;
			if (_characterDisplay != null && activeCharacter != null)
			{
				_characterDisplay.Modulate = activeCharacter.Tint;
			}
		}

		if (_coinBar != null) _coinBar.AddCoinPressed += OnAddCurrencyPressed;
		if (_rauBar != null) _rauBar.AddCoinPressed += OnAddCurrencyPressed;

		// Gắn hiệu ứng nhún (bounce) và tắt hover cho toàn bộ các nút bấm
		UIButtonHelper.AttachToAllButtons(this);
		if (openSocialBtn != null && _chatBox != null)
		{
			UIButtonHelper.AttachBounceEffect(openSocialBtn, 0.97f, ButtonPivotMode.Center, _chatBox);
		}

		// Nếu chuyển từ màn hình khác sang với cờ mở Túi đồ hoặc popup Cài đặt
		if (_data != null && _data.PendingOpenInventory)
		{
			_data.PendingOpenInventory = false;
			_currentTabIndex = 4;

			if (_leftSidebar != null) _leftSidebar.SetActiveTabVisual(4);

			if (_characterDisplay != null) _characterDisplay.Visible = false;
			if (_topBar != null) _topBar.Visible = false;
			if (_nameBanner != null) _nameBanner.Visible = false;
			if (_rightColumn != null) _rightColumn.Visible = false;
			if (_chatBox != null) _chatBox.Visible = false;
			if (_settingsPanel != null)
			{
				_settingsPanel.RefreshInventory();
				_settingsPanel.Visible = true;
			}
		}

		if (_data != null && _data.PendingOpenSettings)
		{
			_data.PendingOpenSettings = false;
			OpenSettingsModal();
		}

		// Khởi tạo ngầm màn hình Nhân vật & Cửa hàng ở các frame kế tiếp để chuyển tab tức thì
		PrewarmEmbeddedScreensAsync();
	}

	public override void _ExitTree()
	{
		_tabContentTween?.Kill();
		if (_leftSidebar != null)
		{
			_leftSidebar.TabSelected -= OnTabSelected;
			_leftSidebar.TabReselected -= OnTabReselected;
			_leftSidebar.SettingsRequested -= OnSettingsRequested;
		}
		if (_settingsPanel != null)
		{
			_settingsPanel.GoToShopRequested -= OnInventoryGoToShop;
			_settingsPanel.GoToEventsRequested -= OnInventoryGoToEvents;
		}
		if (_data != null)
		{
			_data.CurrenciesChanged -= OnSharedCurrenciesChanged;
			_data.MatchConfigChanged -= SyncModeCardVisual;
		}
		if (GetViewport() != null)
		{
			GetViewport().SizeChanged -= UpdateLobbyLayout;
		}
	}

	private void OnSharedCurrenciesChanged(int coins, int rau)
	{
		_coinBar?.SetCoins(coins, animate: true);
		_rauBar?.SetCoins(rau, animate: true);
	}

	private void OnAddCurrencyPressed()
	{
		if (_data != null)
		{
			_data.PendingShopTabIndex = 3; // Mở thẳng tab Đổi Vàng/Rau
		}
		if (_leftSidebar != null)
		{
			_leftSidebar.SelectTab(3);
		}
		else if (_sidebarTabs != null && _sidebarTabs.Length > 3 && _sidebarTabs[3] != null)
		{
			_sidebarTabs[3].EmitSignal(SidebarTab.SignalName.TabSelected, 3);
		}
	}

	private void UpdateLobbyLayout()
	{
		var center = GetViewportRect().Size / 2f;
		if (_characterDisplay != null)
		{
			_characterDisplay.Position = new Vector2(center.X, center.Y - 63f);
		}
	}

	private void OnChatPreviewPressed()
	{
		EnsureSocialDrawer();
		_socialDrawer?.OpenDrawer();
	}

	private void EnsureSocialDrawer()
	{
		if (IsInstanceValid(_socialDrawer)) return;
		var host = _modalOverlayHost ?? (Control)this;
		var packed = GD.Load<PackedScene>(SocialDrawerScenePath);
		_socialDrawer = packed?.Instantiate<LobbySocialDrawer>();
		if (_socialDrawer != null)
		{
			_socialDrawer.PreviewUpdated += SyncChatPreviewBar;
			host.AddChild(_socialDrawer);
			SyncChatPreviewBar();
		}
	}

	private void SyncChatPreviewBar()
	{
		if (_socialDrawer == null) return;
		var (line1, line2, totalUnread) = _socialDrawer.GetPreviewSnapshot();
		if (_previewLine1 != null) _previewLine1.Text = line1;
		if (_previewLine2 != null) _previewLine2.Text = line2;
		if (_previewUnreadBadge != null) _previewUnreadBadge.Visible = totalUnread > 0;
		if (_previewUnreadLabel != null) _previewUnreadLabel.Text = totalUnread > 99 ? "99+" : totalUnread.ToString();
	}

	private void OnMapCardPressed()
	{
		OpenModeSelect("map");
	}

	private void OnMode1vs1Pressed()
	{
		OpenModeSelect("team");
	}

	private void OnModeStylePressed()
	{
		OpenModeSelect("type");
	}

	private void OpenModeSelect(string focusSection)
	{
		if (!IsInstanceValid(_modeSelectScreen))
		{
			var host = _modalOverlayHost ?? (Control)this;
			var packed = GD.Load<PackedScene>(ModeSelectScenePath);
			_modeSelectScreen = packed?.Instantiate<ModeSelectScreen>();
			if (_modeSelectScreen != null)
			{
				_modeSelectScreen.SelectionConfirmed += SyncModeCardVisual;
				host.AddChild(_modeSelectScreen);
			}
		}

		_modeSelectScreen?.OpenWithFocus(focusSection, animate: true);
	}

	private void SyncModeCardVisual()
	{
		if (_data == null) return;

		if (_modeTeamLabel != null)
		{
			_modeTeamLabel.Text = GameCatalog.GetTeamModeLabel(_data.SelectedTeamMode);
		}
		if (_modeTeamBtn != null)
		{
			if (TexLobbyTeamSlot != null) _modeTeamBtn.TextureNormal = TexLobbyTeamSlot;
			// Đồng bộ màu: 1 VS 1 giữ màu xám chuẩn, 2 VS 2 đổi màu hồng khớp nút 2vs2
			_modeTeamBtn.SelfModulate = _data.SelectedTeamMode == TeamMode.Duo2v2
				? new Color(1.0f, 0.44f, 0.64f, 1f)
				: Colors.White;
		}

		if (_modeStyleLabel != null)
		{
			_modeStyleLabel.Text = GameCatalog.GetPlayTypeShortLabel(_data.SelectedPlayType);
		}
		if (_modeStyleBtn != null)
		{
			if (_data.SelectedPlayType == PlayMatchType.Normal)
			{
				if (TexLobbyStyleNormal != null) _modeStyleBtn.TextureNormal = TexLobbyStyleNormal;
				_modeStyleBtn.SelfModulate = Colors.White;
			}
			else
			{
				if (TexLobbyTeamSlot != null) _modeStyleBtn.TextureNormal = TexLobbyTeamSlot;
				_modeStyleBtn.SelfModulate = _data.SelectedPlayType switch
				{
					PlayMatchType.Ranked => new Color(0.95f, 0.25f, 0.45f, 1f),
					PlayMatchType.Bot => new Color(0.28f, 0.68f, 1.0f, 1f),
					PlayMatchType.Custom => new Color(0.55f, 0.55f, 0.64f, 1f),
					_ => Colors.White
				};
			}
		}

		var map = GameCatalog.FindMap(_data.SelectedMapId);
		if (_modeMapTexture != null && map != null && !string.IsNullOrEmpty(map.TexturePath))
		{
			var tex = ResourceLoader.Load<Texture2D>(map.TexturePath);
			if (tex != null) _modeMapTexture.Texture = tex;
			_modeMapTexture.Modulate = map.BannerTint;
		}
	}

	private void OnStartPressed()
	{
		if (_data != null)
		{
			var map = GameCatalog.FindMap(_data.SelectedMapId);
			GD.Print($"[LobbyScreen] Bắt đầu ghép trận: {GameCatalog.GetPlayTypeShortLabel(_data.SelectedPlayType)} · {GameCatalog.GetTeamModeLabel(_data.SelectedTeamMode)} · {map?.Name}");
		}
		else
		{
			GD.Print("[LobbyScreen] Bắt đầu ghép trận!");
		}
	}

	private void OnTabReselected(int tabIndex)
	{
		if (tabIndex == 2 && _currentTabIndex == 2 && _eventsScreen != null)
		{
			_eventsScreen.ReturnToPosterList(animate: true);
		}
	}

	private void OnTabSelected(int tabIndex)
	{
		if (tabIndex == _currentTabIndex || _sidebarTabs == null || tabIndex < 0 || tabIndex >= _sidebarTabs.Length)
			return;

		GD.Print($"Switching from tab {_currentTabIndex} to {tabIndex}");

		if (_leftSidebar == null)
		{
			if (_sidebarTabs[_currentTabIndex] != null)
			{
				_sidebarTabs[_currentTabIndex].SetActive(false, true);
			}

			if (_sidebarTabs[tabIndex] != null)
			{
				_sidebarTabs[tabIndex].SetActive(true, true);
			}
		}

		_currentTabIndex = tabIndex;

		// Xác định màn hình đích trong Persistent Shell
		bool isChars = tabIndex == 1;
		bool isEvents = tabIndex == 2;
		bool isShop = tabIndex == 3;
		bool isInventory = tabIndex == 4;
		bool isLobby = !isChars && !isEvents && !isShop && !isInventory;

		if (!isChars && _characterScreen != null)
		{
			_characterScreen.OnDeactivatedInShell();
			_characterScreen.Visible = false;
		}
		if (!isEvents && _eventsScreen != null)
		{
			_eventsScreen.OnDeactivatedInShell();
			_eventsScreen.Visible = false;
		}
		if (!isShop && _shopScreen != null)
		{
			_shopScreen.OnDeactivatedInShell();
			_shopScreen.Visible = false;
		}

		if (_characterDisplay != null) _characterDisplay.Visible = isLobby;
		if (_topBar != null) _topBar.Visible = isLobby;
		if (_nameBanner != null) _nameBanner.Visible = isLobby;
		if (_rightColumn != null) _rightColumn.Visible = isLobby;
		if (_chatBox != null) _chatBox.Visible = isLobby;
		if (_settingsPanel != null) _settingsPanel.Visible = isInventory;

		_tabContentTween?.Kill();

		if (isChars)
		{
			var chars = EnsureCharacterScreen();
			if (chars != null)
			{
				chars.OnActivatedInShell();
				FadeInControl(chars);
			}
		}
		else if (isEvents)
		{
			var eventsScreen = EnsureEventsScreen();
			if (eventsScreen != null)
			{
				eventsScreen.OnActivatedInShell();
				FadeInControl(eventsScreen);
			}
		}
		else if (isShop)
		{
			var shop = EnsureShopScreen();
			if (shop != null)
			{
				shop.OnActivatedInShell();
				FadeInControl(shop);
			}
		}
		else if (isInventory)
		{
			if (_settingsPanel != null)
			{
				_settingsPanel.RefreshInventory();
				FadeInControl(_settingsPanel);
			}
		}
		else
		{
			SyncLobbyCharacterAndCurrencies();
			FadeInLobbyNodes();
		}
	}

	private void SetupShellHosts()
	{
		_embeddedHost = new Control
		{
			Name = "EmbeddedHost",
			MouseFilter = MouseFilterEnum.Ignore
		};
		_embeddedHost.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(_embeddedHost);

		if (_leftSidebar != null)
		{
			MoveChild(_embeddedHost, _leftSidebar.GetIndex());
		}

		_modalOverlayHost = new Control
		{
			Name = "ModalOverlayHost",
			MouseFilter = MouseFilterEnum.Ignore
		};
		_modalOverlayHost.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(_modalOverlayHost);
	}

	private async void PrewarmEmbeddedScreensAsync()
	{
		var tree = GetTree();
		if (tree == null) return;

		// Chờ qua 1 frame để LobbyScreen hiển thị ngay lập tức rồi mới cache sẵn các tab còn lại
		await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
		if (!IsInstanceValid(this) || !IsInsideTree()) return;
		EnsureCharacterScreen();

		await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
		if (!IsInstanceValid(this) || !IsInsideTree()) return;
		EnsureShopScreen();

		await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
		if (!IsInstanceValid(this) || !IsInsideTree()) return;
		EnsureEventsScreen();

		await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
		if (!IsInstanceValid(this) || !IsInsideTree()) return;
		EnsureSocialDrawer();
	}

	private EventsScreen EnsureEventsScreen()
	{
		if (_eventsScreen != null) return _eventsScreen;
		if (_embeddedHost == null) return null;

		_eventsScene ??= GD.Load<PackedScene>(EventsScenePath);
		if (_eventsScene == null) return null;

		var instance = _eventsScene.Instantiate<EventsScreen>();
		instance.Visible = false;
		_embeddedHost.AddChild(instance);
		instance.ConfigureEmbeddedInShell(_leftSidebar, _modalOverlayHost);
		_eventsScreen = instance;
		return _eventsScreen;
	}

	private CharacterScreen EnsureCharacterScreen()
	{
		if (_characterScreen != null) return _characterScreen;
		if (_embeddedHost == null) return null;

		_characterScene ??= GD.Load<PackedScene>(CharacterScenePath);
		if (_characterScene == null) return null;

		var instance = _characterScene.Instantiate<CharacterScreen>();
		instance.Visible = false;
		_embeddedHost.AddChild(instance);
		instance.ConfigureEmbeddedInShell(_leftSidebar, _modalOverlayHost);
		_characterScreen = instance;
		return _characterScreen;
	}

	private ShopScreen EnsureShopScreen()
	{
		if (_shopScreen != null) return _shopScreen;
		if (_embeddedHost == null) return null;

		_shopScene ??= GD.Load<PackedScene>(ShopScenePath);
		if (_shopScene == null) return null;

		var instance = _shopScene.Instantiate<ShopScreen>();
		instance.Visible = false;
		_embeddedHost.AddChild(instance);
		instance.ConfigureEmbeddedInShell(_leftSidebar, _modalOverlayHost);
		_shopScreen = instance;
		return _shopScreen;
	}

	private void SyncLobbyCharacterAndCurrencies()
	{
		if (_data == null) return;

		_coinBar?.SetCoins(_data.Coins, animate: false);
		_rauBar?.SetCoins(_data.Rau, animate: false);

		var activeCharacter = _data.ActiveCharacter;
		if (_characterDisplay != null && activeCharacter != null)
		{
			_characterDisplay.Modulate = activeCharacter.Tint;
		}
	}

	private void FadeInControl(Control target)
	{
		if (target == null) return;
		target.Visible = true;
		target.Modulate = new Color(1f, 1f, 1f, 0f);

		_tabContentTween = CreateTween().SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
		_tabContentTween.TweenProperty(target, "modulate:a", 1f, 0.14f);
	}

	private void FadeInLobbyNodes()
	{
		var activeColor = _data?.ActiveCharacter?.Tint ?? Colors.White;
		Control[] controls = { _topBar, _nameBanner, _rightColumn, _chatBox };

		_tabContentTween = CreateTween().SetParallel(true).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
		foreach (var ctrl in controls)
		{
			if (ctrl == null) continue;
			ctrl.Modulate = new Color(1f, 1f, 1f, 0f);
			_tabContentTween.TweenProperty(ctrl, "modulate:a", 1f, 0.14f);
		}

		if (_characterDisplay != null)
		{
			_characterDisplay.Modulate = new Color(activeColor.R, activeColor.G, activeColor.B, 0f);
			_tabContentTween.TweenProperty(_characterDisplay, "modulate:a", activeColor.A, 0.14f);
		}
	}

	private void OnSettingsRequested()
	{
		OpenSettingsModal();
	}

	private void OpenSettingsModal()
	{
		if (!IsInstanceValid(_settingsModal))
		{
			var host = _modalOverlayHost ?? (Control)this;
			var packed = GD.Load<PackedScene>(SettingsModalScenePath);
			_settingsModal = packed?.Instantiate<SettingsModal>();
			if (_settingsModal != null)
			{
				_settingsModal.LogoutRequested += OnLogoutPressed;
				host.AddChild(_settingsModal);
			}
		}

		_settingsModal?.OpenModal();
	}

	private void OpenProfileModal()
	{
		if (!IsInstanceValid(_profileModal))
		{
			var host = _modalOverlayHost ?? (Control)this;
			var packed = GD.Load<PackedScene>(ProfileModalScenePath);
			_profileModal = packed?.Instantiate<ProfileModal>();
			if (_profileModal != null)
			{
				host.AddChild(_profileModal);
			}
		}

		_profileModal?.OpenModal();
	}

	private void OnInventoryGoToShop(int shopTabIndex)
	{
		if (_data != null && shopTabIndex >= 0)
		{
			_data.PendingShopTabIndex = shopTabIndex;
		}
		if (_leftSidebar != null)
		{
			_leftSidebar.SelectTab(3);
		}
	}

	private void OnInventoryGoToEvents()
	{
		if (_leftSidebar != null)
		{
			_leftSidebar.SelectTab(2);
		}
	}

	private void OnLogoutPressed()
	{
		GetTree().ChangeSceneToFile("res://scenes/ui/login_screen.tscn");
	}
}
