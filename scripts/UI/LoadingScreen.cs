using Godot;
using System;

public partial class LoadingScreen : Control
{
	private ProgressBar _progressBar;
	private Label _statusLabel;

	public override void _Ready()
	{
		_progressBar = GetNode<ProgressBar>("CenterContainer/MainVBox/ProgressBar");
		_statusLabel = GetNode<Label>("CenterContainer/MainVBox/StatusLabel");

		_progressBar.Value = 0;
		StartLoading();
	}

	private void StartLoading()
	{
		// Dùng Tween để tạo chuyển động thanh mượt mà trong 2.5 giây
		Tween tween = CreateTween();
		
		// Chạy thanh giá trị từ 0 lên 100
		tween.TweenProperty(_progressBar, "value", 100.0, 2.5)
			 .SetTrans(Tween.TransitionType.Cubic)
			 .SetEase(Tween.EaseType.Out);

		// Khi chạy xong 2.5 giây thì gọi hàm hoàn thành
		tween.TweenCallback(Callable.From(OnLoadingFinished)).SetDelay(2.5);
	}

	public override void _Process(double delta)
	{
		// Giữ nguyên dòng trạng thái ở trên
		if (_progressBar.Value < 100)
		{
			_statusLabel.Text = "Đang tải tiến trình...";
		}
	}

	private void OnLoadingFinished()
	{
		_statusLabel.Text = "Đang vào sảnh game...";
		// Tự động chuyển mượt sang màn hình Đăng nhập (hoặc màn tiếp theo)
		GetTree().ChangeSceneToFile("res://scenes/ui/login_screen.tscn");
	}
}
