namespace KeyER;

/// <summary>
/// Conservative, lossless lexical analysis for a Code typing plan.
/// This is NOT a compiler AST. No source transformation is performed.
/// </summary>
internal static class CodeAnalyzer
{
    internal enum Language
    {
        Auto, CSharp, Cpp, C, Python, JavaScript, TypeScript, Java, Go, Rust,
        Html, Css, Json, Xml, Sql, PowerShell, Shell, Ruby, Php, Kotlin, Swift
    }

    internal enum TokenKind { Whitespace, Identifier, Keyword, Number, String, Comment, Operator, Punctuation, Markup }
    internal enum LineKind { Blank, Comment, Import, Type, Function, Control, Return, Declaration, Markup, Block, Statement }
    internal sealed record Token(int Offset, string Text, TokenKind Kind);
    internal sealed record Line(int Number, int Offset, string Text, int Indent, int Depth,
        LineKind Kind, IReadOnlyList<Token> Tokens);
    internal sealed record Plan(Language Language, IReadOnlyList<Line> Lines)
    {
        public string Reassemble() => string.Join("\n", Lines.Select(x => x.Text));
    }

    private static readonly Dictionary<string, Language> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        [".cs"] = Language.CSharp, [".cpp"] = Language.Cpp, [".cxx"] = Language.Cpp,
        [".cc"] = Language.Cpp, [".hpp"] = Language.Cpp, [".hxx"] = Language.Cpp,
        [".c"] = Language.C, [".h"] = Language.C, [".py"] = Language.Python,
        [".js"] = Language.JavaScript, [".jsx"] = Language.JavaScript,
        [".mjs"] = Language.JavaScript, [".ts"] = Language.TypeScript,
        [".tsx"] = Language.TypeScript, [".java"] = Language.Java,
        [".go"] = Language.Go, [".rs"] = Language.Rust,
        [".html"] = Language.Html, [".htm"] = Language.Html,
        [".css"] = Language.Css, [".json"] = Language.Json, [".jsonc"] = Language.Json,
        [".xml"] = Language.Xml, [".xaml"] = Language.Xml,
        [".sql"] = Language.Sql, [".ps1"] = Language.PowerShell,
        [".sh"] = Language.Shell, [".bash"] = Language.Shell,
        [".rb"] = Language.Ruby, [".php"] = Language.Php,
        [".kt"] = Language.Kotlin, [".kts"] = Language.Kotlin,
        [".swift"] = Language.Swift
    };

    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "using", "namespace", "class", "struct", "interface", "record", "enum", "public", "private",
        "protected", "internal", "static", "void", "new", "return", "throw", "if", "else", "while",
        "for", "foreach", "switch", "case", "break", "continue", "try", "catch", "finally",
        "async", "await", "def", "import", "from", "as", "elif", "pass", "yield", "lambda",
        "with", "raise", "function", "const", "let", "var", "export", "default", "extends",
        "implements", "type", "fn", "pub", "use", "mod", "impl", "trait", "match",
        "func", "package", "select", "map", "range", "fun", "val", "object", "protocol",
        "SELECT", "FROM", "WHERE", "INSERT", "UPDATE", "CREATE", "TABLE", "JOIN",
        "DELETE", "WITH", "BEGIN", "END", "true", "false", "null", "None", "self",
        "this", "super", "int", "bool", "string", "float", "double", "char", "local"
    };

    internal static string Name(Language language) => language switch
    {
        Language.CSharp => "C#", Language.Cpp => "C++", Language.C => "C",
        Language.JavaScript => "JavaScript", Language.TypeScript => "TypeScript",
        Language.Python => "Python", Language.Html => "HTML", Language.Css => "CSS",
        Language.Json => "JSON", Language.Xml => "XML", Language.Sql => "SQL",
        Language.Go => "Go", Language.Rust => "Rust", Language.Java => "Java",
        Language.PowerShell => "PowerShell", Language.Shell => "Shell",
        Language.Ruby => "Ruby", Language.Php => "PHP",
        Language.Kotlin => "Kotlin", Language.Swift => "Swift", _ => "Авто"
    };

    internal static Language Detect(string? filename, string source, Language preferred = Language.Auto)
    {
        if (preferred != Language.Auto) return preferred;
        if (!string.IsNullOrWhiteSpace(filename) &&
            Extensions.TryGetValue(Path.GetExtension(filename), out var extension))
            return extension;
        var sample = source.Length > 10000 ? source[..10000] : source;
        if (sample.Contains("#include", StringComparison.Ordinal)) return Language.Cpp;
        if (sample.Contains("using System;", StringComparison.Ordinal)) return Language.CSharp;
        if (sample.Contains("namespace ", StringComparison.Ordinal) &&
            sample.Contains("class ", StringComparison.Ordinal)) return Language.CSharp;
        if (sample.Contains("def ", StringComparison.Ordinal) &&
            sample.Contains(":", StringComparison.Ordinal)) return Language.Python;
        if (sample.Contains("console.log(", StringComparison.Ordinal) ||
            sample.Contains("function ", StringComparison.Ordinal)) return Language.JavaScript;
        if (sample.Contains("<!DOCTYPE html", StringComparison.OrdinalIgnoreCase) ||
            sample.Contains("<html", StringComparison.OrdinalIgnoreCase)) return Language.Html;
        if (sample.Contains("<?xml", StringComparison.OrdinalIgnoreCase)) return Language.Xml;
        if (sample.Contains("SELECT ", StringComparison.OrdinalIgnoreCase) &&
            sample.Contains(" FROM ", StringComparison.OrdinalIgnoreCase)) return Language.Sql;
        if (sample.StartsWith("#!/bin/", StringComparison.Ordinal)) return Language.Shell;
        return Language.Auto;
    }

    internal static Plan Analyze(string source, string? filename = null, Language preferred = Language.Auto)
    {
        source = source.Replace("\r\n", "\n").Replace('\r', '\n');
        var language = Detect(filename, source, preferred);
        var pieces = source.Split('\n');
        var lines = new List<Line>(pieces.Length);
        var depth = 0;
        var offset = 0;
        var scanner = new Scanner(language);

        for (var n = 0; n < pieces.Length; n++)
        {
            var line = pieces[n];
            var tokens = scanner.Tokenize(line);
            var indent = 0;
            foreach (var ch in line)
            {
                if (ch == ' ') indent++;
                else if (ch == '\t') indent += 4;
                else break;
            }

            var first = tokens.FirstOrDefault(t => t.Kind != TokenKind.Whitespace);
            var kind = Classify(line, first, tokens, language);
            var effectiveDepth = IsIndentBased(language) ? indent / 4 : depth;
            if (!IsIndentBased(language))
            {
                foreach (var token in tokens)
                {
                    if (token.Kind is TokenKind.Comment or TokenKind.String or TokenKind.Markup) continue;
                    foreach (var ch in token.Text)
                    {
                        if (ch == '{') depth++;
                        else if (ch == '}') depth = Math.Max(0, depth - 1);
                    }
                }
                if (first?.Text == "}") effectiveDepth = Math.Max(0, effectiveDepth - 1);
            }

            lines.Add(new Line(n + 1, offset, line, indent, effectiveDepth, kind, tokens));
            offset += line.Length + 1;
        }

        return new Plan(language, lines);
    }

    private static bool IsIndentBased(Language language) =>
        language is Language.Python or Language.Ruby or Language.Shell;

    private static LineKind Classify(string line, Token? first, IReadOnlyList<Token> tokens, Language language)
    {
        if (string.IsNullOrWhiteSpace(line)) return LineKind.Blank;
        if (first is null) return LineKind.Blank;
        if (first.Kind == TokenKind.Comment) return LineKind.Comment;
        if (language is Language.Html or Language.Xml) return LineKind.Markup;
        var trimmed = line.TrimStart();
        var word = first.Text;

        if (trimmed.StartsWith("#include", StringComparison.Ordinal) ||
            trimmed.StartsWith("#import", StringComparison.Ordinal) ||
            word is "using" or "import" or "from" or "use" or "package")
            return LineKind.Import;

        if (tokens.Where(t => t.Kind is TokenKind.Identifier or TokenKind.Keyword).Any(
            t => t.Text is "class" or "struct" or "interface" or "enum" or "trait" or "record"))
            return LineKind.Type;

        if (word is "def" or "function" or "fn" or "func" ||
            (trimmed.Contains('(') && trimmed.Contains(')') &&
             (trimmed.EndsWith('{') || trimmed.EndsWith("=>", StringComparison.Ordinal)) &&
             !new[] { "if", "for", "while", "switch", "catch" }.Contains(word)))
            return LineKind.Function;

        if (word is "if" or "for" or "foreach" or "while" or "switch" or "elif" or
            "else" or "try" or "catch" or "finally" or "match" or "case" ||
            (language == Language.Sql && word.ToUpperInvariant() is "WHERE" or "JOIN"))
            return LineKind.Control;

        if (word is "return" or "yield" or "throw" or "raise") return LineKind.Return;
        if (trimmed is "{" or "}" or "};" or "end" ||
            trimmed.StartsWith("}", StringComparison.Ordinal))
            return LineKind.Block;

        if (word is "const" or "let" or "var" or "val" or "int" or "string" or
            "bool" or "float" or "double" or "char" ||
            (language == Language.Sql && word.ToUpperInvariant() is "INSERT" or "UPDATE" or "DELETE"))
            return LineKind.Declaration;

        return LineKind.Statement;
    }

    private sealed class Scanner(Language language)
    {
        private bool _blockComment;
        private string? _multilineString;

        private bool HashComments => language is Language.Python or Language.Shell or Language.Ruby or Language.PowerShell;
        private bool DashComments => language == Language.Sql;
        private bool HtmlComments => language is Language.Html or Language.Xml;
        private bool CStyleComments => language is not (Language.Python or Language.Shell or Language.Ruby or Language.PowerShell or Language.Html or Language.Xml);

        internal IReadOnlyList<Token> Tokenize(string line)
        {
            var list = new List<Token>();
            var i = 0;
            var start = 0;
            while (i < line.Length)
            {
                start = i;
                if (_multilineString is not null)
                {
                    var end = line.IndexOf(_multilineString, i, StringComparison.Ordinal);
                    i = end < 0 ? line.Length : end + _multilineString.Length;
                    Add(TokenKind.String);
                    if (end >= 0) _multilineString = null;
                    continue;
                }

                if (_blockComment)
                {
                    var marker = HtmlComments ? "-->" : "*/";
                    var end = line.IndexOf(marker, i, StringComparison.Ordinal);
                    i = end < 0 ? line.Length : end + marker.Length;
                    Add(TokenKind.Comment);
                    if (end >= 0) _blockComment = false;
                    continue;
                }

                if (char.IsWhiteSpace(line[i]))
                {
                    while (i < line.Length && char.IsWhiteSpace(line[i])) i++;
                    Add(TokenKind.Whitespace);
                    continue;
                }

                if (HtmlComments && Starts("<!--")) { _blockComment = true; continue; }
                if (CStyleComments && Starts("/*")) { _blockComment = true; continue; }
                if ((CStyleComments && Starts("//")) ||
                    (HashComments && Starts("#")) || (DashComments && Starts("--")))
                {
                    i = line.Length;
                    Add(TokenKind.Comment);
                    continue;
                }

                if (language == Language.Python && (Starts("'''") || Starts("\"\"\"")))
                {
                    var marker = line.Substring(i, 3);
                    var end = line.IndexOf(marker, i + 3, StringComparison.Ordinal);
                    i = end < 0 ? line.Length : end + 3;
                    if (end < 0) _multilineString = marker;
                    Add(TokenKind.String);
                    continue;
                }

                if (line[i] is '\'' or '"' or '`')
                {
                    var quote = line[i++];
                    var closed = false;
                    while (i < line.Length)
                    {
                        if (line[i] == '\\' && i + 1 < line.Length) { i += 2; continue; }
                        if (line[i++] == quote) { closed = true; break; }
                    }
                    if (!closed && quote == '`') _multilineString = "`";
                    Add(TokenKind.String);
                    continue;
                }

                if (language is Language.Html or Language.Xml)
                {
                    i++;
                    while (i < line.Length && line[i] != '<' &&
                        line[i] != '>' && !char.IsWhiteSpace(line[i])) i++;
                    Add(TokenKind.Markup);
                    continue;
                }

                if (line[i] == '_' || char.IsLetter(line[i]))
                {
                    i++;
                    while (i < line.Length && (line[i] == '_' || char.IsLetterOrDigit(line[i]))) i++;
                    var word = line.Substring(start, i - start);
                    Add(Keywords.Contains(word) ? TokenKind.Keyword : TokenKind.Identifier);
                    continue;
                }

                if (char.IsDigit(line[i]))
                {
                    i++;
                    while (i < line.Length && (char.IsLetterOrDigit(line[i]) || line[i] is '_' or '.')) i++;
                    Add(TokenKind.Number);
                    continue;
                }

                i++;
                Add("()[]{};,:".Contains(line[start]) ? TokenKind.Punctuation : TokenKind.Operator);
            }
            return list;

            bool Starts(string marker) => line.AsSpan(i).StartsWith(marker, StringComparison.Ordinal);
            void Add(TokenKind kind) =>
                list.Add(new Token(start, line.Substring(start, i - start), kind));
        }
    }
}
