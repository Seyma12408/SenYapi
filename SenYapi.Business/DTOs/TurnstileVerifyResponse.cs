using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SenYapi.Business.DTOs;

public class TurnstileVerifyResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("error-codes")]
    public List<string>? ErrorCodes { get; set; }

    [JsonPropertyName("challenge_ts")]
    public string? ChallengeTs { get; set; }

    [JsonPropertyName("hostname")]
    public string? Hostname { get; set; }

    [JsonPropertyName("action")]
    public string? Action { get; set; }

    [JsonPropertyName("cdata")]
    public string? Cdata { get; set; }
}
