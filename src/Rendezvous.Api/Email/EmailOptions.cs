namespace Rendezvous.Api.Email;

public class EmailOptions
{
    public string Provider { get; set; } = "Disabled";
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
    public ResendOptions Resend { get; set; } = new();
}

public class ResendOptions
{
    public string ApiKey { get; set; } = string.Empty;
}
