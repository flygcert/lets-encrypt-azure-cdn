using Azure;
using Azure.Identity;
using Azure.Security.KeyVault.Certificates;

namespace Flygcert.LetsEncryptAzureCdn.Helpers
{
    public class KeyVaultCertificateHelper(string keyVaultName)
    {
        private readonly CertificateClient certificateClient = new(
                new Uri($"https://{keyVaultName}.vault.azure.net"),
                new DefaultAzureCredential()
            );

        public async Task<(string, string)> ImportCertificate(string certificateName, byte[] certificate, string password)
        {
            var result = await certificateClient.ImportCertificateAsync(new ImportCertificateOptions(certificateName, certificate)
            {
                Password = password
            });

            return (result.Value.Properties.Name, result.Value.Properties.Version);
        }

        public async Task<DateTimeOffset?> GetCertificateExpiryAsync(string certificateName)
        {
            try
            {
                return (await certificateClient.GetCertificateAsync(certificateName)).Value.Properties.ExpiresOn;
            }
            catch (RequestFailedException e)
            {
                if (e.Status == 404)
                {
                    return null;
                }
                else
                {
                    throw;
                }
            }
        }
    }
}
