using System.Text.Json.Serialization;

namespace AutoMinigameWinForms.Models;

public sealed class LicenseValidationRequest
{
    [JsonPropertyName("license_key")]
    public string LicenseKey { get; set; } = string.Empty;

    [JsonPropertyName("product_code")]
    public string ProductCode { get; set; } = string.Empty;

    [JsonPropertyName("machine_id")]
    public string MachineId { get; set; } = string.Empty;

    [JsonPropertyName("machine_name")]
    public string MachineName { get; set; } = string.Empty;

    [JsonPropertyName("user_name")]
    public string UserName { get; set; } = string.Empty;
}

public sealed class LicenseValidationResponse
{
    [JsonPropertyName("valid")]
    public bool Valid { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("license_owner")]
    public string LicenseOwner { get; set; } = string.Empty;

    [JsonPropertyName("product_code")]
    public string ProductCode { get; set; } = string.Empty;

    [JsonPropertyName("expires_at")]
    public string ExpiresAt { get; set; } = string.Empty;
}

public enum LicenseValidationFailureKind
{
    None,
    Input,
    InvalidLicense,
    Server,
    Network,
    Timeout,
}
