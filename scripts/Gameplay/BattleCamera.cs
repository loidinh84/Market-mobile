using System.Collections.Generic;
using Godot;

namespace MarketMobile.Gameplay;

/// <summary>
/// Camera 2D khóa kích thước cố định (không cho zoom), chỉ hỗ trợ vuốt lướt (pan)
/// có quán tính và tự động khóa biên theo kích thước TileMapLayer.
/// </summary>
public partial class BattleCamera : Camera2D
{
    public enum InitialFocusMode
    {
        LeftCenter,
        MapCenter,
        RightCenter,
        KeepEditorPosition
    }

    [ExportGroup("Map Bounds")]
    [Export] public TileMapLayer TargetTileMap { get; set; }
    [Export] public bool AutoBoundsFromTileMap { get; set; } = true;
    [Export] public Rect2 ManualBounds { get; set; } = new Rect2(0, 0, 1920, 1080);
    [Export] public Vector2 BoundsPadding { get; set; } = Vector2.Zero;

    [ExportGroup("Camera Size")]
    [Export(PropertyHint.Range, "0.25,3.0,0.05")] public float FixedZoom { get; set; } = 1.0f;

    [ExportGroup("Initial Position")]
    [Export] public InitialFocusMode StartFocus { get; set; } = InitialFocusMode.LeftCenter;
    [Export] public Node2D InitialFocusTarget { get; set; }

    [ExportGroup("Pan / Swipe")]
    [Export] public float DragSensitivity { get; set; } = 1.0f;
    [Export] public float DragThreshold { get; set; } = 8.0f;
    [Export] public bool LockVerticalAxis { get; set; } = false;
    [Export] public bool EnableInertia { get; set; } = true;
    [Export] public float InertiaDamping { get; set; } = 6.5f;
    [Export] public float MaxInertiaSpeed { get; set; } = 4500.0f;

    private readonly Dictionary<int, Vector2> _activeTouches = new();
    private Rect2 _mapBounds;
    private bool _hasValidBounds;
    private bool _emulateTouchFromMouse;

    private int _panTouchIndex = -1;
    private Vector2 _touchStartPos;
    private bool _isDragging;
    private bool _isMouseFallbackDragging;

    private Vector2 _frameDragAccum;
    private Vector2 _velocity;
    private Tween _panTween;

    public override void _Ready()
    {
        Enabled = true;
        AnchorMode = AnchorModeEnum.DragCenter;

        _emulateTouchFromMouse = ProjectSettings.GetSetting(
            "input_devices/pointing/emulate_touch_from_mouse",
            false
        ).AsBool();

        TargetTileMap ??= GetParent()?.GetNodeOrNull<TileMapLayer>("GroundLayer");

        RefreshBoundsFromTileMap();
        ApplyFixedZoom();
        ApplyInitialFocus();

        if (GetViewport() != null)
        {
            GetViewport().SizeChanged += OnViewportSizeChanged;
        }
    }

    public override void _ExitTree()
    {
        if (GetViewport() != null)
        {
            GetViewport().SizeChanged -= OnViewportSizeChanged;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventScreenTouch touch:
                HandleScreenTouch(touch);
                break;

            case InputEventScreenDrag drag:
                HandleScreenDrag(drag);
                break;

            case InputEventMouseButton mouseButton when !_emulateTouchFromMouse:
                HandleMouseFallbackButton(mouseButton);
                break;

            case InputEventMouseMotion mouseMotion when !_emulateTouchFromMouse:
                HandleMouseFallbackMotion(mouseMotion);
                break;
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (dt <= 0f) return;

        if (_isDragging || _isMouseFallbackDragging)
        {
            Vector2 instantVelocity = _frameDragAccum / dt;
            if (instantVelocity.Length() > MaxInertiaSpeed)
            {
                instantVelocity = instantVelocity.Normalized() * MaxInertiaSpeed;
            }

            _velocity = _velocity.Lerp(instantVelocity, Mathf.Clamp(dt * 18f, 0f, 1f));
            _frameDragAccum = Vector2.Zero;
        }
        else if (EnableInertia && _velocity.LengthSquared() > 1f)
        {
            Vector2 step = _velocity * dt;
            if (LockVerticalAxis)
            {
                step.Y = 0f;
            }

            Vector2 targetPos = GlobalPosition + step;
            Vector2 clampedPos = ClampPositionToBounds(targetPos);

            if (!Mathf.IsEqualApprox(clampedPos.X, targetPos.X))
            {
                _velocity.X = 0f;
            }

            if (!Mathf.IsEqualApprox(clampedPos.Y, targetPos.Y))
            {
                _velocity.Y = 0f;
            }

            GlobalPosition = clampedPos;
            _velocity = _velocity.Lerp(Vector2.Zero, Mathf.Clamp(InertiaDamping * dt, 0f, 1f));

            if (_velocity.Length() < 15f)
            {
                _velocity = Vector2.Zero;
            }
        }
    }

    /// <summary>
    /// Tính lại biên giới hạn camera từ vùng tile đã vẽ trong TargetTileMap.
    /// Gọi lại hàm này nếu bạn sửa map trong lúc runtime.
    /// </summary>
    public void RefreshBoundsFromTileMap()
    {
        if (AutoBoundsFromTileMap && TargetTileMap != null && TargetTileMap.TileSet != null)
        {
            Rect2I usedRect = TargetTileMap.GetUsedRect();
            if (usedRect.Size.X > 0 && usedRect.Size.Y > 0)
            {
                Vector2I tileSize = TargetTileMap.TileSet.TileSize;
                Vector2 localTopLeft = new Vector2(usedRect.Position.X * tileSize.X, usedRect.Position.Y * tileSize.Y);
                Vector2 localBottomRight = new Vector2(usedRect.End.X * tileSize.X, usedRect.End.Y * tileSize.Y);

                Vector2 globalA = TargetTileMap.ToGlobal(localTopLeft);
                Vector2 globalB = TargetTileMap.ToGlobal(localBottomRight);

                float minX = Mathf.Min(globalA.X, globalB.X) - BoundsPadding.X;
                float minY = Mathf.Min(globalA.Y, globalB.Y) - BoundsPadding.Y;
                float maxX = Mathf.Max(globalA.X, globalB.X) + BoundsPadding.X;
                float maxY = Mathf.Max(globalA.Y, globalB.Y) + BoundsPadding.Y;

                _mapBounds = new Rect2(minX, minY, maxX - minX, maxY - minY);
                _hasValidBounds = _mapBounds.Size.X > 0f && _mapBounds.Size.Y > 0f;
                return;
            }
        }

        _mapBounds = ManualBounds;
        _hasValidBounds = _mapBounds.Size.X > 0f && _mapBounds.Size.Y > 0f;
    }

    /// <summary>
    /// Trả về khung biên hiện tại của map theo tọa độ World.
    /// </summary>
    public Rect2 GetMapBounds() => _mapBounds;

    /// <summary>
    /// Lướt mượt camera tới một vị trí trên map (ví dụ: về nhà mình, xem nhà địch).
    /// Sẽ tự dừng nếu người chơi chạm tay kéo lại.
    /// </summary>
    public void PanTo(Vector2 worldPosition, float duration = 0.45f)
    {
        CancelPanTween();
        _velocity = Vector2.Zero;

        if (LockVerticalAxis)
        {
            worldPosition.Y = GlobalPosition.Y;
        }

        Vector2 target = ClampPositionToBounds(worldPosition);
        if (duration <= 0f)
        {
            GlobalPosition = target;
            return;
        }

        _panTween = CreateTween();
        _panTween.SetTrans(Tween.TransitionType.Cubic);
        _panTween.SetEase(Tween.EaseType.Out);
        _panTween.TweenProperty(this, "global_position", target, duration);
    }

    /// <summary>
    /// Dịch chuyển tức thì camera tới một vị trí và tự động kẹp trong biên map.
    /// </summary>
    public void SnapTo(Vector2 worldPosition)
    {
        CancelPanTween();
        _velocity = Vector2.Zero;
        GlobalPosition = ClampPositionToBounds(worldPosition);
    }

    private void ApplyFixedZoom()
    {
        float safeZoom = Mathf.Max(0.1f, FixedZoom);

        if (_hasValidBounds)
        {
            Vector2 viewSize = GetViewportRect().Size;
            if (viewSize.X > 0f && viewSize.Y > 0f)
            {
                float fitBoundsZoom = Mathf.Max(
                    viewSize.X / _mapBounds.Size.X,
                    viewSize.Y / _mapBounds.Size.Y
                );
                safeZoom = Mathf.Max(safeZoom, fitBoundsZoom);
            }
        }

        Zoom = new Vector2(safeZoom, safeZoom);
    }

    private void ApplyInitialFocus()
    {
        if (InitialFocusTarget != null)
        {
            GlobalPosition = ClampPositionToBounds(InitialFocusTarget.GlobalPosition);
            return;
        }

        if (!_hasValidBounds || StartFocus == InitialFocusMode.KeepEditorPosition)
        {
            GlobalPosition = ClampPositionToBounds(GlobalPosition);
            return;
        }

        Vector2 center = _mapBounds.GetCenter();
        Vector2 startPos = StartFocus switch
        {
            InitialFocusMode.LeftCenter => new Vector2(_mapBounds.Position.X, center.Y),
            InitialFocusMode.RightCenter => new Vector2(_mapBounds.End.X, center.Y),
            _ => center
        };

        GlobalPosition = ClampPositionToBounds(startPos);
    }

    private void HandleScreenTouch(InputEventScreenTouch touch)
    {
        if (touch.Pressed)
        {
            CancelPanTween();
            _velocity = Vector2.Zero;
            _frameDragAccum = Vector2.Zero;
            _activeTouches[touch.Index] = touch.Position;

            if (_activeTouches.Count == 1)
            {
                _panTouchIndex = touch.Index;
                _touchStartPos = touch.Position;
                _isDragging = false;
            }
            else
            {
                // Chạm >= 2 ngón: không zoom và tạm ngưng pan để tránh giật camera
                _panTouchIndex = -1;
                _isDragging = false;
            }
        }
        else
        {
            _activeTouches.Remove(touch.Index);

            if (touch.Index == _panTouchIndex)
            {
                _panTouchIndex = -1;
                _isDragging = false;
            }

            // Khi nhấc bớt ngón về lại còn 1 ngón, đặt lại mốc kéo để tránh nhảy vị trí
            if (_activeTouches.Count == 1)
            {
                foreach (var kvp in _activeTouches)
                {
                    _panTouchIndex = kvp.Key;
                    _touchStartPos = kvp.Value;
                    _isDragging = false;
                    _velocity = Vector2.Zero;
                    _frameDragAccum = Vector2.Zero;
                    break;
                }
            }
        }
    }

    private void HandleScreenDrag(InputEventScreenDrag drag)
    {
        _activeTouches[drag.Index] = drag.Position;

        if (_activeTouches.Count != 1 || drag.Index != _panTouchIndex)
        {
            return;
        }

        if (!_isDragging)
        {
            if (drag.Position.DistanceTo(_touchStartPos) < DragThreshold)
            {
                return;
            }

            _isDragging = true;
            CancelPanTween();
        }

        ApplyPanDelta(drag.Relative);
        GetViewport().SetInputAsHandled();
    }

    private void HandleMouseFallbackButton(InputEventMouseButton mouseButton)
    {
        if (mouseButton.ButtonIndex != MouseButton.Left)
        {
            return;
        }

        if (mouseButton.Pressed)
        {
            CancelPanTween();
            _velocity = Vector2.Zero;
            _frameDragAccum = Vector2.Zero;
            _touchStartPos = mouseButton.Position;
            _isMouseFallbackDragging = false;
        }
        else
        {
            _isMouseFallbackDragging = false;
        }
    }

    private void HandleMouseFallbackMotion(InputEventMouseMotion mouseMotion)
    {
        if ((mouseMotion.ButtonMask & MouseButtonMask.Left) == 0)
        {
            _isMouseFallbackDragging = false;
            return;
        }

        if (!_isMouseFallbackDragging)
        {
            if (mouseMotion.Position.DistanceTo(_touchStartPos) < DragThreshold)
            {
                return;
            }

            _isMouseFallbackDragging = true;
            CancelPanTween();
        }

        ApplyPanDelta(mouseMotion.Relative);
        GetViewport().SetInputAsHandled();
    }

    private void ApplyPanDelta(Vector2 screenRelative)
    {
        Vector2 worldDelta = new Vector2(
            -screenRelative.X / Zoom.X * DragSensitivity,
            LockVerticalAxis ? 0f : -screenRelative.Y / Zoom.Y * DragSensitivity
        );

        Vector2 nextPos = ClampPositionToBounds(GlobalPosition + worldDelta);
        Vector2 actualDelta = nextPos - GlobalPosition;

        GlobalPosition = nextPos;
        _frameDragAccum += actualDelta;
    }

    private Vector2 ClampPositionToBounds(Vector2 position)
    {
        if (!_hasValidBounds)
        {
            return position;
        }

        Vector2 halfView = GetViewportRect().Size * 0.5f / Zoom;

        float minX = _mapBounds.Position.X + halfView.X;
        float maxX = _mapBounds.End.X - halfView.X;
        float clampedX = minX > maxX
            ? (_mapBounds.Position.X + _mapBounds.End.X) * 0.5f
            : Mathf.Clamp(position.X, minX, maxX);

        float minY = _mapBounds.Position.Y + halfView.Y;
        float maxY = _mapBounds.End.Y - halfView.Y;
        float clampedY = minY > maxY
            ? (_mapBounds.Position.Y + _mapBounds.End.Y) * 0.5f
            : Mathf.Clamp(position.Y, minY, maxY);

        return new Vector2(clampedX, clampedY);
    }

    private void OnViewportSizeChanged()
    {
        ApplyFixedZoom();
        GlobalPosition = ClampPositionToBounds(GlobalPosition);
    }

    private void CancelPanTween()
    {
        if (_panTween != null && _panTween.IsValid())
        {
            _panTween.Kill();
        }

        _panTween = null;
    }
}
