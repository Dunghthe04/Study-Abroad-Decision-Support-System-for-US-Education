namespace StudyAbroad.Infrastructure.Auth
{
    public class SmtpOptions
    {
        public const string SectionName = "Smtp";

        public string Host { get; set; } = "smtp.gmail.com";
        public int Port { get; set; } = 587;
        public string User { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FromEmail { get; set; } = "no-reply@usas.edu.vn";
        public string FromName { get; set; } = "USAS Education Support";
        public bool EnableSsl { get; set; } = true;
    }
}
