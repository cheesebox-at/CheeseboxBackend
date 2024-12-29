using Backend.Enums;
using Backend.Models.Identity;

namespace Backend.DTOs;

public class CreateUserDto
{
    public EUserType EUserType { get; set; } = EUserType.User;
    public long[] RolesIds { get; set; } = [];
    public required string Email { get; set; }
    public bool EmailVerified { get; set; } = false;
    public required string Password { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public AddressModel[] AddressData { get; set; } = [];
}