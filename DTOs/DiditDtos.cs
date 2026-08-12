namespace SubiteAPI.DTOs;

public class DiditSessionResultDto
{
    public string SessionId { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsVerified { get; set; }
}

public class DiditStatusDto
{
    public bool IsVerified { get; set; }
    public string? SessionId { get; set; }
    public string? Status { get; set; }
    public string? Message { get; set; }
}
