namespace BitKeyBridge;

/// <summary>
/// Keeps ordinary Helpdesk GUI sessions in the caller's security context.
/// Only privileged CLI administration is a candidate for UAC; suppressing
/// the prompt does not bypass the authorization requirement.
/// </summary>
internal static class StartupPrivilegePolicy
{
    internal static bool NeedsElevation(
        bool isCli,
        bool readOnlyStatusCommand,
        bool currentUserVaultCommand,
        bool identityBoundCommand,
        bool isAdministrator) =>
        isCli &&
        !readOnlyStatusCommand &&
        !currentUserVaultCommand &&
        !identityBoundCommand &&
        !isAdministrator;
}
