using Azure;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Dns;
using Azure.ResourceManager.Dns.Models;

namespace Flygcert.LetsEncryptAzureCdn.Helpers
{
    public class DnsHelper(string subscriptionId)
    {
        private readonly ArmClient client = new(new DefaultAzureCredential(), subscriptionId);

        public async Task<IList<string>?> FetchTxtRecordsAsync(string resourceGroupName, string dnsZoneName, string recordName)
        {
            SubscriptionResource subscription = await client.GetDefaultSubscriptionAsync();
            ResourceGroupResource resourceGroup = await subscription.GetResourceGroups().GetAsync(resourceGroupName);
            DnsZoneCollection dnsZoneCollection = resourceGroup.GetDnsZones();
            DnsZoneResource dnsZone = await dnsZoneCollection.GetAsync(dnsZoneName);

            DnsTxtRecordCollection txtRecordCollection = dnsZone.GetDnsTxtRecords();
            try
            {
                DnsTxtRecordResource txtRecord = await txtRecordCollection.GetAsync(recordName);

                if (txtRecord.Data.DnsTxtRecords.Count > 0) 
                {
                    return txtRecord.Data.DnsTxtRecords[0].Values;
                }
                else
                {
                    return null;
                }
            }
            catch (Azure.RequestFailedException e)
            {
                if (e.ErrorCode == "NotFound")
                {
                    return null;
                }
                else
                {
                    throw;
                }
            }
        }

        public async Task CreateTxtRecord(string resourceGroupName, string dnsZoneName, string recordName, string recordValue)
        {
            SubscriptionResource subscription = await client.GetDefaultSubscriptionAsync();
            ResourceGroupResource resourceGroup = await subscription.GetResourceGroups().GetAsync(resourceGroupName);
            DnsZoneCollection dnsZoneCollection = resourceGroup.GetDnsZones();
            DnsZoneResource dnsZone = await dnsZoneCollection.GetAsync(dnsZoneName);

            await dnsZone.GetDnsTxtRecords().CreateOrUpdateAsync(WaitUntil.Completed, recordName, new DnsTxtRecordData()
                {
                    TtlInSeconds = 300,
                    DnsTxtRecords =
                    {
                        new DnsTxtRecordInfo()
                        {
                            Values = { recordValue }
                        }
                    }
                }
            );
        }
    }
}
