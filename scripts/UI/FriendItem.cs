using Godot;

namespace MarketMobile.UI;

public partial class FriendItem : VBoxContainer
{
    [Signal]
    public delegate void ChatRequestedEventHandler(string friendId, string friendName);

    [Signal]
    public delegate void DeleteRequestedEventHandler(string friendId, string friendName);

    public string FriendId { get; private set; } = string.Empty;
    public string FriendName { get; private set; } = string.Empty;

    private TextureRect _avatarTexture;
    private Label _nameLabel;
    private TextureButton _chatBtn;
    private TextureButton _deleteBtn;

    public override void _Ready()
    {
        _avatarTexture = GetNodeOrNull<TextureRect>("RowHBox/AvatarFrame/AvatarTexture");
        _nameLabel = GetNodeOrNull<Label>("RowHBox/NameLabel");
        _chatBtn = GetNodeOrNull<TextureButton>("RowHBox/ChatBtn");
        _deleteBtn = GetNodeOrNull<TextureButton>("RowHBox/DeleteBtn");

        if (_chatBtn != null)
            _chatBtn.Pressed += OnChatPressed;

        if (_deleteBtn != null)
            _deleteBtn.Pressed += OnDeletePressed;

        ApplyData();
    }

    public void Setup(string friendId, string friendName, Texture2D avatar = null)
    {
        FriendId = friendId;
        FriendName = friendName;

        if (_avatarTexture != null && avatar != null)
            _avatarTexture.Texture = avatar;

        ApplyData();
    }

    private void ApplyData()
    {
        if (_nameLabel != null && !string.IsNullOrEmpty(FriendName))
            _nameLabel.Text = FriendName;
    }

    private void OnChatPressed()
    {
        EmitSignal(SignalName.ChatRequested, FriendId, FriendName);
    }

    private void OnDeletePressed()
    {
        EmitSignal(SignalName.DeleteRequested, FriendId, FriendName);
    }
}
