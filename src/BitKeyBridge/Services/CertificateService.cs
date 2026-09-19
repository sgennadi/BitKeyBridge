using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace BitKeyBridge;

public sealed class CertificateService
{
    public X509Certificate2 FindByThumbprint(string thumbprint)
    {
        var normalized = Normalize(thumbprint);
        foreach (var location in new[] { StoreLocation.CurrentUser, StoreLocation.LocalMachine })
        {
            using var store = new X509Store(StoreName.My, location);
            store.Open(OpenFlags.ReadOnly);
            var cert = store.Certificates
                .OfType<X509Certificate2>()
                .FirstOrDefault(x => Normalize(x.Thumbprint) == normalized);
            if (cert is null) continue;
            if (!cert.HasPrivateKey) throw new InvalidOperationException($"Certificate {normalized} has no private key.");
            if (cert.NotAfter <= DateTime.Now) throw new InvalidOperationException($"Certificate {normalized} expired on {cert.NotAfter}.");
            return cert;
        }
        throw new InvalidOperationException($"Certificate {normalized} was not found in CurrentUser\\My or LocalMachine\\My.");
    }

    public X509Certificate2 CreateLocalMachineCertificate(string subjectName, int years = 3)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            new X500DistinguishedName($"CN={subjectName}"),
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

        using var generated = request.CreateSelfSigned(DateTimeOffset.Now.AddMinutes(-5), DateTimeOffset.Now.AddYears(years));
        var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
        var pfx = generated.Export(X509ContentType.Pfx, password);
        try
        {
            var persisted = new X509Certificate2(
                pfx,
                password,
                X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet);
            using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadWrite);
            store.Add(persisted);
            return persisted;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(pfx);
        }
    }

    private static string Normalize(string value) =>
        new(value.Where(Uri.IsHexDigit).Select(char.ToUpperInvariant).ToArray());
}
