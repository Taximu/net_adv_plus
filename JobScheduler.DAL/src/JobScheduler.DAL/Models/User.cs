namespace JobScheduler.DAL.Models;

public class User
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string Role { get; set; } = "user";
    public string Status { get; set; } = "active";
    public string Preferences { get; set; } = "{}";
    public int MaxJobs { get; set; } = 100;
    public int MaxConcurrentJobs { get; set; } = 10;
    public int JobsCreatedCount { get; set; } = 0;
    public string PasswordHash { get; set; } = string.Empty;
    public bool MfaEnabled { get; set; } = false;
    public DateTime? LastLoginAt { get; set; }
    public string? ApiKey { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}
