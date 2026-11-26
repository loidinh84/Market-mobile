using Godot;

namespace MarketMobile.UI;

/// <summary>
/// Component thanh hiển thị Vàng dùng chung giữa các màn hình (Sảnh, Nhân vật, Cửa hàng...).
/// </summary>
public partial class CoinBar : PanelContainer
{
    [Signal]
    public delegate void AddCoinPressedEventHandler();

    [Export] public int InitialCoins { get; set; } = 123456;
    [Export] public bool ShowAddButton { get; set; } = true;
    [Export] public Texture2D CurrencyIcon { get; set; }
    [Export] public Vector2 IconSize { get; set; } = new(48f, 48f);

    private TextureRect _iconRect;
    private Label _coinNumber;
    private Button _addCoinBtn;
    private Tween _coinTween;
    private int _displayedCoins;
    private int _targetCoins;
    private bool _hasExplicitValue = false;

    public int Coins => _targetCoins;

    public override void _Ready()
    {
        _iconRect = GetNodeOrNull<TextureRect>("HBox/CoinIcon");
        _coinNumber = GetNodeOrNull<Label>("HBox/CoinNumber");
        _addCoinBtn = GetNodeOrNull<Button>("HBox/AddCoinBtn");

        if (_iconRect != null)
        {
            if (CurrencyIcon != null) _iconRect.Texture = CurrencyIcon;
            _iconRect.CustomMinimumSize = IconSize;
        }

        if (_addCoinBtn != null)
        {
            _addCoinBtn.Visible = ShowAddButton;
            _addCoinBtn.Pressed += () => EmitSignal(SignalName.AddCoinPressed);
        }

        if (!_hasExplicitValue)
        {
            _targetCoins = Mathf.Max(0, InitialCoins);
            _displayedCoins = _targetCoins;
        }

        UpdateCoinLabel(_displayedCoins);
    }

    /// <summary>
    /// Cập nhật số vàng hiển thị, hỗ trợ hiệu ứng nhảy số mượt và nháy nhẹ khi thay đổi.
    /// </summary>
    public void SetCoins(int amount, bool animate = true)
    {
        int clamped = Mathf.Max(0, amount);
        _hasExplicitValue = true;

        if (clamped == _targetCoins && _coinNumber != null)
        {
            UpdateCoinLabel(clamped);
            return;
        }

        int previousCoins = _displayedCoins;
        _targetCoins = clamped;

        if (_coinNumber == null || !animate || !IsInsideTree())
        {
            _displayedCoins = clamped;
            UpdateCoinLabel(clamped);
            return;
        }

        _coinTween?.Kill();
        _coinTween = CreateTween().SetParallel(true);
        _coinTween.TweenMethod(Callable.From<int>(val =>
        {
            _displayedCoins = val;
            UpdateCoinLabel(val);
        }), previousCoins, clamped, 0.35f)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);

        // Hiệu ứng nháy màu nhẹ để người chơi nhận biết số dư vừa thay đổi
        Color flashColor = clamped < previousCoins
            ? new Color(1f, 0.65f, 0.45f, 1f)
            : new Color(0.55f, 1f, 0.65f, 1f);
        _coinNumber.Modulate = flashColor;
        _coinTween.TweenProperty(_coinNumber, "modulate", Colors.White, 0.38f)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
    }

    private void UpdateCoinLabel(int value)
    {
        if (_coinNumber != null)
        {
            _coinNumber.Text = $"{value:N0}";
        }
    }
}
