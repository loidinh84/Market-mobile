using Godot;

namespace MarketMobile.UI;

public partial class FindFriendItem : VBoxContainer
{
    [Signal]
    public delegate void AddFriendRequestedEventHandler(string playerId);

    public string PlayerId { get; private set; } = string.Empty;
    public string PlayerName { get; private set; } = string.Empty;
    public bool AlreadyFriend { get; private set; }

    private Label _nameLabel;
    private Label _idLabel;
    private Button _addFriendBtn;

    public override void _Ready()
    {
        _nameLabel = GetNodeOrNull<Label>("RowHBox/InfoVBox/NameLabel");
        _idLabel = GetNodeOrNull<Label>("RowHBox/InfoVBox/IdLabel");
        _addFriendBtn = GetNodeOrNull<Button>("RowHBox/AddFriendBtn");

        if (_addFriendBtn != null)
            _addFriendBtn.Pressed += OnAddFriendPressed;

        ApplyData();
    }

    public void Setup(string playerId, string playerName, bool alreadyFriend)
    {
        PlayerId = playerId;
        PlayerName = playerName;
        AlreadyFriend = alreadyFriend;
        ApplyData();
    }

    private void ApplyData()
    {
        if (_nameLabel != null && !string.IsNullOrEmpty(PlayerName))
            _nameLabel.Text = PlayerName;

        if (_idLabel != null && !string.IsNullOrEmpty(PlayerId))
            _idLabel.Text = $"ID: {PlayerId}";

        if (_addFriendBtn != null)
        {
            _addFriendBtn.Text = AlreadyFriend ? "Đã kết bạn" : "Kết bạn";
            _addFriendBtn.Disabled = AlreadyFriend;
            _addFriendBtn.MouseDefaultCursorShape = AlreadyFriend ? CursorShape.Arrow : CursorShape.PointingHand;
        }
    }

    private void OnAddFriendPressed()
    {
        EmitSignal(SignalName.AddFriendRequested, PlayerId);
    }
}
