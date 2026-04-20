namespace FirebirdWeb.Models
{
    public class RegistrationRequest
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Mobile { get; set; } = string.Empty;
        public string Passwd { get; set; } = string.Empty;
        public string LicenseKey { get; set; } = string.Empty;
    }
}
