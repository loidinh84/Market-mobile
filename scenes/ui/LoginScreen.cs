using Godot;
using System;

public partial class LoginScreen : Control
{
	private LineEdit _accountInput;
	private LineEdit _passwordInput;
	private Button _loginButton;
	private Button _registerButton;
	private Button _googleButton;

	public override void _Ready()
	{
		// Lấy tham chiếu đến các node con
		_accountInput = GetNode<LineEdit>("CenterContainer/MainVBox/AccountBox/AccountInput");
		_passwordInput = GetNode<LineEdit>("CenterContainer/MainVBox/PasswordBox/PasswordInput");
		_loginButton = GetNode<Button>("CenterContainer/MainVBox/ButtonsRow/ButtonsVBox/Login");
		_registerButton = GetNode<Button>("CenterContainer/MainVBox/ButtonsRow/ButtonsVBox/Register");
		_googleButton = GetNode<Button>("CenterContainer/MainVBox/ButtonsRow/ButtonsVBox/LoginWithGoogle");

		// Đăng ký sự kiện click nút
		_loginButton.Pressed += OnLoginButtonPressed;
		_registerButton.Pressed += OnRegisterButtonPressed;
		_googleButton.Pressed += OnGoogleButtonPressed;
	}

	private void OnLoginButtonPressed()
	{
		string username = _accountInput.Text.Trim();
		string password = _passwordInput.Text;

		if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
		{
			GD.Print("Vui lòng nhập đầy đủ tài khoản và mật khẩu!");
			return;
		}

		GD.Print($"Đăng nhập thành công với tài khoản: {username}");
	}

	private void OnRegisterButtonPressed()
	{
		GD.Print("Mở form đăng ký tài khoản mới...");
	}

	private void OnGoogleButtonPressed()
	{
		GD.Print("Xử lý đăng nhập bằng Google...");
	}
}
