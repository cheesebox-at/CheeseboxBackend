namespace Backend.Models.User;

public class UserMetrics
{
    public string[] PreviousUsernames { get; set; } = [];
    public DateTime RegisteredAt { get; set; }
    public DateTime LastLogin { get; set; }
    public DateTime LastActivity { get; set; }
    public DateTime LastUpdate { get; set; }
    public DateTime LastPasswordChangeAt { get; set; }
}