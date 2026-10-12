namespace BitKeyBridge;

/// <summary>Central Graph permission modes. Existing grants require manual tenant review.</summary>
public static class ManagedPermissionPolicy
{
    public const string IntuneRead = "DeviceManagementManagedDevices.Read.All";
    public const string IntuneRotation = "DeviceManagementManagedDevices.ReadWrite.All";

    public static string Permission(bool allowRotation) =>
        allowRotation ? IntuneRotation : IntuneRead;

    public static string DelegatedScope(bool allowRotation) =>
        "https://graph.microsoft.com/" + Permission(allowRotation);
}
