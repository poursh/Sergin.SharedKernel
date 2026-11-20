using System.Text;
using System.Text.RegularExpressions;

namespace Sergin.SharedKernel.Application.Tasks;

/// <summary>
/// A task's stable name: <c>&lt;schema&gt;.&lt;root, kebab&gt;.&lt;command minus "Command", kebab&gt;</c>, e.g.
/// <c>dm.device.read-device-profile</c>. The root type name is used as is, never pluralized, so no pluralization rule
/// can drift.
/// </summary>
public static partial class TaskNames
{
    private const string CommandSuffix = "Command";

    public static bool IsValid(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return Pattern().IsMatch(name);
    }

    /// <summary>The name a task of <paramref name="request"/> on <paramref name="root"/> takes in <paramref name="schema"/> (none for a source that is not a module's).</summary>
    public static string Derive(string? schema, Type root, Type request)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(request);

        string verb = request.Name.EndsWith(CommandSuffix, StringComparison.Ordinal) && request.Name.Length > CommandSuffix.Length
            ? request.Name[..^CommandSuffix.Length]
            : request.Name;

        string derived = $"{Kebab(root.Name)}.{Kebab(verb)}";
        return string.IsNullOrEmpty(schema) ? derived : $"{schema}.{derived}";
    }

    // PascalCase to kebab case: a word starts at an upper-case letter after a lower-case letter or digit, or at the last
    // capital of a run followed by a lower-case letter (ReadHTTPLog -> read-http-log). A generic type's arity is dropped.
    private static string Kebab(string name)
    {
        int tick = name.IndexOf('`', StringComparison.Ordinal);
        string plain = tick < 0 ? name : name[..tick];
        var kebab = new StringBuilder(plain.Length + 8);

        for (int i = 0; i < plain.Length; i++)
        {
            char current = plain[i];

            if (char.IsUpper(current) && i > 0)
            {
                char previous = plain[i - 1];
                bool nextIsLower = i + 1 < plain.Length && char.IsLower(plain[i + 1]);

                if (char.IsLower(previous) || char.IsDigit(previous) || char.IsUpper(previous) && nextIsLower)
                {
                    kebab.Append('-');
                }
            }

            kebab.Append(char.ToLowerInvariant(current));
        }

        return kebab.ToString();
    }

    [GeneratedRegex(@"^[a-z][a-z0-9]*(\.[a-z][a-z0-9]*(-[a-z0-9]+)*)+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Pattern();
}
