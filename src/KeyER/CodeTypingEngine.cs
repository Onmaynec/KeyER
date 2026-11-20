namespace KeyER;

/// <summary>
/// Code-mode typing based on a lossless lexical plan. The engine never changes
/// code syntax or relies on unobserved IDE completion behavior.
/// </summary>
public sealed partial class TypingEngine
{
    private async Task RunCodeAsync(string source, TypingProfile profile, IntPtr target,
        string? filename, CodeAnalyzer.Language language, CancellationToken token)
    {
        var plan = CodeAnalyzer.Analyze(source, filename, language);
        var random = _randomFactory();
        var done = 0;
        var baseDelay = 60000.0 / profile.CharactersPerMinute;
        var smoothDelay = baseDelay;

        await WaitForHotkeyReleaseAsync(target, token);

        foreach (var line in plan.Lines)
        {
            await EnsureActiveAsync(target, token);

            if (line.Number > 1)
            {
                await KeyAsync(Keys.Enter, target, token);
                done++;
                ProgressChanged?.Invoke(done, source.Length);

                // The editor controls auto-indent. No automatic "smart" deletion
                // is possible without reading its current state (future v0.4).
                if (profile.ClearAutoIndent && line.Text.Length > 0)
                {
                    await KeyAsync(Keys.Home, target, token);
                    await KeyAsync(Keys.Home, target, token);
                    await ChordAsync(Keys.ShiftKey, Keys.End, target, token);
                }
                await PauseAsync(Math.Clamp(profile.CodeLinePauseMilliseconds, 0, 10000), token);
            }

            await LinePauseAsync(line, profile, random, token);

            foreach (var part in line.Tokens)
            {
                var index = 0;
                while (index < part.Text.Length)
                {
                    await EnsureActiveAsync(target, token);
                    var c = part.Text[index];

                    // Mistakes are restricted to identifier/keyword letters. No
                    // syntax, indentation, literal or comment is deliberately corrupted.
                    if (profile.TypoPercent > 0 &&
                        part.Kind is CodeAnalyzer.TokenKind.Identifier or CodeAnalyzer.TokenKind.Keyword &&
                        CanMistype(c) && random.NextDouble() * 100 < (double)profile.TypoPercent)
                    {
                        // Pass token-local text: delayed corrections cannot escape
                        // into another token or cross punctuation.
                        var used = await MistypeAndCorrectAsync(part.Text, index, done,
                            profile, TypingMode.Code, target, random, false, token);
                        index += used;
                        done += used;
                    }
                    else
                    {
                        await CharacterAsync(c, target, token);
                        index++;
                        done++;
                    }

                    var previous = index > 1 ? part.Text[index - 2] : '\0';
                    var factor = part.Kind switch
                    {
                        CodeAnalyzer.TokenKind.Identifier => 100.0 / profile.CodeIdentifierSpeedPercent,
                        CodeAnalyzer.TokenKind.Keyword => 1.15,
                        CodeAnalyzer.TokenKind.String => 100.0 / profile.CodeStringSpeedPercent,
                        CodeAnalyzer.TokenKind.Comment => 100.0 / profile.CodeCommentSpeedPercent,
                        CodeAnalyzer.TokenKind.Number => 1.2,
                        CodeAnalyzer.TokenKind.Punctuation => 1.25,
                        CodeAnalyzer.TokenKind.Operator => 1.15,
                        _ => 1.0
                    };
                    factor *= 1.0 + (random.NextDouble() * 2 - 1) *
                        profile.SpeedVariationPercent / 100.0;
                    factor *= 1.0 + Math.Min(1.0, done / 4000.0) *
                        profile.FatiguePercent / 100.0;

                    var targetDelay = Math.Clamp(baseDelay * factor, 12, 3000);
                    smoothDelay += (targetDelay - smoothDelay) * 0.28;
                    await PauseAsync(Math.Clamp((int)Math.Round(smoothDelay), 12, 3000), token);

                    if (done % 4 == 0 || index == part.Text.Length)
                        ProgressChanged?.Invoke(done, source.Length);
                }

                // Reversible, meaningful caret action on identifiers: select the
                // last few letters and type them again, like a local re-edit.
                // Disabled by default to minimize interactions with autocomplete.
                if (part.Kind == CodeAnalyzer.TokenKind.Identifier &&
                    part.Text.Length >= 3 && part.Text.All(char.IsLetterOrDigit) &&
                    random.Next(100) < profile.CodeReviewPercent)
                {
                    var count = random.Next(1, Math.Min(3, part.Text.Length) + 1);
                    for (var k = 0; k < count; k++)
                        await ChordAsync(Keys.ShiftKey, Keys.Left, target, token);
                    foreach (var ch in part.Text[^count..])
                        await CharacterAsync(ch, target, token);
                }

                if (part.Kind == CodeAnalyzer.TokenKind.Punctuation &&
                    part.Text.IndexOfAny([';', '{', '}', ':']) >= 0)
                {
                    var pause = profile.CodeSyntaxPauseMilliseconds +
                                random.Next(0, 90);
                    await PauseAsync(pause, token);
                }
                else if (part.Kind == CodeAnalyzer.TokenKind.Comment &&
                         part.Text.Length >= 12)
                {
                    await PauseAsync(Math.Min(300, profile.CodeSyntaxPauseMilliseconds), token);
                }

                if (random.NextDouble() * 100 < (double)profile.LongPausePercent)
                    await PauseAsync(random.Next(profile.PauseMinMilliseconds,
                        profile.PauseMaxMilliseconds + 1), token);
            }
        }

        ProgressChanged?.Invoke(source.Length, source.Length);
    }

    private Task LinePauseAsync(CodeAnalyzer.Line line, TypingProfile profile,
        Random random, CancellationToken token)
    {
        var basePause = line.Kind switch
        {
            CodeAnalyzer.LineKind.Type or CodeAnalyzer.LineKind.Function =>
                profile.CodeFunctionPauseMilliseconds,
            CodeAnalyzer.LineKind.Control or CodeAnalyzer.LineKind.Block =>
                profile.CodeBlockPauseMilliseconds,
            CodeAnalyzer.LineKind.Comment => profile.CodeSyntaxPauseMilliseconds,
            CodeAnalyzer.LineKind.Import => profile.CodeSyntaxPauseMilliseconds / 2,
            CodeAnalyzer.LineKind.Blank => 0,
            _ => 0
        };
        var depthCost = Math.Min(line.Depth, 8) * 18;
        var milliseconds = basePause + depthCost;
        return milliseconds > 0 ? PauseAsync(milliseconds + random.Next(0, 70), token)
                                : Task.CompletedTask;
    }
}
