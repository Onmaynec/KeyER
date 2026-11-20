namespace KeyER;

/// <summary>
/// Stateful concentration, fatigue/recovery and deliberate short review gestures.
/// Deterministic when supplied a seeded Random. Does not emit any keystrokes itself.
/// </summary>
internal sealed class HumanBehaviorModel
{
    private readonly TypingProfile _profile;
    private readonly Random _random;
    private int _windowLeft;
    private double _focusTarget = 1;
    private double _focusCurrent = 1;
    private double _fatigue;

    public HumanBehaviorModel(TypingProfile profile, Random random)
    {
        _profile = profile;
        _random = random;
        NextWindow();
    }

    public double NextSpeedFactor()
    {
        // Focus does not switch abruptly: the rhythm is eased toward each new window.
        _focusCurrent += (_focusTarget - _focusCurrent) * 0.10;
        _fatigue = Math.Min(1, _fatigue + 1.0 / 2500.0);
        _windowLeft--;
        return _focusCurrent * (1 + _fatigue * _profile.FatiguePercent / 100.0);
    }

    public int NextAttentionBreakMilliseconds()
    {
        if (_windowLeft > 0) return 0;
        NextWindow();
        if (_random.Next(100) >= _profile.AttentionBreakPercent) return 0;
        _fatigue = Math.Max(0, _fatigue - _profile.FocusRecoveryPercent / 100.0);
        return _random.Next(_profile.AttentionBreakMinMilliseconds,
            _profile.AttentionBreakMaxMilliseconds + 1);
    }

    public int NextThinkingPauseMilliseconds()
    {
        return _random.Next(100) < _profile.ThinkingPausePercent
            ? _random.Next(_profile.ThinkingPauseMinMilliseconds,
                _profile.ThinkingPauseMaxMilliseconds + 1)
            : 0;
    }

    public bool ShouldReview() => _random.Next(100) < _profile.LookbackPercent;

    public int NextLookbackLength(int available) =>
        available <= 0 ? 0 : _random.Next(1, Math.Min(available, _profile.LookbackMaxCharacters) + 1);

    public void OnRest(int milliseconds)
    {
        if (milliseconds < 200) return;
        // Longer intentional pauses reduce fatigue but do not reset the whole session.
        _fatigue = Math.Max(0, _fatigue -
            _profile.FocusRecoveryPercent / 100.0 * Math.Min(1, milliseconds / 2000.0));
    }

    private void NextWindow()
    {
        _windowLeft = _random.Next(_profile.FocusMinCharacters,
            _profile.FocusMaxCharacters + 1);
        _focusTarget = 0.87 + _random.NextDouble() * 0.30;
    }
}
