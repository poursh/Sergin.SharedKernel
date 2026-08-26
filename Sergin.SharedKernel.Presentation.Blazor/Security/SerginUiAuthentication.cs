namespace Sergin.SharedKernel.Presentation.Blazor.Security;

/// <summary>
/// What the shell needs to know about how the host authenticates: whether there is a session to end,
/// and where to end it.
/// </summary>
/// <remarks>
/// A host running on the configured development user has nothing to sign out of, so the shell must not
/// render a sign-out control there. The bootstrap knows the mode and this library does not — and cannot,
/// since the dependency runs the other way — so it registers this instead. A plain singleton composed in
/// code, bound to no configuration key, following <c>SerginUiModuleCatalog</c>'s precedent rather than
/// <c>DevUserOptions</c>'.
/// </remarks>
public sealed record SerginUiAuthentication(bool CanSignOut, string LogoutPath)
{
    /// <summary>No interactive session: the user is fixed by configuration.</summary>
    public static SerginUiAuthentication Disabled { get; } = new(CanSignOut: false, LogoutPath: string.Empty);
}
