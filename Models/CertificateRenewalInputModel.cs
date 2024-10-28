namespace Flygcert.LetsEncryptAzureCdn.Models
{
    public class CertificateRenewalInputModel
    {
        public string DnsZoneResourceGroup { get; set; } = string.Empty;
        public string DnsZoneName { get; set; } = string.Empty;
        public string DomainName { get; set; } = string.Empty;
        public string CertificateCountryName { get; set; } = string.Empty;
        public string CertificateState { get; set; } = string.Empty;
        public string CertificateLocality { get; set; } = string.Empty;
        public string CertificateOrganization { get; set; } = string.Empty;
        public string CertificateOrganizationUnit { get; set; } = string.Empty;
        public string CdnProfileName { get; set; } = string.Empty;
        public string CdnEndpointName { get; set; } = string.Empty;
        public string CdnCustomDomainName { get; set; } = string.Empty;
        public string CdnResourceGroup { get; set; } = string.Empty;
        public string KeyVaultName { get; set; } = string.Empty;
        public string KeyVaultResourceGroup { get; set; } = string.Empty;
    }
}
