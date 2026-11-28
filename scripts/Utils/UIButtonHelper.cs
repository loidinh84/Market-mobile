using System;
using Godot;

namespace MarketMobile.Utils;

public enum ButtonPivotMode
{
    Center,     // Thu nhỏ đều về tâm (dành cho nút nổi độc lập: Nâng cấp, +, Avatar...)
    RightEdge,  // Neo giữ chặt mép phải vào viền màn hình (dành cho nút bám mép phải: Bắt đầu, Bạn bè)
    LeftEdge    // Neo giữ chặt mép trái
}

public static class UIButtonHelper
{
    /// <summary>
    /// Gắn hiệu ứng nhún (squish/bounce) khi nhấn nút và loại bỏ hiệu ứng hover/focus lưu lại trên mobile.
    /// Nếu targetControl được truyền vào (ví dụ Card cha), hiệu ứng nhún sẽ tác động lên toàn bộ targetControl.
    /// </summary>
    public static void AttachBounceEffect(BaseButton button, float pressedScale = 0.94f, ButtonPivotMode pivotMode = ButtonPivotMode.Center, Control targetControl = null)
    {
        if (button == null) return;
        Control target = targetControl ?? button;

        // 1. Tắt FocusMode để tránh lưu viền focus hoặc giữ trạng thái sau khi chạm trên điện thoại
        button.FocusMode = Control.FocusModeEnum.None;

        // 2. Bỏ hover: gán style hover bằng style normal để không bị đổi màu dính hover trên màn hình cảm ứng
        var normalStyle = button.GetThemeStylebox("normal");
        if (normalStyle != null)
        {
            button.AddThemeStyleboxOverride("hover", normalStyle);
            button.AddThemeStyleboxOverride("hover_pressed", normalStyle);
        }

        // Bỏ hover màu chữ: gán font_hover_color bằng font_color để chữ không bị đổi màu sáng khi chuột lướt qua
        if (button is Button b)
        {
            var fontColor = b.GetThemeColor("font_color");
            b.AddThemeColorOverride("font_hover_color", fontColor);
            b.AddThemeColorOverride("font_hover_pressed_color", fontColor);
            b.AddThemeColorOverride("font_focus_color", fontColor);
        }

        // 3. Đặt tâm PivotOffset theo vị trí neo của target
        void UpdatePivot()
        {
            switch (pivotMode)
            {
                case ButtonPivotMode.RightEdge:
                    // Neo chặt mép phải: Khi ấn nhún, mép phải giữ nguyên 100% không bao giờ bị hở hay mất mép
                    target.PivotOffset = new Vector2(target.Size.X, target.Size.Y / 2f);
                    break;
                case ButtonPivotMode.LeftEdge:
                    target.PivotOffset = new Vector2(0f, target.Size.Y / 2f);
                    break;
                default:
                    // Mặc định: Căn giữa tâm node
                    target.PivotOffset = target.Size / 2f;
                    break;
            }
        }

        target.Resized += UpdatePivot;
        target.CallDeferred(Control.MethodName.Set, Control.PropertyName.PivotOffset,
            pivotMode == ButtonPivotMode.RightEdge ? new Vector2(target.Size.X, target.Size.Y / 2f) : target.Size / 2f);

        Tween currentTween = null;

        // 4. KHI ĐÈ NÚT (ButtonDown):
        // Nút nhún nhẹ xuống và TIẾP TỤC GIỮ TRẠNG THÁI LÚN NÀY chừng nào ngón tay còn đè trên nút.
        button.ButtonDown += () =>
        {
            if (button.Disabled) return;
            UpdatePivot();
            currentTween?.Kill();
            currentTween = target.CreateTween();
            currentTween.TweenProperty(target, "scale", new Vector2(pressedScale, pressedScale), 0.08f)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        };

        // 5. KHI NHẢ NÚT (ButtonUp):
        // Nút nảy bật trở lại kích thước chuẩn (có hiệu ứng lò xo nhẹ) và kích hoạt hành động.
        button.ButtonUp += () =>
        {
            if (button.Disabled) return;
            currentTween?.Kill();
            currentTween = target.CreateTween();
            currentTween.TweenProperty(target, "scale", Vector2.One, 0.12f)
                .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        };

        // 6. KHI ĐANG ĐÈ NÚT MÀ TRƯỢT NGÓN TAY RA NGOÀI (Hủy bấm):
        // Nút tự động bung lại kích thước bình thường và không kích hoạt hành động.
        button.MouseExited += () =>
        {
            if (target.Scale != Vector2.One)
            {
                currentTween?.Kill();
                currentTween = target.CreateTween();
                currentTween.TweenProperty(target, "scale", Vector2.One, 0.1f)
                    .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            }
        };
    }

    /// <summary>
    /// Tự động duyệt qua cây node và gắn hiệu ứng nhún cho toàn bộ các nút bấm (Button / TextureButton).
    /// </summary>
    public static void AttachToAllButtons(Node root, float pressedScale = 0.94f)
    {
        if (root == null) return;

        
        if (root is BaseButton btn && !btn.Name.ToString().Contains("ClickBtn"))
        {
            string btnName = btn.Name.ToString();
            
            ButtonPivotMode mode = (btnName == "StartBtn" || btnName == "FriendsBtn")
                ? ButtonPivotMode.RightEdge
                : ButtonPivotMode.Center;
            float targetScale = btnName == "StartBtn" ? 0.975f : pressedScale;

            
            if (btn.GetParent() is PanelContainer parentCard && parentCard.Name.ToString().StartsWith("Card"))
            {
                AttachBounceEffect(btn, targetScale, mode, parentCard);
            }
            else
            {
                AttachBounceEffect(btn, targetScale, mode);
            }
        }

        foreach (Node child in root.GetChildren())
        {
            AttachToAllButtons(child, pressedScale);
        }
    }

    /// <summary>
    /// </summary>
    /// <param name="button">Nút cần gắn</param>
    /// <param name="onAction">Hành động cần thực thi</param>
    /// <param name="initialDelay"></param>
    /// <param name="repeatInterval"></param>
    /// <param name="canExecute"></param>
    public static void AttachHoldRepeat(BaseButton button, Action onAction, float initialDelay = 2.0f, float repeatInterval = 0.25f, Func<bool> canExecute = null)
    {
        if (button == null || onAction == null) return;

        ulong holdToken = 0;
        bool didAutoRepeat = false;

        async void StartHold()
        {
            if (canExecute != null && !canExecute()) return;
            ulong currentToken = ++holdToken;
            didAutoRepeat = false;

            await button.ToSignal(button.GetTree().CreateTimer(initialDelay), SceneTreeTimer.SignalName.Timeout);

            while (holdToken == currentToken && button.IsInsideTree() && button.IsPressed() && (canExecute == null || canExecute()))
            {
                didAutoRepeat = true;
                onAction.Invoke();
                await button.ToSignal(button.GetTree().CreateTimer(repeatInterval), SceneTreeTimer.SignalName.Timeout);
            }
        }

        void StopHold()
        {
            holdToken++;
        }

        button.ButtonDown += StartHold;
        button.ButtonUp += StopHold;
        button.MouseExited += StopHold;

        // Xử lý sự kiện Pressed khi nhấp nhả thông thường:
        // Nếu vừa chạy xong chuỗi auto-repeat thì bỏ qua lần click này để không bị nhảy thêm 1 số ngoài ý muốn
        button.Pressed += () =>
        {
            if (didAutoRepeat)
            {
                didAutoRepeat = false;
                return;
            }
            if (canExecute == null || canExecute())
            {
                onAction.Invoke();
            }
        };
    }
}
