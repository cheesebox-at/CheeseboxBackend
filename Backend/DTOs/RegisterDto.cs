using Backend.Models.Identity;

namespace Backend.DTOs;

public struct RegisterDto
{
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string Email { get; set; }
    public required string Password { get; set; }
    public string? ConfirmPassword { get; set; }

    public AddressModel?[] AddressData { get; set; }
}