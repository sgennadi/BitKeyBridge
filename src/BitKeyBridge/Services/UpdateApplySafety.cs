namespace BitKeyBridge;

/// <summary>
/// Pure, offline-testable policy for elevated apply plans. The background task
/// must never be used as a general-purpose privileged file replacement tool.
/// GUI-initiated manual and existing portable updates keep their established
/// targets, while scheduled work is restricted to the machine installation.
/// </summary>
internal static class UpdateApplySafety
{
    internal static void ValidateScheduledBackgroundPlan(
        UpdateApplyPlan plan, string machineExecutable)
    {
        if (!plan.ScheduledBackground)
            return;

        if (!plan.Automatic || plan.Rollback || plan.RestartGui ||
            plan.TargetExecutables is null ||
            plan.TargetExecutables.Count != 1 ||
            string.IsNullOrWhiteSpace(plan.TargetExecutables[0]) ||
            !string.Equals(Path.GetFullPath(plan.TargetExecutables[0]),
                Path.GetFullPath(machineExecutable),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Scheduled background apply must target only the protected installed BitKeyBridge executable.");
        }
    }
}
