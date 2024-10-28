using Azure;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

namespace Flygcert.LetsEncryptAzureCdn.Helpers
{
    public class KeyVaultSecretHelper(string keyVaultName)
    {
        private readonly SecretClient secretClient = new(
                new Uri($"https://{keyVaultName}.vault.azure.net"),
                new DefaultAzureCredential()
            );

        public async Task<string?> GetSecretAsync(string secretName)
        {
            try
            {
                return (await secretClient.GetSecretAsync(secretName)).Value.Value;
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

        public async Task SetSecretAsync(string secretName, string secretValue)
        {
            await secretClient.SetSecretAsync(secretName, secretValue);
        }
    }
}
