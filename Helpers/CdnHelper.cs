using Azure;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Cdn.Models;
using Azure.ResourceManager.Cdn;

namespace Flygcert.LetsEncryptAzureCdn.Helpers
{
    public class CdnHelper(string subscriptionId)
    {
        private readonly ArmClient client = new(new DefaultAzureCredential(), subscriptionId);

        public async Task EnableHttpsForCustomDomain(string resourceGroupName, string cdnProfileName, string cdnEndpointName,
            string cdnCustomDomainName, string certificateName, string certificateVerison, string keyVaultName)
        {
            SubscriptionResource subscription = await client.GetDefaultSubscriptionAsync();
            ResourceGroupResource resourceGroup = await subscription.GetResourceGroups().GetAsync(resourceGroupName);

            ProfileCollection profileCollection = resourceGroup.GetProfiles();
            ProfileResource profile = await profileCollection.GetAsync(cdnProfileName);

            CdnEndpointCollection cdnEndpointCollection = profile.GetCdnEndpoints();
            CdnEndpointResource cdnEndpoint = await cdnEndpointCollection.GetAsync(cdnEndpointName);
            CdnCustomDomainCollection cdnCustomDomainCollection = cdnEndpoint.GetCdnCustomDomains();
            CdnCustomDomainResource cdnCustomDomain = await cdnCustomDomainCollection.GetAsync(cdnCustomDomainName);

            KeyVaultCertificateSource keyVaultCertificate = new(
                    KeyVaultCertificateSourceType.KeyVaultCertificateSource,
                    subscription.Id,
                    resourceGroupName,
                    keyVaultName,
                    certificateName,
                    CertificateUpdateAction.NoAction,
                    CertificateDeleteAction.NoAction
            );

            UserManagedHttpsContent userManagedHttpsContent = new(
                SecureDeliveryProtocolType.ServerNameIndication,
                keyVaultCertificate
            );

            await cdnCustomDomain.EnableCustomHttpsAsync(WaitUntil.Completed, userManagedHttpsContent);
        }
    }
}
