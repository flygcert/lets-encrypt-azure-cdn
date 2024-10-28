using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Flygcert.LetsEncryptAzureCdn.Models;
using Flygcert.LetsEncryptAzureCdn.Helpers;
using Microsoft.Extensions.Configuration;

namespace Flygcert.LetsEncryptAzureCdn
{
    public class ApplyOrRenewCertificate(ILoggerFactory loggerFactory)
    {
        private readonly ILogger _logger = loggerFactory.CreateLogger<ApplyOrRenewCertificate>();

        [Function("ApplyOrRenewCertificate")]
        public async Task RunAsync([TimerTrigger("0 17 23 * * *")] TimerInfo myTimer)
        {
            _logger.LogInformation("C# Timer trigger function executed at: {date}", DateTime.Now);

            if (myTimer.ScheduleStatus is not null)
            {
                _logger.LogInformation("Next timer schedule at: {time}", myTimer.ScheduleStatus.Next);
            }

            string subscriptionId = Environment.GetEnvironmentVariable("SubscriptionId") ?? throw new ArgumentNullException(null, "Missing 'SubscriptionId' parameter");

            var config = new ConfigurationBuilder()
                                .SetBasePath(Environment.CurrentDirectory)
                                .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                                .AddEnvironmentVariables()
                                .Build();

            var certificateDetails = new List<CertificateRenewalInputModel>();

            config.GetSection("CertificateDetails").Bind(certificateDetails);

            foreach (var certifcate in certificateDetails)
            {
                _logger.LogInformation("Processing certificate - {certifcate}", certifcate.DomainName);

                var acmeHelper = new AcmeHelper(_logger);
                var certificateHelper = new KeyVaultCertificateHelper(certifcate.KeyVaultName);

                await InitAcme(_logger, certifcate, acmeHelper);

                string domainName = certifcate.DomainName;

                if (domainName.StartsWith('*'))
                {
                    domainName = domainName[1..];
                }

                _logger.LogInformation("Calculated domain name is {domainName}", domainName);

                string keyVaultCertificateName = domainName.Replace(".", "-");

                _logger.LogInformation("Getting expiry for {keyVaultCertificateName} in Key Vault certifictes", keyVaultCertificateName);

                var certificateExpiry = await certificateHelper.GetCertificateExpiryAsync(keyVaultCertificateName);

                if (certificateExpiry.HasValue && certificateExpiry.Value.Subtract(DateTime.UtcNow).TotalDays > 7)
                {
                    _logger.LogInformation("No certificates to renew.");

                    continue;
                }

                _logger.LogInformation("Creating order for certificates");

                await acmeHelper.CreateOrderAsync(certifcate.DomainName);

                _logger.LogInformation("Authorization created");

                await FetchAndCreateDnsRecords(_logger, subscriptionId, certifcate, acmeHelper, domainName);

                _logger.LogInformation("Validating DNS challenge");

                await acmeHelper.ValidateDnsAuthorizationAsync();

                _logger.LogInformation("Challenge validated");

                string password = Guid.NewGuid().ToString();

                var pfx = await acmeHelper.GetPfxCertificateAsync(password, certifcate.CertificateCountryName, certifcate.CertificateState, certifcate.CertificateLocality,
                    certifcate.CertificateOrganization, certifcate.CertificateOrganizationUnit, certifcate.DomainName, domainName);

                _logger.LogInformation("Certificate built");

                (string certificateName, string certificateVerison) = await certificateHelper.ImportCertificate(keyVaultCertificateName, pfx, password);

                _logger.LogInformation("Certificate imported");
            }
        }

        private static async Task InitAcme(ILogger log, CertificateRenewalInputModel certifcate, AcmeHelper acmeHelper)
        {
            var secretHelper = new KeyVaultSecretHelper(certifcate.KeyVaultName);
            var acmeAccountPem = await secretHelper.GetSecretAsync("AcmeAccountKeyPem");

            if (string.IsNullOrWhiteSpace(acmeAccountPem))
            {
                log.LogInformation("Acme Account not found.");

                string emailId = Environment.GetEnvironmentVariable("AcmeAccountEmail") ?? throw new ArgumentNullException(null, "Missing 'AcmeAccountEmail' parameter");

                string pem = await acmeHelper.InitWithNewAccountAsync(emailId);

                log.LogInformation("Acme account created");

                await secretHelper.SetSecretAsync("AcmeAccountKeyPem", pem);

                log.LogInformation("Secret uploaded to key vault");
            }
            else
            {
                acmeHelper.InitWithExistingAccount(acmeAccountPem);
            }
        }

        private static async Task FetchAndCreateDnsRecords(ILogger log, string subscriptionId, CertificateRenewalInputModel certifcate, AcmeHelper acmeHelper, string domainName)
        {
            var dnsHelper = new DnsHelper(subscriptionId);

            log.LogInformation("Fetching DNS authorization");

            var dnsText = await acmeHelper.GetDnsAuthorizationTextAsync();
            var dnsName = ("_acme-challenge." + domainName).Replace("." + certifcate.DnsZoneName, "").Trim();

            log.LogInformation("Got DNS challenge {dnsText} for {dnsName}", dnsText, dnsName);

            await CreateDnsTxtRecordsIfNecessary(log, certifcate, dnsHelper, dnsText, dnsName);

            log.LogInformation("Waiting 60 seconds for DNS propagation");

            await Task.Delay(60 * 1000);
        }

        private static async Task CreateDnsTxtRecordsIfNecessary(ILogger log, CertificateRenewalInputModel certifcate, DnsHelper dnsHelper, string recordText, string recordName)
        {
            var txtRecords = await dnsHelper.FetchTxtRecordsAsync(certifcate.DnsZoneResourceGroup, certifcate.DnsZoneName, recordName);

            if (txtRecords == null || !txtRecords.Contains(recordText))
            {
                await dnsHelper.CreateTxtRecord(certifcate.DnsZoneResourceGroup, certifcate.DnsZoneName, recordName, recordText);

                log.LogInformation("Created DNS TXT records");
            }
        }
    }
}
