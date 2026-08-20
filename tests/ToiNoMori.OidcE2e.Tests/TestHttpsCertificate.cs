using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace ToiNoMori.OidcE2e.Tests;

internal sealed class TestHttpsCertificate : IDisposable
{
    private readonly RSA _rsa;

    private TestHttpsCertificate(RSA rsa, X509Certificate2 certificate)
    {
        _rsa = rsa;
        Certificate = certificate;
    }

    public X509Certificate2 Certificate { get; }

    public static TestHttpsCertificate Create()
    {
        var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=localhost",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature,
            true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new("1.3.6.1.5.5.7.3.1") },
            true));
        var subjectNames = new SubjectAlternativeNameBuilder();
        subjectNames.AddDnsName("localhost");
        subjectNames.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(subjectNames.Build());

        var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddHours(2));
        return new(rsa, certificate);
    }

    public bool Matches(X509Certificate2? other) =>
        other is not null
        && CryptographicOperations.FixedTimeEquals(Certificate.RawData, other.RawData);

    public void Dispose()
    {
        Certificate.Dispose();
        _rsa.Dispose();
    }
}
