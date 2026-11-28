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

    private Control _bgTexture;
    private TextureRect _iconRect;
    private Label _titleLabel;
    private Button _clickBtn;
    private Tween _animTween;

    public const float ActiveWidth = 460f;
    public const float InactiveWidth = 380f;
    public const float TabHeight = 158f;

    private const float InactiveIconCenterX = 155f;
    private const float InactiveIconCenterY = 24f;
    private const float ActiveIconCenterY = 79f;

    private string NormalizedTitle => Title?.Trim() ?? "";

    private void SetBgTexture(Texture2D tex)
    {
        if (tex == null || _bgTexture == null) return;
        if (_bgTexture is NinePatchRect np)
            np.Texture = tex;
        else if (_bgTexture is TextureRect tr)
            tr.Texture = tex;
    }

    public Vector2 GetActiveIconSize()
    {
        string t = NormalizedTitle;
        if (TabIndex == 0 || t == "Sảnh")
            return new Vector2(122f, 122f);
        if (TabIndex == 1 || t == "Nhân vật")
            return new Vector2(118f, 118f);
        if (TabIndex == 2 || t == "Sự kiện")
            return new Vector2(106f, 106f);
        if (TabIndex == 3 || t == "Cửa hàng")
            return new Vector2(109f, 109f);
        if (TabIndex == 4 || t == "Túi đồ" || t == "Cài đặt")
            return new Vector2(106f, 106f);
        return new Vector2(112f, 112f);
    }

    public Vector2 GetInactiveIconSize()
    {
        string t = NormalizedTitle;
        if (TabIndex == 0 || t == "Sảnh")
            return new Vector2(110f, 110f);
        if (TabIndex == 1 || t == "Nhân vật")
            return new Vector2(106f, 106f);
        if (TabIndex == 2 || t == "Sự kiện")
            return new Vector2(95f, 95f);
        if (TabIndex == 3 || t == "Cửa hàng")
            return new Vector2(98f, 98f);
        if (TabIndex == 4 || t == "Túi đồ" || t == "Cài đặt")
            return new Vector2(96f, 96f);
        return new Vector2(100f, 100f);
    }

    public Vector2 GetActiveIconPos()
    {
        string t = NormalizedTitle;
        var size = GetActiveIconSize();
        float centerX = 306f;

        if (TabIndex == 0 || t == "Sảnh")
            centerX = 295f;
        else if (TabIndex == 1 || t == "Nhân vật")
            centerX = 318f;
        else if (TabIndex == 2 || t == "Sự kiện")
            centerX = 304f;
        else if (TabIndex == 3 || t == "Cửa hàng")
            centerX = 318f;
        else if (TabIndex == 4 || t == "Túi đồ" || t == "Cài đặt")
            centerX = 298f;

        return new Vector2(centerX - size.X * 0.5f, ActiveIconCenterY - size.Y * 0.5f);
    }

    public Vector2 GetInactiveIconPos()
    {
        var size = GetInactiveIconSize();
        return new Vector2(InactiveIconCenterX - size.X * 0.5f, InactiveIconCenterY - size.Y * 0.5f);
    }

    public Vector2 GetActiveLabelPos()
    {
        string t = NormalizedTitle;
        if (TabIndex == 0 || t == "Sảnh")
            return new Vector2(92f, 40f);
        if (TabIndex == 1 || t == "Nhân vật")
            return new Vector2(56f, 42f);
        if (TabIndex == 2 || t == "Sự kiện")
            return new Vector2(76f, 42f);
        if (TabIndex == 3 || t == "Cửa hàng")
            return new Vector2(54f, 42f);
        if (TabIndex == 4 || t == "Túi đồ" || t == "Cài đặt")
            return new Vector2(92f, 42f);
        return new Vector2(64f, 42f);
    }

    private static readonly Vector2 ActiveLabelSize = new(240f, 66f);
    private static readonly Vector2 InactiveLabelPos = new(0f, 72f);
    private static readonly Vector2 InactiveLabelSize = new(310f, 60f);
    private const int InactiveFontSize = 57;

    private int GetActiveFontSize()
    {
        return (Title != null && Title.Length <= 4) ? 60 : 52;
    }
    public override void _Ready()
    {
        _bgTexture = GetNodeOrNull<Control>("BgTexture");
        _iconRect = GetNodeOrNull<TextureRect>("IconRect");
        _titleLabel = GetNodeOrNull<Label>("TitleLabel");
        _clickBtn = GetNodeOrNull<Button>("ClickBtn");

        _tickedTexture ??= GD.Load<Texture2D>("res://assets/sprites/ui/buttons/btn_sidebar_tab_normal.png");
        _untickTexture ??= GD.Load<Texture2D>("res://assets/sprites/ui/buttons/btn_sidebar_tab_pressed.png");

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
        {
            _clickBtn.Pressed += OnClick;
            _clickBtn.ButtonDown += OnPressVisualDown;
            _clickBtn.ButtonUp += OnPressVisualUp;
            _clickBtn.MouseExited += OnPressVisualUp;
        }
    }

    private void OnPressVisualDown()
    {
        SetBgTexture(_untickTexture);
    }

    private void OnPressVisualUp()
    {
        SetBgTexture(IsActive ? _tickedTexture : _untickTexture);
    }

    private void OnClick()
    {
        EmitSignal(SignalName.TabSelected, TabIndex);
    }

    public void SetActive(bool active, bool animate = true)
    {
        IsActive = active;

        _bgTexture ??= GetNodeOrNull<Control>("BgTexture");
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
            SetBgTexture(_tickedTexture);
            _bgTexture.SelfModulate = Colors.White;

            _titleLabel.HorizontalAlignment = HorizontalAlignment.Left;
            var activeIconPos = GetActiveIconPos();
            var activeIconSize = GetActiveIconSize();
            var activeLabelPos = GetActiveLabelPos();

            if (animate && !Engine.IsEditorHint())
            {
                _animTween = CreateTween().SetParallel(true);

                _animTween.TweenProperty(this, "custom_minimum_size:x", ActiveWidth, 0.22f)
                    .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
                _animTween.TweenProperty(this, "size:x", ActiveWidth, 0.22f)
                    .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);

                _animTween.TweenProperty(_iconRect, "position", activeIconPos, 0.22f)
                    .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
                _animTween.TweenProperty(_iconRect, "size", activeIconSize, 0.22f)
                    .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);

                _animTween.TweenProperty(_titleLabel, "position", activeLabelPos, 0.22f)
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
                _iconRect.Size = activeIconSize;
                _titleLabel.Position = activeLabelPos;
                _titleLabel.Size = ActiveLabelSize;
                _titleLabel.AddThemeFontSizeOverride("font_size", targetFontSize);
            }
        }
        else
        {
            SetBgTexture(_untickTexture);
            _bgTexture.SelfModulate = new Color(0.84f, 0.86f, 0.90f, 0.96f);

            _titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
            var inactiveIconPos = GetInactiveIconPos();
            var inactiveIconSize = GetInactiveIconSize();

            if (animate && !Engine.IsEditorHint())
            {
                _animTween = CreateTween().SetParallel(true);

                _animTween.TweenProperty(this, "custom_minimum_size:x", InactiveWidth, 0.2f)
                    .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
                _animTween.TweenProperty(this, "size:x", InactiveWidth, 0.2f)
                    .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);

                _animTween.TweenProperty(_iconRect, "position", inactiveIconPos, 0.2f)
                    .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
                _animTween.TweenProperty(_iconRect, "size", inactiveIconSize, 0.2f)
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
                _iconRect.Position = inactiveIconPos;
                _iconRect.Size = inactiveIconSize;
                _titleLabel.Position = InactiveLabelPos;
                _titleLabel.Size = InactiveLabelSize;
                _titleLabel.AddThemeFontSizeOverride("font_size", targetFontSize);
            }
        }
    }
}
