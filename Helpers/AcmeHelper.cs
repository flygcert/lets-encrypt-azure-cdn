using Certes;
using Certes.Acme;
using Flygcert.LetsEncryptAzureCdn.Exceptions;
using Microsoft.Extensions.Logging;

namespace Flygcert.LetsEncryptAzureCdn.Helpers
{
    public class AcmeHelper(ILogger log)
    {
        private AcmeContext acmeContext;
        private IOrderContext orderContext;
        private IChallengeContext challengeContext;

        private readonly ILogger _logger = log;
        private const int maxNumberOfRetries = 20;

        public async Task<string> InitWithNewAccountAsync(string emailId)
        {
            acmeContext = new AcmeContext(WellKnownServers.LetsEncryptV2);
            //acmeContext = new AcmeContext(WellKnownServers.LetsEncryptStagingV2);
            await acmeContext.NewAccount(emailId, true);
            return acmeContext.AccountKey.ToPem();
        }

        public void InitWithExistingAccount(string acmeAccountPem)
        {
            acmeContext = new AcmeContext(WellKnownServers.LetsEncryptV2, KeyFactory.FromPem(acmeAccountPem));
            //acmeContext = new AcmeContext(WellKnownServers.LetsEncryptStagingV2, KeyFactory.FromPem(acmeAccountPem));
        }

        public async Task CreateOrderAsync(string domainName)
        {
            orderContext = await acmeContext.NewOrder([domainName]);
        }

        public async Task<string> GetDnsAuthorizationTextAsync()
        {
            var authorization = (await orderContext.Authorizations()).First();

            challengeContext = await authorization.Dns();

            return acmeContext.AccountKey.DnsTxt(challengeContext.Token);
        }

        public async Task ValidateDnsAuthorizationAsync()
        {
            var challengeResult = await challengeContext.Validate();

            int numberOfRetries = 0;

            while (challengeResult.Status.HasValue && challengeResult.Status.Value == Certes.Acme.Resource.ChallengeStatus.Pending 
                && numberOfRetries <= maxNumberOfRetries)
            {
                _logger.LogInformation("Validation is pending. Will retry in 1 second. Number of retries - {numberOfRetries}", numberOfRetries);
                await Task.Delay(1 * 1000);
                challengeResult = await challengeContext.Resource();
                numberOfRetries += 1;
            }

            if(numberOfRetries >= maxNumberOfRetries)
            {
                throw new ChallengeValidationFailedException();
            }

            if (!challengeResult.Status.HasValue || challengeResult.Status.Value != Certes.Acme.Resource.ChallengeStatus.Valid)
            {
                _logger.LogError("Unable to validate challenge - {detail} - {problems}", challengeResult.Error.Detail, string.Join('~', challengeResult.Error.Subproblems.Select(x => x.Detail)));
                throw new ChallengeValidationFailedException();
            }
        }

        public async Task<byte[]> GetPfxCertificateAsync(string password, string certificateCountryName, string certificateState, string certificateLocality,
            string certificateOrganization, string certificateOrganizationUnit, string domainName, string friendlyName)
        {
            var privateKey = KeyFactory.NewKey(KeyAlgorithm.RS256);

            var cert = await orderContext.Generate(new CsrInfo
            {
                CountryName = certificateCountryName,
                State = certificateState,
                Locality = certificateLocality,
                Organization = certificateOrganization,
                OrganizationUnit = certificateOrganizationUnit,
                CommonName = domainName,
            }, privateKey);

            var pfxBuilder = cert.ToPfx(privateKey);

            return pfxBuilder.Build(friendlyName, password);
        }
    }
}
