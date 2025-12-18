namespace Backend.DTOs;

public class GoogleLoginDto
{
    /// <summary>
    /// The ID token received from Google Sign-In on the frontend
    /// </summary>
    public required string IdToken { get; set; }
}

