using Godot;
using MarketMobile.Core;

namespace MarketMobile.UI;

public partial class LobbyMatchmakingController : Node
{
	[Signal]
	public delegate void MatchmakingStateChangedEventHandler(bool isSearching);

	[Signal]
	public delegate void MatchmakingTickEventHandler(int elapsedSeconds);

	[Signal]
	public delegate void MatchFoundEventHandler(int playType, int teamMode, string mapId);

	[Signal]
	public delegate void PenaltyTickEventHandler(int remainingSeconds);

	[Signal]
	public delegate void PenaltyEndedEventHandler();

	public bool IsSearching { get; private set; }
	public bool IsInPenalty => _penaltyRemaining > 0f;
	public int PenaltySecondsRemaining => Mathf.Max(0, Mathf.CeilToInt(_penaltyRemaining));
	public PlayMatchType ActivePlayType { get; private set; } = PlayMatchType.Normal;
	public TeamMode ActiveTeamMode { get; private set; } = TeamMode.Solo1v1;
	public string ActiveMapId { get; private set; } = "map_cho_dau_moi";
	public int ElapsedSeconds => _lastReportedSecond;

	private float _elapsed = 0f;
	private float _targetDuration = 3.5f;
	private int _lastReportedSecond = 0;
	private float _penaltyRemaining = 0f;
	private int _lastReportedPenalty = 0;

	public override void _Process(double delta)
	{
		float dt = (float)delta;

		if (_penaltyRemaining > 0f)
		{
			_penaltyRemaining = Mathf.Max(0f, _penaltyRemaining - dt);
			int ceilSec = Mathf.CeilToInt(_penaltyRemaining);
			if (ceilSec != _lastReportedPenalty)
			{
				_lastReportedPenalty = ceilSec;
				EmitSignal(SignalName.PenaltyTick, _lastReportedPenalty);
			}
			if (_penaltyRemaining <= 0f)
			{
				_lastReportedPenalty = 0;
				EmitSignal(SignalName.PenaltyEnded);
			}
		}

		if (!IsSearching) return;

		_elapsed += dt;
		int wholeSec = Mathf.FloorToInt(_elapsed);
		if (wholeSec != _lastReportedSecond)
		{
			_lastReportedSecond = wholeSec;
			EmitSignal(SignalName.MatchmakingTick, _lastReportedSecond);
		}

		if (_elapsed >= _targetDuration)
		{
			IsSearching = false;
			EmitSignal(SignalName.MatchmakingStateChanged, false);
			EmitSignal(SignalName.MatchFound, (int)ActivePlayType, (int)ActiveTeamMode, ActiveMapId);
		}
	}

	public void StartSearch(PlayMatchType playType, TeamMode teamMode, string mapId)
	{
		if (IsInPenalty) return;
		ActivePlayType = playType;
		ActiveTeamMode = teamMode;
		ActiveMapId = string.IsNullOrEmpty(mapId) ? "map_cho_dau_moi" : mapId;
		_elapsed = 0f;
		_lastReportedSecond = 0;
		_targetDuration = playType == PlayMatchType.Bot ? 1.5f : 3.5f;
		IsSearching = true;

		EmitSignal(SignalName.MatchmakingStateChanged, true);
		EmitSignal(SignalName.MatchmakingTick, 0);
	}

	public void CancelSearch()
	{
		if (!IsSearching) return;
		IsSearching = false;
		_elapsed = 0f;
		_lastReportedSecond = 0;
		EmitSignal(SignalName.MatchmakingStateChanged, false);
	}

	public void ApplyDeclinePenalty(float seconds = 30f)
	{
		CancelSearch();
		_penaltyRemaining = Mathf.Max(0f, seconds);
		_lastReportedPenalty = Mathf.CeilToInt(_penaltyRemaining);
		EmitSignal(SignalName.PenaltyTick, _lastReportedPenalty);
	}

	public static string FormatElapsed(int totalSeconds)
	{
		int mins = Mathf.Max(0, totalSeconds) / 60;
		int secs = Mathf.Max(0, totalSeconds) % 60;
		return $"{mins:D2}:{secs:D2}";
	}
}
