using Godot;

namespace MarketMobile.UI;

public partial class SidebarTab : Control
{
    [Signal]
    public delegate void TabSelectedEventHandler(int tabIndex);

    [Export] public string Title { get; set; } = "Sảnh";
    [Export] public Texture2D IconTexture { get; set; }
    [Export] public int TabIndex { get; set; } = 0;
    [Export] public bool IsActive { get; set; } = false;

    [Export] private Texture2D _tickedTexture;
    [Export] private Texture2D _untickTexture;

    private TextureRect _bgTexture;
    private TextureRect _iconRect;
    private Label _titleLabel;
    private Button _clickBtn;
    private Tween _animTween;

    public const float ActiveWidth = 460f;
    public const float InactiveWidth = 380f;
    public const float TabHeight = 158f;

    private static readonly Vector2 ActiveIconPos = new(245f, 32f);
    private static readonly Vector2 ActiveIconSize = new(92f, 92f);
    private static readonly Vector2 ActiveLabelPos = new(50f, 45f);
    private static readonly Vector2 ActiveLabelSize = new(220f, 65f);

    private Vector2 _inactiveIconPos = new(120f, -25f);
    private Vector2 _inactiveIconSize = new(100f, 100f);
    private Vector2 _inactiveLabelPos = new(0f, 85f);
    private Vector2 _inactiveLabelSize = new(310f, 65f);
    private int _inactiveFontSize = 65;

    public override void _Ready()
    {
        _bgTexture = GetNodeOrNull<TextureRect>("BgTexture");
        _iconRect = GetNodeOrNull<TextureRect>("IconRect");
        _titleLabel = GetNodeOrNull<Label>("TitleLabel");
        _clickBtn = GetNodeOrNull<Button>("ClickBtn");

        _tickedTexture ??= GD.Load<Texture2D>("res://assets/sprites/icons/tab_ticked.png");
        _untickTexture ??= GD.Load<Texture2D>("res://assets/sprites/icons/tab_untick.png");

        if (_iconRect != null && IconTexture != null)
            _iconRect.Texture = IconTexture;

        if (_titleLabel != null && !string.IsNullOrEmpty(Title))
            _titleLabel.Text = Title;

        if (_clickBtn != null)
            _clickBtn.Pressed += OnClick;

        // Cache the custom positions and sizes saved in the scene
        if (!IsActive)
        {
            if (_iconRect != null)
            {
                _inactiveIconPos = _iconRect.Position;
                _inactiveIconSize = _iconRect.Size;
            }
            if (_titleLabel != null)
            {
                _inactiveLabelPos = _titleLabel.Position;
                _inactiveLabelSize = _titleLabel.Size;
                _inactiveFontSize = _titleLabel.GetThemeFontSize("font_size");
                if (_inactiveFontSize <= 0) _inactiveFontSize = 65;
            }
        }
    }

    private void OnClick()
    {
        EmitSignal(SignalName.TabSelected, TabIndex);
    }

    public void SetActive(bool active, bool animate = true)
    {
        IsActive = active;

        _bgTexture ??= GetNodeOrNull<TextureRect>("BgTexture");
        _iconRect ??= GetNodeOrNull<TextureRect>("IconRect");
        _titleLabel ??= GetNodeOrNull<Label>("TitleLabel");

        if (_bgTexture == null || _iconRect == null || _titleLabel == null)
            return;

        _animTween?.Kill();

        if (active)
        {
            if (_tickedTexture != null)
                _bgTexture.Texture = _tickedTexture;

            _titleLabel.HorizontalAlignment = HorizontalAlignment.Left;
            _titleLabel.AddThemeFontSizeOverride("font_size", 75);

            if (animate && !Engine.IsEditorHint())
            {
                _animTween = CreateTween().SetParallel(true);
                _animTween.TweenProperty(this, "custom_minimum_size:x", ActiveWidth, 0.22f)
                    .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
                _animTween.TweenProperty(this, "size:x", ActiveWidth, 0.22f)
                    .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);

                _animTween.TweenProperty(_iconRect, "position", ActiveIconPos, 0.22f)
                    .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
                _animTween.TweenProperty(_iconRect, "size", ActiveIconSize, 0.22f)
                    .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);

                _animTween.TweenProperty(_titleLabel, "position", ActiveLabelPos, 0.22f)
                    .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
                _animTween.TweenProperty(_titleLabel, "size", ActiveLabelSize, 0.22f)
                    .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            }
            else
            {
                CustomMinimumSize = new Vector2(ActiveWidth, TabHeight);
                Size = new Vector2(ActiveWidth, TabHeight);
                _iconRect.Position = ActiveIconPos;
                _iconRect.Size = ActiveIconSize;
                _titleLabel.Position = ActiveLabelPos;
                _titleLabel.Size = ActiveLabelSize;
            }
        }
        else
        {
            if (_untickTexture != null)
                _bgTexture.Texture = _untickTexture;

            _titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _titleLabel.AddThemeFontSizeOverride("font_size", _inactiveFontSize);

            if (animate && !Engine.IsEditorHint())
            {
                _animTween = CreateTween().SetParallel(true);
                _animTween.TweenProperty(this, "custom_minimum_size:x", InactiveWidth, 0.2f)
                    .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
                _animTween.TweenProperty(this, "size:x", InactiveWidth, 0.2f)
                    .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);

                _animTween.TweenProperty(_iconRect, "position", _inactiveIconPos, 0.2f)
                    .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
                _animTween.TweenProperty(_iconRect, "size", _inactiveIconSize, 0.2f)
                    .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);

                _animTween.TweenProperty(_titleLabel, "position", _inactiveLabelPos, 0.2f)
                    .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
                _animTween.TweenProperty(_titleLabel, "size", _inactiveLabelSize, 0.2f)
                    .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            }
            else
            {
                CustomMinimumSize = new Vector2(InactiveWidth, TabHeight);
                Size = new Vector2(InactiveWidth, TabHeight);
                _iconRect.Position = _inactiveIconPos;
                _iconRect.Size = _inactiveIconSize;
                _titleLabel.Position = _inactiveLabelPos;
                _titleLabel.Size = _inactiveLabelSize;
            }
        }
    }
}
