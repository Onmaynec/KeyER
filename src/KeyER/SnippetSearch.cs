namespace KeyER;

/// <summary>
/// Side-effect free Find navigation. Search never replaces or mutates source text.
/// Results are capped to keep the UI responsive for very large documents.
/// </summary>
internal sealed class SnippetSearch
{
    private const int MaximumMatches = 10_000;
    private readonly List<int> _offsets = [];

    public IReadOnlyList<int> Offsets => _offsets;
    public bool Truncated { get; private set; }
    public int Length { get; private set; }

    public void Update(string source, string query, bool matchCase)
    {
        _offsets.Clear();
        Truncated = false;
        Length = query.Length;
        if (string.IsNullOrEmpty(query) || string.IsNullOrEmpty(source)) return;

        var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var at = 0;
        while (at <= source.Length - query.Length)
        {
            var match = source.IndexOf(query, at, comparison);
            if (match < 0) break;
            if (_offsets.Count == MaximumMatches)
            {
                Truncated = true;
                break;
            }
            _offsets.Add(match);
            at = match + Math.Max(1, query.Length);
        }
    }

    public int Next(int currentPosition, bool forward, bool includeCurrent = false)
    {
        if (_offsets.Count == 0) return -1;
        if (forward)
        {
            foreach (var offset in _offsets)
                if (offset > currentPosition || (includeCurrent && offset == currentPosition))
                    return offset;
            return _offsets[0];
        }
        for (var i = _offsets.Count - 1; i >= 0; i--)
            if (_offsets[i] < currentPosition || (includeCurrent && _offsets[i] == currentPosition))
                return _offsets[i];
        return _offsets[^1];
    }

    public int OrdinalAt(int position)
    {
        var index = _offsets.BinarySearch(position);
        return index < 0 ? 0 : index + 1;
    }
}
