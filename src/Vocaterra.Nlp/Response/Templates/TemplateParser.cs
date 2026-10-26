using System.Text;

namespace Vocaterra.Nlp.Response.Templates;

/// <summary>A piece of a parsed template: literal text or a placeholder.</summary>
internal abstract record TemplatePart;

/// <summary>Literal text, with doubled braces already unescaped.</summary>
internal sealed record LiteralPart(string Text) : TemplatePart;

/// <summary>A <c>{slot:filter:filter}</c> placeholder.</summary>
internal sealed record PlaceholderPart(string Source, string SlotName, IReadOnlyList<TemplateFilter> Filters) : TemplatePart;

/// <summary>One filter of a placeholder with its comma-separated arguments.</summary>
internal sealed record TemplateFilter(string Name, IReadOnlyList<string> Args);

/// <summary>Splits template text into literals and placeholders; <c>{{</c> and <c>}}</c> are literal braces.</summary>
internal static class TemplateParser
{
    public static IReadOnlyList<TemplatePart> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var parts = new List<TemplatePart>();
        var literal = new StringBuilder();
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (c == '{')
            {
                if (i + 1 < text.Length && text[i + 1] == '{')
                {
                    literal.Append('{');
                    i += 2;
                    continue;
                }

                var close = text.IndexOf('}', i + 1);
                if (close < 0)
                {
                    throw new TemplateException(text[i..], $"Unterminated placeholder '{text[i..]}'.");
                }

                var source = text[i..(close + 1)];
                if (literal.Length > 0)
                {
                    parts.Add(new LiteralPart(literal.ToString()));
                    literal.Clear();
                }

                parts.Add(ParsePlaceholder(source, text[(i + 1)..close]));
                i = close + 1;
            }
            else if (c == '}')
            {
                if (i + 1 < text.Length && text[i + 1] == '}')
                {
                    literal.Append('}');
                    i += 2;
                    continue;
                }

                throw new TemplateException("}", $"Unmatched '}}' at offset {i}; write '}}}}' for a literal brace.");
            }
            else
            {
                literal.Append(c);
                i++;
            }
        }

        if (literal.Length > 0)
        {
            parts.Add(new LiteralPart(literal.ToString()));
        }

        return parts;
    }

    private static PlaceholderPart ParsePlaceholder(string source, string body)
    {
        if (body.Contains('{', StringComparison.Ordinal))
        {
            throw new TemplateException(source, $"Nested braces in placeholder '{source}'.");
        }

        var segments = SplitTopLevel(body, source);
        var name = segments[0].Trim();
        if (name.Length == 0 || !IsSlotName(name))
        {
            throw new TemplateException(source, $"Invalid slot name '{name}' in placeholder '{source}'.");
        }

        var filters = new List<TemplateFilter>(segments.Count - 1);
        for (var s = 1; s < segments.Count; s++)
        {
            filters.Add(ParseFilter(segments[s].Trim(), source));
        }

        return new PlaceholderPart(source, name, filters);
    }

    private static List<string> SplitTopLevel(string body, string source)
    {
        var segments = new List<string>();
        var depth = 0;
        var start = 0;
        for (var i = 0; i < body.Length; i++)
        {
            switch (body[i])
            {
                case '(':
                    depth++;
                    break;
                case ')':
                    depth--;
                    if (depth < 0)
                    {
                        throw new TemplateException(source, $"Unbalanced parentheses in placeholder '{source}'.");
                    }

                    break;
                case ':' when depth == 0:
                    segments.Add(body[start..i]);
                    start = i + 1;
                    break;
                default:
                    break;
            }
        }

        if (depth != 0)
        {
            throw new TemplateException(source, $"Unbalanced parentheses in placeholder '{source}'.");
        }

        segments.Add(body[start..]);
        return segments;
    }

    private static TemplateFilter ParseFilter(string text, string source)
    {
        if (text.Length == 0)
        {
            throw new TemplateException(source, $"Empty filter in placeholder '{source}'.");
        }

        var open = text.IndexOf('(', StringComparison.Ordinal);
        string name;
        var args = new List<string>();
        if (open < 0)
        {
            name = text;
        }
        else
        {
            if (text[^1] != ')')
            {
                throw new TemplateException(source, $"Filter '{text}' must end with ')' in placeholder '{source}'.");
            }

            name = text[..open].Trim();
            var inner = text[(open + 1)..^1];
            foreach (var arg in inner.Split(','))
            {
                var trimmed = arg.Trim();
                if (trimmed.Length == 0)
                {
                    throw new TemplateException(source, $"Empty argument in filter '{text}' of placeholder '{source}'.");
                }

                args.Add(trimmed);
            }
        }

        if (name.Length == 0 || !IsFilterName(name))
        {
            throw new TemplateException(source, $"Invalid filter name '{name}' in placeholder '{source}'.");
        }

        return new TemplateFilter(name, args);
    }

    private static bool IsSlotName(string name)
    {
        foreach (var c in name)
        {
            if (!(char.IsLetterOrDigit(c) || c is '_' or '-' or '.'))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsFilterName(string name)
    {
        foreach (var c in name)
        {
            if (!(char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-'))
            {
                return false;
            }
        }

        return true;
    }
}
