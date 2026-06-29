using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace ChatApplicationServer.Security
{
    internal class CertificateHelper
    {
        public static void GenerateSelfSigned(string pfxPath, string cerPath, string password, string cn)
        {
            using var rsa = RSA.Create(2048);
            var req = new CertificateRequest($"CN={cn}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, false));

            var san = new SubjectAlternativeNameBuilder();
            san.AddDnsName(cn);
            san.AddDnsName("localhost");
            san.AddIpAddress(IPAddress.Loopback);
            req.CertificateExtensions.Add(san.Build());

            using var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(2));
            File.WriteAllBytes(pfxPath, cert.Export(X509ContentType.Pfx, password));
            File.WriteAllBytes(cerPath, cert.Export(X509ContentType.Cert));
        }

        public static X509Certificate2 Load(string pfxPath, string password)
            => X509CertificateLoader.LoadPkcs12FromFile(pfxPath, password, X509KeyStorageFlags.Exportable);
    }
}
