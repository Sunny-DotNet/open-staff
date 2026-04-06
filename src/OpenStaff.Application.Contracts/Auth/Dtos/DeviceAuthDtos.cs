namespace OpenStaff.Application.Contracts.Auth.Dtos;

public class DeviceCodeDto
{
    public string UserCode { get; set; } = string.Empty;
    public string VerificationUri { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
    public int Interval { get; set; }
}

public class DeviceAuthPollDto
{
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int? Interval { get; set; }
}
