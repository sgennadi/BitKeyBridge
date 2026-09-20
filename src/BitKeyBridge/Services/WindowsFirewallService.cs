using System.Runtime.InteropServices;

namespace BitKeyBridge;

public static class WindowsFirewallService
{
    public const string RuleName = "BitKeyBridge Remote API";
    private const int NetFwActionAllow = 1;
    private const int NetFwIpProtocolTcp = 6;
    private const int NetFwProfile2Domain = 1;
    private const int NetFwProfile2Private = 2;

    public static void EnsureRemoteApiRule(int port)
    {
        if (!OperatingSystem.IsWindows()) return;
        port = Math.Clamp(port, 1024, 65535);

        object? policy = null;
        object? rules = null;
        object? rule = null;
        try
        {
            var policyType = Type.GetTypeFromProgID("HNetCfg.FwPolicy2")
                ?? throw new InvalidOperationException("Windows Firewall COM API is unavailable.");
            policy = Activator.CreateInstance(policyType)
                ?? throw new InvalidOperationException("Could not create Windows Firewall policy object.");
            dynamic p = policy;
            rules = p.Rules;

            try
            {
                dynamic existing = ((dynamic)rules).Item(RuleName);
                ((dynamic)rules).Remove(RuleName);
                if (existing is not null && Marshal.IsComObject(existing))
                    Marshal.FinalReleaseComObject(existing);
            }
            catch { }

            var ruleType = Type.GetTypeFromProgID("HNetCfg.FWRule")
                ?? throw new InvalidOperationException("Windows Firewall rule COM API is unavailable.");
            rule = Activator.CreateInstance(ruleType)
                ?? throw new InvalidOperationException("Could not create Windows Firewall rule object.");
            dynamic r = rule;
            r.Name = RuleName;
            r.Description = "Inbound TLS access to the BitKeyBridge Remote API.";
            r.Protocol = NetFwIpProtocolTcp;
            r.LocalPorts = port.ToString();
            r.Direction = 1; // NET_FW_RULE_DIR_IN
            r.Enabled = true;
            r.Profiles = NetFwProfile2Domain | NetFwProfile2Private;
            r.Action = NetFwActionAllow;
            r.EdgeTraversal = false;
            r.InterfaceTypes = "All";
            ((dynamic)rules).Add(r);
        }
        finally
        {
            ReleaseCom(rule);
            ReleaseCom(rules);
            ReleaseCom(policy);
        }
    }

    public static void RemoveRemoteApiRule()
    {
        if (!OperatingSystem.IsWindows()) return;

        object? policy = null;
        object? rules = null;
        try
        {
            var policyType = Type.GetTypeFromProgID("HNetCfg.FwPolicy2");
            if (policyType is null) return;
            policy = Activator.CreateInstance(policyType);
            if (policy is null) return;
            dynamic p = policy;
            rules = p.Rules;
            try { ((dynamic)rules).Remove(RuleName); } catch { }
        }
        finally
        {
            ReleaseCom(rules);
            ReleaseCom(policy);
        }
    }

    private static void ReleaseCom(object? value)
    {
        if (value is null || !Marshal.IsComObject(value)) return;
        try { Marshal.FinalReleaseComObject(value); } catch { }
    }
}
