namespace Backend.Models;

public class BlockedTimeFrames
{
    public required DateTime StartDate { get; set; } // including hours
    public required DateTime EndDate { get; set; } // including hours
}