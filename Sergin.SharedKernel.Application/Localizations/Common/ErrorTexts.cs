namespace Sergin.SharedKernel.Application.Localizations.Common;

/// <summary>
/// The title and detail an error that reaches a person is shown with, under
/// <c>common.errors.&lt;code&gt;.title|detail</c>: one pair per platform error code (<c>General.NotFound</c> is
/// <c>common.errors.not-found</c>), and the pair of the error's type for a code no pair names. A validation error
/// shares <see cref="ValidationTitle"/> and keeps its own message as the detail.
/// </summary>
public static class ErrorTexts
{
    private const string GeneralPrefix = "General.";

    private static readonly TextCategory category = TextCategory.Create("Common", "Errors");

    private static readonly Dictionary<string, ErrorTextPair> byCode = new(StringComparer.Ordinal);

    public static TextDefinition ValidationTitle { get; } =
        category.Child("Validation").Text("Title", "One or more validation errors occurred.");

    public static ErrorTextPair NotFound { get; } =
        Pair("NotFound", "Not found", "The record was not found, or you cannot see it here.");

    public static ErrorTextPair Forbidden { get; } =
        Pair("Forbidden", "Not allowed", "You do not have the permission this needs.");

    public static ErrorTextPair Conflict { get; } =
        Pair("Conflict", "Conflict", "This conflicts with the record as it is now.");

    public static ErrorTextPair Unexpected { get; } =
        Pair("Unexpected", "Something went wrong", "An unexpected error occurred.");

    public static ErrorTextPair Failure { get; } =
        Pair("Failure", "Something went wrong", "An unexpected error occurred.");

    public static ErrorTextPair Unauthorized { get; } =
        Pair("Unauthorized", "Not signed in", "Sign in to do this.");

    public static ErrorTextPair VersionRequired { get; } =
        Pair("VersionRequired", "Version required", "This change must say which version of the record it was made against.");

    public static ErrorTextPair VersionStale { get; } =
        Pair("VersionStale", "Changed meanwhile", "The record changed after it was loaded. Reload it and try again.");

    public static ErrorTextPair VersionNotPublished { get; } =
        Pair("VersionNotPublished", "Something went wrong", "The request succeeded but reported no record version.");

    /// <summary>The pair declared for <paramref name="code"/>, or the one for <paramref name="type"/> when none is.</summary>
    public static ErrorTextPair For(string code, ErrorType type)
    {
        ArgumentNullException.ThrowIfNull(code);

        return byCode.GetValueOrDefault(code) ?? type switch
        {
            ErrorType.NotFound => NotFound,
            ErrorType.Forbidden => Forbidden,
            ErrorType.Conflict => Conflict,
            ErrorType.Unauthorized => Unauthorized,
            ErrorType.Unexpected => Unexpected,
            _ => Failure,
        };
    }

    private static ErrorTextPair Pair(string code, string title, string detail)
    {
        TextCategory error = category.Child(code);
        ErrorTextPair pair = new(error.Text("Title", title), error.Text("Detail", detail));
        byCode.Add(GeneralPrefix + code, pair);
        return pair;
    }
}
