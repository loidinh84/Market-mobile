using Godot;

namespace MarketMobile.UI;

public partial class SidebarTab : Control
{
    [Signal]
    public delegate void TabSelectedEventHandler(int tabIndex);

    [Export] public string Title { get; set; } = "";
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


    public Vector2 GetActiveIconPos()
    {
        string t = Title?.Trim() ?? "";

        if (TabIndex == 0 || t == "Sảnh")
            return new Vector2(220f, 10f);

        if (TabIndex == 1 || t == "Nhân vật")
            return new Vector2(250f, 10f);

        if (TabIndex == 2 || t == "Sự kiện")
            return new Vector2(225f, 10f);

        if (TabIndex == 3 || t == "Cửa hàng")
            return new Vector2(250f, 10f);

        if (TabIndex == 4 || t == "Cài đặt")
            return new Vector2(225f, 10f);

        return new Vector2(250f, 10f);
    }

    private static readonly Vector2 ActiveIconSize = new(140f, 140f);
    private static readonly Vector2 ActiveLabelPos = new(45f, 46f);
    private static readonly Vector2 ActiveLabelSize = new(250f, 66f);
    private static readonly Vector2 InactiveIconPos = new(96f, -30f);
    private static readonly Vector2 InactiveIconSize = new(118f, 118f);
    private static readonly Vector2 InactiveLabelPos = new(0f, 80f);
    private static readonly Vector2 InactiveLabelSize = new(310f, 60f);
    private const int InactiveFontSize = 65;

    private int GetActiveFontSize()
    {
        return (Title != null && Title.Length <= 4) ? 68 : 58;
    }
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

        if (_titleLabel != null)
        {
            if (!string.IsNullOrEmpty(Title))
                _titleLabel.Text = Title;
            else
                Title = _titleLabel.Text;
        }

        if (_clickBtn != null)
            _clickBtn.Pressed += OnClick;
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

        int targetFontSize = active ? GetActiveFontSize() : InactiveFontSize;
        int currentFontSize = _titleLabel.GetThemeFontSize("font_size");
        if (currentFontSize <= 0) currentFontSize = targetFontSize;

        if (active)
        {
            if (_tickedTexture != null)
                _bgTexture.Texture = _tickedTexture;

            _titleLabel.HorizontalAlignment = HorizontalAlignment.Left;
            var activeIconPos = GetActiveIconPos();

            if (animate && !Engine.IsEditorHint())
            {
                _animTween = CreateTween().SetParallel(true);

                _animTween.TweenProperty(this, "custom_minimum_size:x", ActiveWidth, 0.22f)
                    .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
                _animTween.TweenProperty(this, "size:x", ActiveWidth, 0.22f)
                    .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);

                _animTween.TweenProperty(_iconRect, "position", activeIconPos, 0.22f)
                    .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
                _animTween.TweenProperty(_iconRect, "size", ActiveIconSize, 0.22f)
                    .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);

                _animTween.TweenProperty(_titleLabel, "position", ActiveLabelPos, 0.22f)
                    .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
                _animTween.TweenProperty(_titleLabel, "size", ActiveLabelSize, 0.22f)
                    .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
                _animTween.TweenMethod(Callable.From<int>(size => _titleLabel.AddThemeFontSizeOverride("font_size", size)),
                    currentFontSize, targetFontSize, 0.22f);
            }
            else
            {
                CustomMinimumSize = new Vector2(ActiveWidth, TabHeight);
                Size = new Vector2(ActiveWidth, TabHeight);
                _iconRect.Position = activeIconPos;
                _iconRect.Size = ActiveIconSize;
                _titleLabel.Position = ActiveLabelPos;
                _titleLabel.Size = ActiveLabelSize;
                _titleLabel.AddThemeFontSizeOverride("font_size", targetFontSize);
            }
        }
        else
        {
            if (_untickTexture != null)
                _bgTexture.Texture = _untickTexture;

            _titleLabel.HorizontalAlignment = HorizontalAlignment.Center;

            if (animate && !Engine.IsEditorHint())
            {
                _animTween = CreateTween().SetParallel(true);

                _animTween.TweenProperty(this, "custom_minimum_size:x", InactiveWidth, 0.2f)
                    .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
                _animTween.TweenProperty(this, "size:x", InactiveWidth, 0.2f)
                    .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);

                _animTween.TweenProperty(_iconRect, "position", InactiveIconPos, 0.2f)
                    .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
                _animTween.TweenProperty(_iconRect, "size", InactiveIconSize, 0.2f)
                    .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);

                _animTween.TweenProperty(_titleLabel, "position", InactiveLabelPos, 0.2f)
                    .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
                _animTween.TweenProperty(_titleLabel, "size", InactiveLabelSize, 0.2f)
                    .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
                _animTween.TweenMethod(Callable.From<int>(size => _titleLabel.AddThemeFontSizeOverride("font_size", size)),
                    currentFontSize, targetFontSize, 0.2f);
            }
            else
            {
                CustomMinimumSize = new Vector2(InactiveWidth, TabHeight);
                Size = new Vector2(InactiveWidth, TabHeight);
                _iconRect.Position = InactiveIconPos;
                _iconRect.Size = InactiveIconSize;
                _titleLabel.Position = InactiveLabelPos;
                _titleLabel.Size = InactiveLabelSize;
                _titleLabel.AddThemeFontSizeOverride("font_size", targetFontSize);
            }
        }
    }
}
