using Godot;
using MarketMobile.Utils;

namespace MarketMobile.UI;

/// <summary>
/// Popup Cài đặt dùng nền modal_setting.png, mở từ icon bánh răng góc trên trái.
/// </summary>
public partial class SettingsModal : Control
{
	[Signal]
	public delegate void LogoutRequestedEventHandler();

	[Signal]
	public delegate void ClosedEventHandler();

	private Control _backdrop;
	private Control _dialogFrame;
	private Button _closeBtn;
	private HSlider _volumeSlider;
	private Label _volumeValueLabel;
	private Button _soundSwitchBtn;
	private Panel _knob;
	private Button _langBtn;
	private Label _langBtnLabel;
	private Button _logoutBtn;

	private bool _isMuted = false;
	private int _langIndex = 0; // 0 = Tiếng Việt, 1 = English
	private Tween _knobTween;
	private Tween _modalTween;

	private static readonly StyleBoxFlat StyleSwitchOn = CreateSwitchStyle(new Color(0.24f, 0.72f, 0.34f, 1f));
	private static readonly StyleBoxFlat StyleSwitchOff = CreateSwitchStyle(new Color(0.58f, 0.56f, 0.62f, 1f));

	private const float KnobOnX = 54f;
	private const float KnobOffX = 6f;

	public override void _Ready()
	{
		Visible = false;

		_backdrop = GetNodeOrNull<Control>("Backdrop");
		_dialogFrame = GetNodeOrNull<Control>("Center/DialogFrame");
		_closeBtn = GetNodeOrNull<Button>("Center/DialogFrame/CloseBtn");
		_volumeSlider = GetNodeOrNull<HSlider>("Center/DialogFrame/BodyRoot/MainVBox/RowVolume/VolumeSlider");
		_volumeValueLabel = GetNodeOrNull<Label>("Center/DialogFrame/BodyRoot/MainVBox/RowVolume/VolumeValueLabel");
		_soundSwitchBtn = GetNodeOrNull<Button>("Center/DialogFrame/BodyRoot/MainVBox/RowSound/SoundSwitchBtn");
		_knob = GetNodeOrNull<Panel>("Center/DialogFrame/BodyRoot/MainVBox/RowSound/SoundSwitchBtn/Knob");
		_langBtn = GetNodeOrNull<Button>("Center/DialogFrame/BodyRoot/MainVBox/RowLanguage/LangBtn");
		_langBtnLabel = GetNodeOrNull<Label>("Center/DialogFrame/BodyRoot/MainVBox/RowLanguage/LangBtn/BtnLabel");
		_logoutBtn = GetNodeOrNull<Button>("Center/DialogFrame/BodyRoot/LogoutRow/LogoutBtn");

		if (_dialogFrame != null)
		{
			_dialogFrame.MouseFilter = MouseFilterEnum.Stop;
			_dialogFrame.PivotOffset = _dialogFrame.CustomMinimumSize / 2f;
		}

		if (_backdrop != null)
		{
			_backdrop.GuiInput += OnBackdropInput;
		}

		if (_closeBtn != null)
		{
			_closeBtn.Pressed += CloseModal;
		}

		if (_volumeSlider != null)
		{
			_volumeSlider.ValueChanged += OnVolumeChanged;
		}

		if (_soundSwitchBtn != null)
		{
			_soundSwitchBtn.Pressed += OnSoundSwitchPressed;
		}

		if (_langBtn != null)
		{
			_langBtn.Pressed += OnLanguagePressed;
		}

		if (_logoutBtn != null)
		{
			_logoutBtn.Pressed += OnLogoutBtnPressed;
		}

		SyncFromAudioServer();
		UIButtonHelper.AttachToAllButtons(this);
	}

	public override void _ExitTree()
	{
		_knobTween?.Kill();
		_modalTween?.Kill();
	}

	public void OpenModal()
	{
		SyncFromAudioServer();
		Visible = true;
		MoveToFront();

		if (_dialogFrame != null)
		{
			_dialogFrame.PivotOffset = _dialogFrame.Size / 2f;
			_dialogFrame.Scale = new Vector2(0.88f, 0.88f);
			_dialogFrame.Modulate = new Color(1, 1, 1, 0);
		}
		if (_backdrop != null)
		{
			_backdrop.Modulate = new Color(1, 1, 1, 0);
		}

		_modalTween?.Kill();
		_modalTween = CreateTween().SetParallel(true);
		if (_backdrop != null)
		{
			_modalTween.TweenProperty(_backdrop, "modulate:a", 1f, 0.16f);
		}
		if (_dialogFrame != null)
		{
			_modalTween.TweenProperty(_dialogFrame, "scale", Vector2.One, 0.22f)
				.SetTrans(Tween.TransitionType.Back)
				.SetEase(Tween.EaseType.Out);
			_modalTween.TweenProperty(_dialogFrame, "modulate:a", 1f, 0.16f);
		}
	}

	public void CloseModal()
	{
		if (!Visible) return;

		_modalTween?.Kill();
		_modalTween = CreateTween().SetParallel(true);
		if (_backdrop != null)
		{
			_modalTween.TweenProperty(_backdrop, "modulate:a", 0f, 0.14f);
		}
		if (_dialogFrame != null)
		{
			_dialogFrame.PivotOffset = _dialogFrame.Size / 2f;
			_modalTween.TweenProperty(_dialogFrame, "scale", new Vector2(0.90f, 0.90f), 0.14f)
				.SetTrans(Tween.TransitionType.Quad)
				.SetEase(Tween.EaseType.In);
			_modalTween.TweenProperty(_dialogFrame, "modulate:a", 0f, 0.14f);
		}
		_modalTween.Chain().TweenCallback(Callable.From(() =>
		{
			Visible = false;
			EmitSignal(SignalName.Closed);
		}));
	}

	private void OnBackdropInput(InputEvent @event)
	{
		bool isClick = @event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left;
		bool isTouch = @event is InputEventScreenTouch st && st.Pressed;
		if (isClick || isTouch)
		{
			_backdrop?.AcceptEvent();
			CloseModal();
		}
	}

	private void SyncFromAudioServer()
	{
		int busCount = AudioServer.GetBusCount();
		if (busCount > 0)
		{
			_isMuted = AudioServer.IsBusMute(0);
			float db = AudioServer.GetBusVolumeDb(0);
			float linear = _isMuted ? 0f : Mathf.DbToLinear(db);
			float pct = Mathf.Clamp(Mathf.Round(linear * 100f), 0f, 100f);

			if (_volumeSlider != null) _volumeSlider.SetValueNoSignal(pct);
			if (_volumeValueLabel != null) _volumeValueLabel.Text = $"{(int)pct}%";
		}

		UpdateSwitchVisual(animate: false);
	}

	private void OnVolumeChanged(double value)
	{
		int pct = (int)value;
		if (_volumeValueLabel != null)
		{
			_volumeValueLabel.Text = $"{pct}%";
		}

		if (AudioServer.GetBusCount() > 0)
		{
			float linear = Mathf.Clamp(pct / 100f, 0.0001f, 1f);
			AudioServer.SetBusVolumeDb(0, Mathf.LinearToDb(linear));
			if (pct == 0 && !_isMuted)
			{
				_isMuted = true;
				AudioServer.SetBusMute(0, true);
				UpdateSwitchVisual(animate: true);
			}
			else if (pct > 0 && _isMuted)
			{
				_isMuted = false;
				AudioServer.SetBusMute(0, false);
				UpdateSwitchVisual(animate: true);
			}
		}
	}

	private void OnSoundSwitchPressed()
	{
		_isMuted = !_isMuted;
		if (AudioServer.GetBusCount() > 0)
		{
			AudioServer.SetBusMute(0, _isMuted);
		}
		UpdateSwitchVisual(animate: true);
	}

	private void UpdateSwitchVisual(bool animate)
	{
		var style = _isMuted ? StyleSwitchOff : StyleSwitchOn;
		if (_soundSwitchBtn != null)
		{
			_soundSwitchBtn.AddThemeStyleboxOverride("normal", style);
			_soundSwitchBtn.AddThemeStyleboxOverride("hover", style);
			_soundSwitchBtn.AddThemeStyleboxOverride("pressed", style);
		}

		if (_knob != null)
		{
			float targetX = _isMuted ? KnobOffX : KnobOnX;
			if (animate)
			{
				_knobTween?.Kill();
				_knobTween = CreateTween();
				_knobTween.TweenProperty(_knob, "position:x", targetX, 0.16f)
					.SetTrans(Tween.TransitionType.Quad)
					.SetEase(Tween.EaseType.Out);
			}
			else
			{
				_knob.Position = new Vector2(targetX, _knob.Position.Y);
			}
		}
	}

	private void OnLanguagePressed()
	{
		_langIndex = (_langIndex + 1) % 2;
		if (_langBtnLabel != null)
		{
			_langBtnLabel.Text = _langIndex == 0 ? "Tiếng Việt" : "English";
		}
		GD.Print($"[SettingsModal] Ngôn ngữ: {_langBtnLabel?.Text}");
	}

	private void OnLogoutBtnPressed()
	{
		CloseModal();
		EmitSignal(SignalName.LogoutRequested);
	}

	private static StyleBoxFlat CreateSwitchStyle(Color bg) => new()
	{
		BgColor = bg,
		BorderWidthLeft = 2,
		BorderWidthTop = 2,
		BorderWidthRight = 2,
		BorderWidthBottom = 2,
		BorderColor = new Color(0.18f, 0.14f, 0.18f, 0.85f),
		CornerRadiusTopLeft = 25,
		CornerRadiusTopRight = 25,
		CornerRadiusBottomRight = 25,
		CornerRadiusBottomLeft = 25
	};
}
