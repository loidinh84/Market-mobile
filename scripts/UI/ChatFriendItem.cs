using Godot;

namespace MarketMobile.UI;

public partial class ChatFriendItem : VBoxContainer
{
    [Signal]
    public delegate void SelectedEventHandler(string friendId, string friendName);

    public string FriendId { get; private set; } = string.Empty;
    public string FriendName { get; private set; } = string.Empty;

    private ColorRect _highlightBg;
    private ShaderMaterial _highlightMat;
    private ColorRect _hoverBg;
    private Label _nameLabel;
    private bool _isSelected;
    private Tween _stateTween;
    private Tween _sweepTween;

    public override void _Ready()
    {
        _highlightBg = GetNodeOrNull<ColorRect>("RowRoot/HighlightBg");
        _highlightMat = _highlightBg?.Material as ShaderMaterial;
        _hoverBg = GetNodeOrNull<ColorRect>("RowRoot/HoverBg");
        _nameLabel = GetNodeOrNull<Label>("RowRoot/ContentHBox/NameLabel");

        var clickBtn = GetNodeOrNull<Button>("RowRoot/ClickBtn");
        if (clickBtn != null)
        {
            clickBtn.Pressed += () => EmitSignal(SignalName.Selected, FriendId, FriendName);
            clickBtn.MouseEntered += () => AnimateHover(!_isSelected);
            clickBtn.MouseExited += () => AnimateHover(false);
        }

        ApplyName();
    }

    public void Setup(string friendId, string friendName)
    {
        FriendId = friendId;
        FriendName = friendName;
        ApplyName();
    }

    public void SetSelected(bool selected, bool animate)
    {
        _isSelected = selected;
        AnimateHover(false);

        float targetHighlight = selected ? 1.0f : 0.0f;
        float targetNameAlpha = selected ? 1.0f : 0.78f;

        _stateTween?.Kill();
        if (!animate)
        {
            if (_highlightBg != null) _highlightBg.Modulate = new Color(1, 1, 1, targetHighlight);
            if (_nameLabel != null) _nameLabel.Modulate = new Color(1, 1, 1, targetNameAlpha);
        }
        else
        {
            _stateTween = CreateTween().SetParallel(true)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.Out);
            if (_highlightBg != null)
                _stateTween.TweenProperty(_highlightBg, "modulate:a", targetHighlight, selected ? 0.22f : 0.14f);
            if (_nameLabel != null)
                _stateTween.TweenProperty(_nameLabel, "modulate:a", targetNameAlpha, 0.18f);
        }

        if (selected)
            TriggerSweep();
    }

    private void ApplyName()
    {
        if (_nameLabel != null && !string.IsNullOrEmpty(FriendName))
            _nameLabel.Text = FriendName;
    }

    private void AnimateHover(bool show)
    {
        if (_hoverBg == null) return;
        var tween = CreateTween().SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(_hoverBg, "color:a", show ? 0.10f : 0.0f, 0.14f);
    }

    private void TriggerSweep()
    {
        if (_highlightMat == null) return;
        _sweepTween?.Kill();
        _highlightMat.SetShaderParameter("sweep_pos", -0.35f);
        _sweepTween = CreateTween().SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        _sweepTween.TweenMethod(
            Callable.From<float>(v => _highlightMat.SetShaderParameter("sweep_pos", v)),
            -0.35f,
            1.35f,
            0.38f
        );
    }
}
