using Godot;

namespace MarketMobile.UI;

public partial class FriendRequestItem : VBoxContainer
{
    [Signal]
    public delegate void AcceptedEventHandler(string playerId);

    [Signal]
    public delegate void DeclinedEventHandler(string playerId);

    public string PlayerId { get; private set; } = string.Empty;
    public string PlayerName { get; private set; } = string.Empty;

    private Label _nameLabel;
    private Label _idLabel;

    public override void _Ready()
    {
        _nameLabel = GetNodeOrNull<Label>("RowHBox/InfoVBox/NameLabel");
        _idLabel = GetNodeOrNull<Label>("RowHBox/InfoVBox/IdLabel");

        var acceptBtn = GetNodeOrNull<Button>("RowHBox/AcceptBtn");
        var declineBtn = GetNodeOrNull<Button>("RowHBox/DeclineBtn");

        if (acceptBtn != null) acceptBtn.Pressed += () => EmitSignal(SignalName.Accepted, PlayerId);
        if (declineBtn != null) declineBtn.Pressed += () => EmitSignal(SignalName.Declined, PlayerId);

        ApplyData();
    }

    public void Setup(string playerId, string playerName)
    {
        PlayerId = playerId;
        PlayerName = playerName;
        ApplyData();
    }

    private void ApplyData()
    {
        if (_nameLabel != null && !string.IsNullOrEmpty(PlayerName))
            _nameLabel.Text = PlayerName;

        if (_idLabel != null && !string.IsNullOrEmpty(PlayerId))
            _idLabel.Text = $"ID: {PlayerId}";
    }
}
