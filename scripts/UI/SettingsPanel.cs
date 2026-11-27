using Godot;

namespace MarketMobile.UI;

public partial class SettingsPanel : Control
{
	[Signal]
	public delegate void LogoutRequestedEventHandler();

	private HSlider _volumeSlider;
	private Label _volumeValueLabel;
	private TextureButton _soundSwitchBtn;
	private TextureRect _knob;
	private Label _muteStatusLabel;
	private TextureButton _langBtn;
	private Label _langBtnLabel;
	private TextureButton _logoutBtn;

	private bool _isSoundOn = true;
	private Tween _knobTween;

	// Knob X positions (pixels inside btn_sound 119x59, opaque pill X: 10..98)
	private const float KnobOnX = 54f;
	private const float KnobOffX = 6f;
	private const float KnobWidth = 50f;

	public override void _Ready()
	{
		_volumeSlider = GetNodeOrNull<HSlider>("ContentMargin/MainVBox/VolumeRow/VolumeSlider");
		_volumeValueLabel = GetNodeOrNull<Label>("ContentMargin/MainVBox/VolumeRow/VolumeValueLabel");
		_soundSwitchBtn = GetNodeOrNull<TextureButton>("ContentMargin/MainVBox/SoundToggleRow/SoundSwitchBtn");
		_knob = GetNodeOrNull<TextureRect>("ContentMargin/MainVBox/SoundToggleRow/SoundSwitchBtn/Knob");
		_muteStatusLabel = GetNodeOrNull<Label>("ContentMargin/MainVBox/SoundToggleRow/MuteStatusLabel");
		_langBtn = GetNodeOrNull<TextureButton>("ContentMargin/MainVBox/LanguageRow/LangBtn");
		_langBtnLabel = GetNodeOrNull<Label>("ContentMargin/MainVBox/LanguageRow/LangBtn/Label");
		_logoutBtn = GetNodeOrNull<TextureButton>("LogoutBtn");

		if (_volumeSlider != null)
			_volumeSlider.ValueChanged += OnVolumeChanged;

		if (_soundSwitchBtn != null)
			_soundSwitchBtn.Pressed += OnSoundToggled;

		if (_langBtn != null)
			_langBtn.Pressed += OnLanguagePressed;

		if (_logoutBtn != null)
			_logoutBtn.Pressed += OnLogoutBtnPressed;

		// Đồng bộ trạng thái âm thanh hiện tại từ AudioServer
		_isSoundOn = !AudioServer.IsBusMute(0);
		UpdateSwitchVisual(false);
		OnVolumeChanged(_volumeSlider?.Value ?? 80.0);
	}

	private void OnVolumeChanged(double value)
	{
		int rounded = Mathf.RoundToInt((float)value);
		if (_volumeValueLabel != null)
			_volumeValueLabel.Text = $"{rounded}/100";

		// Áp dụng lên AudioServer bus 0 (Master)
		float db = rounded == 0 ? -80f : Mathf.LinearToDb(rounded / 100f);
		AudioServer.SetBusVolumeDb(0, db);
	}

	private void OnSoundToggled()
	{
		_isSoundOn = !_isSoundOn;
		AudioServer.SetBusMute(0, !_isSoundOn);
		UpdateSwitchVisual(true);
	}

	private void UpdateSwitchVisual(bool animate)
	{
		if (_muteStatusLabel != null)
		{
			_muteStatusLabel.Text = _isSoundOn ? "Đang bật" : "Đang tắt";
			_muteStatusLabel.AddThemeColorOverride("font_color",
				_isSoundOn ? new Color(1f, 1f, 1f) : new Color(1f, 0.65f, 0.65f));
		}

		if (_knob == null) return;

		float targetX = _isSoundOn ? KnobOnX : KnobOffX;

		if (!animate)
		{
			_knob.OffsetLeft = targetX;
			_knob.OffsetRight = targetX + KnobWidth;
			return;
		}

		_knobTween?.Kill();
		_knobTween = CreateTween().SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
		_knobTween.TweenProperty(_knob, "offset_left", targetX, 0.18f);
		_knobTween.Parallel().TweenProperty(_knob, "offset_right", targetX + KnobWidth, 0.18f);
	}

	private void OnLanguagePressed()
	{
		if (_langBtnLabel == null) return;
		_langBtnLabel.Text = _langBtnLabel.Text == "Tiếng Việt" ? "English" : "Tiếng Việt";
		GD.Print($"[SettingsPanel] Ngôn ngữ: {_langBtnLabel.Text}");
	}

	private void OnLogoutBtnPressed()
	{
		EmitSignal(SignalName.LogoutRequested);
	}
}
