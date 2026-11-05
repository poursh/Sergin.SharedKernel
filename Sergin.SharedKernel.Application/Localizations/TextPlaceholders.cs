using System.Globalization;
using System.Text;

namespace Sergin.SharedKernel.Application.Localizations;

/// <summary>
/// Named placeholders in a text, <c>{DeviceId}</c>. <c>{{</c> and <c>}}</c> write a literal brace.
/// </summary>
public static class TextPlaceholders
{
    /// <summary>The placeholder names <paramref name="text"/> uses.</summary>
    public static IReadOnlySet<string> In(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        HashSet<string> names = [];
        Scan(text, null, names, CultureInfo.InvariantCulture, null);

        return names;
    }

    /// <summary>
    /// <paramref name="template"/> with each placeholder replaced by its argument, formatted in
    /// <paramref name="culture"/>. A placeholder with no argument stays as written.
    /// </summary>
    public static string Format(string template, IReadOnlyDictionary<string, object?> arguments, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(culture);

        StringBuilder result = new(template.Length);
        Scan(template, result, null, culture, arguments);

        return result.ToString();
    }

    private static void Scan(
        string text,
        StringBuilder? output,
        HashSet<string>? names,
        CultureInfo culture,
        IReadOnlyDictionary<string, object?>? arguments)
    {
        int i = 0;

        while (i < text.Length)
        {
            char c = text[i];

            if ((c == '{' || c == '}') && i + 1 < text.Length && text[i + 1] == c)
            {
                output?.Append(c);
                i += 2;
                continue;
            }

            if (c == '{')
            {
                int end = text.IndexOf('}', i + 1);

                if (end > i + 1 && IsName(text.AsSpan(i + 1, end - i - 1)))
                {
                    string name = text.Substring(i + 1, end - i - 1);
                    names?.Add(name);

                    output?.Append(arguments is not null && arguments.TryGetValue(name, out object? value)
                        ? Convert.ToString(value, culture)
                        : text.Substring(i, end - i + 1));

                    i = end + 1;
                    continue;
                }
            }

            output?.Append(c);
            i++;
        }
    }

    private static bool IsName(ReadOnlySpan<char> name)
    {
        if (name.IsEmpty || !(char.IsAsciiLetter(name[0]) || name[0] == '_'))
        {
            return false;
        }

        foreach (char c in name)
        {
            if (!(char.IsAsciiLetterOrDigit(c) || c == '_'))
            {
                return false;
            }
        }

        return true;
    }
}
