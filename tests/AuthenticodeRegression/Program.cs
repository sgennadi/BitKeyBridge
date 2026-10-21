using BitKeyBridge;

static void Require(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}
static void MustDeny(Action action)
{
    try { action(); }
    catch (InvalidOperationException) { return; }
    throw new Exception("Signature policy unexpectedly allowed the payload.");
}
const string pin = "1234567890ABCDEF1234567890ABCDEF12345678";
Require(AuthenticodeVerificationService.ThumbprintMatches(pin.ToLowerInvariant(), pin), "Case normalization");
Require(AuthenticodeVerificationService.ThumbprintMatches(pin, "12:34 " + pin[4..]), "Separators");
foreach (var invalid in new string?[] { null, "", "123", new('Z', 40), new('0', 40) })
    Require(!AuthenticodeVerificationService.ThumbprintMatches(pin, invalid), "Invalid/wrong pin accepted");
if (args.Length == 2)
{
    Require(OperatingSystem.IsWindows(), "Native signature tests require Windows.");
    var path = args[0];
    var expected = args[1];
    var result = AuthenticodeVerificationService.Verify(path);
    Require(result.Trusted && result.Signed, "Signed fixture is not trusted: " + result.Status);
    Require(AuthenticodeVerificationService.ThumbprintMatches(result.SignerThumbprint, expected), "Wrong leaf certificate");
    var config = new AppConfig { RequireTrustedUpdateSignature = true, TrustedUpdateSignerThumbprint = expected };
    AuthenticodeVerificationService.EnforcePolicy(config, path);
    config.TrustedUpdateSignerThumbprint = new string('0', 40);
    MustDeny(() => AuthenticodeVerificationService.EnforcePolicy(config, path));
    config.RequireTrustedUpdateSignature = false;
    MustDeny(() => AuthenticodeVerificationService.EnforcePolicy(config, path));
    config.TrustedUpdateSignerThumbprint = expected;
    MustDeny(() => AuthenticodeVerificationService.EnforcePolicy(config, typeof(AppConfig).Assembly.Location));
    var damaged = Path.Combine(Path.GetTempPath(), "bkb-tampered-" + Guid.NewGuid() + ".exe");
    try
    {
        File.Copy(path, damaged);
        using (var stream = new FileStream(damaged, FileMode.Open, FileAccess.ReadWrite))
        {
            stream.Position = 0x40; // Hashed DOS stub; preserve PE structure and certificate table.
            var value = stream.ReadByte();
            stream.Position = 0x40;
            stream.WriteByte((byte)(value ^ 1));
        }
        Require(!AuthenticodeVerificationService.Verify(damaged).Trusted, "Tampered payload trusted");
        MustDeny(() => AuthenticodeVerificationService.EnforcePolicy(config, damaged));
    }
    finally { File.Delete(damaged); }
}
Console.WriteLine("Authenticode regression checks passed.");

// Minimal configuration boundary; the actual production verifier is linked above.
namespace BitKeyBridge
{
    public sealed class AppConfig
    {
        public bool AuditBrokerEnabled { get; set; }
        public bool RequireProtectedAuditForRecovery { get; set; }
        public bool RequireTrustedUpdateSignature { get; set; }
        public string TrustedUpdatePublisher { get; set; } = "";
        public string TrustedUpdateSignerThumbprint { get; set; } = "";
    }
}
