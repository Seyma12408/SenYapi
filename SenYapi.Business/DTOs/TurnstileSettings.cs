namespace SenYapi.Business.DTOs;

public class TurnstileSettings
{
    public string SiteKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
}
