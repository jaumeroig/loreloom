namespace LoreLoom.Api.Services;

public class EmailOptions
{
    public const string SectionName = "Email";

    public string SmtpHost { get; set; } = "";
    public int SmtpPort { get; set; } = 587;
    public string SmtpUser { get; set; } = "";
    public string SmtpPassword { get; set; } = "";
    public string FromAddress { get; set; } = "noreply@loreloom.app";
    public string FromName { get; set; } = "LoreLoom";
    public bool EnableSsl { get; set; } = true;
    public string BaseUrl { get; set; } = "http://localhost:5173";
}
