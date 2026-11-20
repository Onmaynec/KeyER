namespace KeyER;

public sealed partial class TypingEngine
{
    // Safe BMP subset: one Left/Right event is one document character here.
    // Excludes grapheme clusters, surrogate pairs and punctuation with
    // editor-specific navigation semantics.
    private static bool IsReviewableCodeUnit(char value) =>
        value is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or
            >= '0' and <= '9' or >= 'а' and <= 'я' or >= 'А' and <= 'Я' or
            'ё' or 'Ё' or '_';

    private async Task ReviewPreviousAsync(int available, HumanBehaviorModel behavior,
        IntPtr target, Random random, CancellationToken token)
    {
        var count = behavior.NextLookbackLength(available);
        if (count == 0) return;

        // Only navigate by matching step counts; never delete text or use Home,
        // End, Ctrl+Arrow, which have editor-dependent word-boundary behavior.
        for (var i = 0; i < count; i++)
        {
            await KeyAsync(Keys.Left, target, token);
            await PauseAsync(random.Next(24, 62), token);
        }
        await PauseAsync(random.Next(100, 300), token);
        for (var i = 0; i < count; i++)
        {
            await KeyAsync(Keys.Right, target, token);
            await PauseAsync(random.Next(24, 62), token);
        }
    }
}
