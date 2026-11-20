using System.Text;

namespace KeyER;

/// <summary>
/// Human Typing Engine 2.0. Text mode only. All corrections are confined to the
/// current physical line and use exact UTF-16 offsets for safe caret recovery.
/// Complex Unicode is typed verbatim and never crossed by a deferred correction.
/// </summary>
public sealed partial class TypingEngine
{
    private enum TextError { Substitute, Omit, Duplicate, Transpose }

    private sealed class DeferredError
    {
        public required string Original { get; init; }
        public required int WrongLength { get; init; }
        public int Trailing { get; set; }
        public bool FirstWordEnded { get; set; }
        public int FollowingWords { get; set; }
        public int TargetWords { get; init; }
    }

    private sealed class HumanCadence
    {
        private readonly TypingProfile _p;
        private readonly Random _rng;
        private readonly HumanBehaviorModel _behavior;
        private double _current;
        private int _burstRemaining;
        private bool _burst;

        private static readonly HashSet<string> FamiliarPairs = new(StringComparer.OrdinalIgnoreCase)
        {
            "th", "he", "in", "er", "an", "re", "on", "at", "en", "nd",
            "st", "es", "or", "te", "ed", "is", "it", "al", "ar", "to",
            "ст", "но", "на", "ов", "ен", "ни", "ра", "пр", "то", "ко",
            "по", "во", "ро", "ер", "ос", "ре", "ал", "ли", "ка", "не",
            "та", "ва", "го", "ор", "ни", "ла", "от", "за"
        };

        public HumanCadence(TypingProfile profile, Random rng, HumanBehaviorModel behavior)
        {
            _p = profile;
            _rng = rng;
            _behavior = behavior;
            _current = 60000.0 / profile.CharactersPerMinute;
        }

        public int Next(char c, char previous, int sourceIndex, int wordLength)
        {
            if (--_burstRemaining <= 0)
            {
                _burst = _rng.Next(100) < _p.BurstPercent;
                _burstRemaining = _rng.Next(7, 22);
            }

            var factor = 1.0;
            if (wordLength is > 0 and <= 4)
                factor *= 100.0 / _p.ShortWordSpeedPercent;
            else if (wordLength >= 9)
                factor *= 100.0 / _p.LongWordSpeedPercent;

            if (char.IsDigit(c))
                factor *= _p.DigitSlowdownPercent / 100.0;
            else if (char.IsPunctuation(c))
                factor *= _p.PunctuationSlowdownPercent / 100.0;
            else if (char.IsSymbol(c) || (char.IsLetter(c) && !SimpleLetter(c)))
                factor *= _p.SpecialSlowdownPercent / 100.0;

            if (c != '\0' && previous != '\0' && char.IsLetter(c) && char.IsLetter(previous) &&
                FamiliarPairs.Contains(string.Concat(previous, c)))
                factor *= 1.0 - _p.FamiliarPairBoostPercent / 100.0;

            if (_burst) factor *= 0.78;
            factor *= _behavior.NextSpeedFactor();
            factor *= 1 + (_rng.NextDouble() * 2 - 1) * _p.SpeedVariationPercent / 100.0;

            var target = (60000.0 / _p.CharactersPerMinute) * factor;
            // Exponential easing prevents abrupt, jittery speed changes.
            _current += (target - _current) * _p.SpeedSmoothingPercent / 100.0;
            return Math.Clamp((int)Math.Round(_current), 12, 3500);
        }

        private static bool SimpleLetter(char c) =>
            c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or
                >= 'а' and <= 'я' or >= 'А' and <= 'Я' or 'ё' or 'Ё';
    }

    private async Task RunHumanTextAsync(string source, TypingProfile p, IntPtr target,
        CancellationToken token)
    {
        var rng = _randomFactory();
        var behavior = new HumanBehaviorModel(p, rng);
        var rhythm = new HumanCadence(p, rng, behavior);
        var wordLengths = GetWordLengths(source);
        DeferredError? pending = null;
        var index = 0;

        await WaitForHotkeyReleaseAsync(target, token);

        while (index < source.Length)
        {
            await EnsureActiveAsync(target, token);
            var c = source[index];

            // Delayed edits cannot cross newlines, tabs, emoji or punctuation:
            // navigation by arrow counts is only reliable in plain text/spaces.
            if (pending is not null && (!IsSafeToNavigate(c) ||
                pending.Trailing >= p.LateCorrectionMaxDistance))
            {
                await FixAsync(pending, p, target, rng, token);
                pending = null;
            }

            if (c == '\n')
            {
                if (p.TextEnter == TextEnterMode.ShiftEnter)
                    await ChordAsync(Keys.ShiftKey, Keys.Enter, target, token);
                else
                    await KeyAsync(Keys.Enter, target, token);
                index++;
            }
            else if (c == '\t')
            {
                if (p.TextTab == TextTabMode.Spaces)
                {
                    for (var k = 0; k < p.TabSpaces; k++)
                        await CharacterAsync(' ', target, token);
                }
                else await KeyAsync(Keys.Tab, target, token);
                index++;
            }
            else if (pending is null && p.TypoPercent > 0 && CanTextMistype(c) &&
                rng.NextDouble() * 100 < (double)p.TypoPercent)
            {
                var kind = (TextError)rng.Next(4);
                if (kind == TextError.Transpose && (index + 1 >= source.Length ||
                    !CanTextMistype(source[index + 1])))
                    kind = TextError.Substitute;

                var consumed = kind == TextError.Transpose ? 2 : 1;
                var original = source.Substring(index, consumed);
                var wrong = kind switch
                {
                    TextError.Substitute => WrongLetter(c, rng).ToString(),
                    TextError.Omit => "",
                    TextError.Duplicate => new string(c, 2),
                    _ => string.Concat(source[index + 1], c)
                };

                foreach (var ch in wrong)
                {
                    await CharacterAsync(ch, target, token);
                    await PauseAsync(rhythm.Next(ch, '\0', index, wordLengths[index]), token);
                }

                index += consumed;
                var late = rng.Next(100) < p.LateCorrectionPercent;
                var typo = new DeferredError
                {
                    Original = original,
                    WrongLength = wrong.Length,
                    TargetWords = rng.Next(1, p.LateCorrectionMaxWords + 1)
                };

                if (late) pending = typo;
                else await FixAsync(typo, p, target, rng, token);
            }
            else
            {
                // Input is UTF-16 by design. Surrogate code units are sent in order,
                // but a delayed correction is always fixed before crossing them.
                await CharacterAsync(c, target, token);
                if (pending is not null)
                {
                    pending.Trailing++;
                    if (c == ' ' && index > 0 && char.IsLetterOrDigit(source[index - 1]))
                    {
                        if (!pending.FirstWordEnded) pending.FirstWordEnded = true;
                        else pending.FollowingWords++;
                    }
                }

                var prev = index > 0 ? source[index - 1] : '\0';
                await PauseAsync(rhythm.Next(c, prev, index, wordLengths[index]), token);
                index++;
            }

            if (pending is not null &&
                (pending.FollowingWords >= pending.TargetWords ||
                 pending.Trailing >= p.LateCorrectionMaxDistance))
            {
                await FixAsync(pending, p, target, rng, token);
                pending = null;
            }

            if (index % 4 == 0 || c is '\n' or '\t' || index >= source.Length)
                ProgressChanged?.Invoke(index, source.Length);

            await TextPauseAsync(source, index, c, p, rng, token);

            // A deliberate review stays inside the most recent ordinary word;
            // never navigate across line breaks, emoji, tabs or pending corrections.
            if (pending is null && index > 0 &&
                (index == source.Length || !IsSafeToNavigate(source[index])) &&
                IsReviewableCodeUnit(source[index - 1]) && behavior.ShouldReview())
            {
                var start = index - 1;
                while (start >= 0 && IsReviewableCodeUnit(source[start])) start--;
                await ReviewPreviousAsync(index - start - 1, behavior, target, rng, token);
            }

            var attention = behavior.NextAttentionBreakMilliseconds();
            if (attention > 0) await PauseAsync(attention, token);

            if (c is '.' or '!' or '?' && index > 0 &&
                (index == source.Length || char.IsWhiteSpace(source[index])))
            {
                var thinking = behavior.NextThinkingPauseMilliseconds();
                if (thinking > 0) await PauseAsync(thinking, token);
            }
        }

        if (pending is not null) await FixAsync(pending, p, target, rng, token);
        ProgressChanged?.Invoke(source.Length, source.Length);
    }

    private async Task FixAsync(DeferredError error, TypingProfile p, IntPtr target,
        Random rng, CancellationToken token)
    {
        // The caret is directly after the wrong text plus Trailing safe chars.
        // Preserve the suffix and return to its end after replacing the error.
        for (var i = 0; i < error.Trailing; i++)
            await KeyAsync(Keys.Left, target, token);

        if (error.WrongLength > 0)
        {
            var choice = rng.Next(100);
            if (choice < p.SelectionCorrectionPercent)
            {
                // Shift+Left selects the erroneous range, typing replaces it.
                for (var i = 0; i < error.WrongLength; i++)
                    await ChordAsync(Keys.ShiftKey, Keys.Left, target, token);
            }
            else if (choice < p.SelectionCorrectionPercent + p.DeleteCorrectionPercent)
            {
                // Position before the bad range; use Delete instead of Backspace.
                for (var i = 0; i < error.WrongLength; i++)
                    await KeyAsync(Keys.Left, target, token);
                for (var i = 0; i < error.WrongLength; i++)
                    await KeyAsync(Keys.Delete, target, token);
            }
            else
            {
                for (var i = 0; i < error.WrongLength; i++)
                    await KeyAsync(Keys.Back, target, token);
            }
        }

        foreach (var ch in error.Original)
            await CharacterAsync(ch, target, token);

        await PauseAsync(rng.Next(60, 170), token);
        for (var i = 0; i < error.Trailing; i++)
            await KeyAsync(Keys.Right, target, token);
    }

    private async Task TextPauseAsync(string source, int index, char last,
        TypingProfile p, Random rng, CancellationToken token)
    {
        // Pauses are additional to gradual per-character cadence.
        if (last is '.' or '!' or '?' &&
            (index >= source.Length || char.IsWhiteSpace(source[index])))
            await RandomPauseAsync(p.SentencePauseMinMilliseconds, p.SentencePauseMaxMilliseconds,
                token, rng);

        if (last == '\n')
        {
            var blankLine = index < source.Length && source[index] == '\n';
            if (blankLine)
                await RandomPauseAsync(p.BlockPauseMinMilliseconds, p.BlockPauseMaxMilliseconds, token, rng);
            else
                await RandomPauseAsync(p.ParagraphPauseMinMilliseconds,
                    p.ParagraphPauseMaxMilliseconds, token, rng);
        }

        if (rng.NextDouble() * 100 < (double)p.LongPausePercent)
            await RandomPauseAsync(p.PauseMinMilliseconds, p.PauseMaxMilliseconds, token, rng);
    }

    private Task RandomPauseAsync(int min, int max, CancellationToken token, Random rng) =>
        PauseAsync(rng.Next(min, max + 1), token);

    private static bool CanTextMistype(char c) =>
        c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or
            >= 'а' and <= 'я' or >= 'А' and <= 'Я' or 'ё' or 'Ё';

    private static bool IsSafeToNavigate(char c) =>
        CanTextMistype(c) || c == ' ' || char.IsDigit(c);

    private static int[] GetWordLengths(string source)
    {
        var lengths = new int[source.Length];
        for (var i = 0; i < source.Length;)
        {
            if (!char.IsLetterOrDigit(source[i]))
            {
                i++;
                continue;
            }

            var start = i;
            while (i < source.Length && char.IsLetterOrDigit(source[i])) i++;
            var len = i - start;
            for (var n = start; n < i; n++) lengths[n] = len;
        }
        return lengths;
    }
}
