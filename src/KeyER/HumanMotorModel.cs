namespace KeyER;

/// <summary>
/// Humanizer 3.0: one bounded motor/attention timeline for Text and Code.
/// It models *statistical* keyboard motion, not a claim of real human identity.
/// A seeded Random makes its outputs reproducible in regression tests.
/// No keylogging, user recordings, network, or learned personal biometric data.
/// </summary>
internal sealed class HumanMotorModel
{
    internal readonly record struct KeySite(double X, double Y, int Finger, int Hand, int Layout);

    private static readonly Dictionary<char, KeySite> KeyMap = CreateMap();
    private static readonly HashSet<string> FamiliarPairs = new(StringComparer.OrdinalIgnoreCase)
    {
        "th", "he", "in", "er", "an", "re", "on", "at", "en", "nd", "st",
        "es", "or", "te", "ed", "is", "it", "al", "ar", "to", "ou", "ch",
        "ing", "the", "and",
        "ст", "но", "на", "ов", "ен", "ни", "ра", "пр", "то", "ко",
        "по", "во", "ро", "ер", "ос", "ре", "ал", "ли", "ка", "не",
        "та", "ва", "го", "ор", "ла", "от", "за"
    };

    private readonly TypingProfile _p;
    private readonly Random _random;
    private double _smoothed;
    private double _rhythmNoise;
    private double _personalTempo;
    private int _burstRemaining;
    private bool _burst;
    private int _charsSinceRest;

    public HumanMotorModel(TypingProfile profile, Random random)
    {
        _p = profile;
        _random = random;
        _smoothed = 60000.0 / Math.Max(20, profile.CharactersPerMinute);
        // A session has stable small differences; no persistence or biometric signature.
        _personalTempo = 0.955 + _random.NextDouble() * 0.09;
        _burstRemaining = _random.Next(7, 19);
    }

    internal static bool TryLocate(char letter, out KeySite location) =>
        KeyMap.TryGetValue(char.ToLowerInvariant(letter), out location);

    /// <summary>Relative motor effort at a transition; no timing side effects.</summary>
    internal static double TransitionFactor(char previous, char next, int travelInfluence)
    {
        if (!TryLocate(previous, out var a) || !TryLocate(next, out var b) ||
            a.Layout != b.Layout)
            return 1.0;

        var intensity = Math.Clamp(travelInfluence / 100.0, 0, 1.0);
        var distance = Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
        var factor = 1.0 + Math.Min(4.0, distance) * 0.053;

        if (a.X == b.X && a.Y == b.Y) factor += 0.19; // repeated key
        else if (a.Finger == b.Finger) factor += 0.15; // same-finger stretch
        else if (a.Hand != b.Hand) factor -= 0.115;   // alternating hands
        else if (distance < 1.65) factor -= 0.035;    // short adjacent roll

        return 1.0 + (factor - 1.0) * intensity;
    }

    /// <summary>
    /// Source-character inter-key interval with a short/long-word context,
    /// syntactic context and a slowly drifting correlated personal rhythm.
    /// </summary>
    public int NextDelay(char current, char previous, int wordLength,
        double contextFactor = 1.0, double focusFactor = 1.0)
    {
        var strength = _p.HumanizerStrength / 100.0;
        if (--_burstRemaining <= 0)
        {
            _burst = _random.Next(100) < _p.BurstPercent;
            _burstRemaining = _random.Next(5, 21);
        }

        var factor = Math.Clamp(contextFactor, 0.35, 3.5);
        if (wordLength is > 0 and <= 4)
            factor *= 100.0 / _p.ShortWordSpeedPercent;
        else if (wordLength >= 9)
            factor *= 100.0 / _p.LongWordSpeedPercent;

        if (char.IsDigit(current)) factor *= _p.DigitSlowdownPercent / 100.0;
        else if (char.IsPunctuation(current)) factor *= _p.PunctuationSlowdownPercent / 100.0;
        else if (char.IsSymbol(current) ||
                 (char.IsLetter(current) && !BasicLetter(current)))
            factor *= _p.SpecialSlowdownPercent / 100.0;

        if (char.IsLetter(current) && char.IsLetter(previous) &&
            FamiliarPairs.Contains(string.Concat(previous, current)))
            factor *= 1 - _p.FamiliarPairBoostPercent / 100.0;

        factor *= 1.0 + (TransitionFactor(previous, current, _p.KeyTravelInfluence)
            - 1.0) * strength;

        // A real rhythm has memory: neighboring latencies covary rather than
        // sampling each event independently from a uniform distribution.
        var inertia = _p.RhythmMemoryPercent / 100.0;
        var innovation = NextGaussian();
        _rhythmNoise = _rhythmNoise * inertia + innovation * Math.Sqrt(1 - inertia * inertia);
        var variation = _p.SpeedVariationPercent / 100.0;
        var correlated = Math.Exp(Math.Clamp(_rhythmNoise, -2.3, 2.3) *
            variation * (0.12 + 0.25 * strength));
        var independent = 1.0 + (_random.NextDouble() * 2 - 1) *
            variation * (1.0 - 0.63 * strength);
        factor *= correlated * independent * _personalTempo;

        if (_burst) factor *= 1.0 - 0.22 * (0.4 + strength * 0.6);
        if (previous == ' ' && char.IsLetter(current))
            factor *= 1.0 + 0.065 * strength; // opening a fresh word
        if (_charsSinceRest < 3)
            factor *= 1.0 + (3 - _charsSinceRest) * 0.035 * strength;
        if (char.IsUpper(current) && !char.IsUpper(previous))
            factor *= 1.0 + 0.115 * strength;
        _charsSinceRest++;

        var baseline = 60000.0 / _p.CharactersPerMinute;
        var target = Math.Clamp(baseline * factor * Math.Clamp(focusFactor, 0.5, 2.5),
            12.0, 3500.0);
        var response = _p.SpeedSmoothingPercent / 100.0;
        // The motor still responds to punctuation; very low smoothing should
        // not erase keyboard-distance effects entirely.
        _smoothed += (target - _smoothed) * (0.08 + response * 0.76);
        return Math.Clamp((int)Math.Round(_smoothed), 12, 3500);
    }

    /// <summary>
    /// Context-dependent thinking/hand repositioning before hard transitions.
    /// No indiscriminate pauses on each character.
    /// </summary>
    public int NextHesitation(char current, char previous, bool isCode)
    {
        var strength = _p.HumanizerStrength / 100.0;
        if (strength == 0 || _p.MicroHesitationPercent == 0) return 0;

        var complexity = 0.0;
        if (char.IsDigit(current) && !char.IsDigit(previous)) complexity += 1.2;
        if (char.IsUpper(current) && !char.IsUpper(previous)) complexity += 0.8;
        if (current is '(' or ')' or '[' or ']' or '{' or '}' or '"' or '\'' or '\\')
            complexity += isCode ? 1.55 : 0.8;
        if (current is ':' or ';' or '=' or '/' or '<' or '>')
            complexity += isCode ? 1.05 : 0.6;
        if (previous is '.' or '?' or '!' or ':' or ';' && char.IsWhiteSpace(current))
            complexity += 1.5;
        if (char.IsLetter(current) && previous is '\n' or '\t') complexity += 1.1;
        if (char.IsLetter(current) && char.IsLetter(previous) &&
            TryLocate(current, out var a) && TryLocate(previous, out var b) &&
            a.Layout != b.Layout)
            complexity += 1.8; // switching keyboard language

        if (complexity <= 0) return 0;
        var chance = Math.Min(0.45,
            _p.MicroHesitationPercent / 100.0 * strength * complexity);
        if (_random.NextDouble() >= chance) return 0;

        // Rare longer hesitation with a bounded right-skewed duration.
        var duration = 55.0 + 115.0 *
            Math.Pow(-Math.Log(Math.Max(1e-9, 1 - _random.NextDouble())), 0.9);
        return Math.Clamp((int)Math.Round(duration * (0.7 + strength * 0.65)),
            45, 850);
    }

    /// <summary>
    /// Imperfections cluster in difficult passages instead of arriving at a
    /// fixed global rate per keystroke. The supplied typo percentage remains
    /// the baseline, and zero always disables deliberate mistakes.
    /// </summary>
    public bool ShouldMistype(char current, char previous, int wordLength,
        decimal basePercent)
    {
        if (basePercent <= 0) return false;
        var strength = _p.HumanizerStrength / 100.0;
        var difficulty = TransitionFactor(previous, current, _p.KeyTravelInfluence);
        var complexity = 1.0 + (difficulty - 1) * 1.3 * strength;
        if (char.IsUpper(current) && !char.IsUpper(previous))
            complexity += 0.18 * strength;
        if (wordLength >= 9) complexity += 0.16 * strength;
        if (_burst) complexity += 0.11 * strength;
        if (char.IsLetter(current) && char.IsLetter(previous) &&
            FamiliarPairs.Contains(string.Concat(previous, current)))
            complexity -= 0.18 * strength;
        complexity *= 1.0 + Math.Min(0.35, _charsSinceRest / 700.0 * 0.35) * strength;
        var chance = Math.Clamp((double)basePercent * complexity, 0.0, 40.0);
        return _random.NextDouble() * 100.0 < chance;
    }

    /// <summary>
    /// Weighted typo categories: physical substitution and omission happen
    /// more often than a two-character swap; 0=substitute, 1=omit,
    /// 2=duplicate, 3=transpose.
    /// </summary>
    public int NextErrorKind()
    {
        var roll = _random.Next(100);
        return roll < 49 ? 0 : roll < 73 ? 1 : roll < 89 ? 2 : 3;
    }

    public void OnRest(int milliseconds)
    {
        if (milliseconds < 180) return;
        _charsSinceRest = 0;
        _burst = false;
        _burstRemaining = _random.Next(6, 17);
        // A rest partially breaks rhythmic inertia, not the personal average.
        _rhythmNoise *= milliseconds >= 1000 ? 0.3 : 0.7;
    }

    /// <summary>Choose a nearby physical key in the same writing system.</summary>
    internal static char NearbyLetter(char source, Random random)
    {
        if (!TryLocate(source, out var site))
            return char.IsUpper(source) ? 'X' : 'x';

        var choices = new List<(char letter, double weight)>();
        foreach (var (letter, other) in KeyMap)
        {
            if (other.Layout != site.Layout || char.ToLowerInvariant(source) == letter)
                continue;
            var dx = other.X - site.X;
            var dy = other.Y - site.Y;
            var distanceSquared = dx * dx + dy * dy;
            if (distanceSquared > 2.9 || distanceSquared < 0.01) continue;
            choices.Add((letter, 1.0 / (0.3 + distanceSquared)));
        }
        if (choices.Count == 0)
            return char.IsUpper(source) ? 'X' : 'x';
        var roll = random.NextDouble() * choices.Sum(c => c.weight);
        foreach (var (letter, weight) in choices)
        {
            roll -= weight;
            if (roll <= 0)
                return char.IsUpper(source) ? char.ToUpperInvariant(letter) : letter;
        }
        var fallback = choices[^1].letter;
        return char.IsUpper(source) ? char.ToUpperInvariant(fallback) : fallback;
    }

    private double NextGaussian()
    {
        var a = Math.Max(1e-9, _random.NextDouble());
        var b = _random.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(a)) * Math.Cos(2.0 * Math.PI * b);
    }

    private static bool BasicLetter(char c) =>
        c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or
            >= 'а' and <= 'я' or >= 'А' and <= 'Я' or 'ё' or 'Ё';

    private static Dictionary<char, KeySite> CreateMap()
    {
        var result = new Dictionary<char, KeySite>();
        void AddLayout(string[] rows, int layout)
        {
            for (var y = 0; y < rows.Length; y++)
            {
                var row = rows[y];
                for (var x = 0; x < row.Length; x++)
                {
                    var middle = (row.Length - 1) / 2.0;
                    var hand = x <= middle ? 0 : 1;
                    var distanceFromEdge = hand == 0 ? x : row.Length - 1 - x;
                    var finger = hand * 4 + Math.Min(3, distanceFromEdge);
                    var offset = y == 0 ? 0 : y == 1 ? 0.45 : 0.85;
                    result[row[x]] = new KeySite(x + offset, y, finger, hand, layout);
                }
            }
        }

        AddLayout(["qwertyuiop", "asdfghjkl", "zxcvbnm"], 1);
        AddLayout(["йцукенгшщзхъ", "фывапролджэ", "ячсмитьбю"], 2);
        result['ё'] = new KeySite(-1.0, 0.0, 0, 0, 2);
        return result;
    }
}
