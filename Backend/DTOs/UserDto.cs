namespace Backend.DTOs;

/// <summary>
/// Data Transfer Object for User data - excludes sensitive information
/// </summary>
public class UserDto
{
    public long UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public bool EmailVerified { get; set; }
    public string UserType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public int TotalOrders { get; set; }
    public decimal TotalSpent { get; set; }
}

/// <summary>
/// Detailed User DTO with address information
/// </summary>
public class UserDetailDto : UserDto
{
    public AddressDto[] Addresses { get; set; } = [];
    public UserMetricsDto Metrics { get; set; } = new();
}

/// <summary>
/// Address DTO
/// </summary>
public class AddressDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

/// <summary>
/// User Metrics DTO
/// </summary>
public class UserMetricsDto
{
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public int LoginCount { get; set; }
}

/// <summary>
/// User list response with pagination
/// </summary>
public class UserListResponseDto
{
    public List<UserDto> Users { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

/// <summary>
/// DTO for updating user profile
/// </summary>
public class UpdateUserProfileDto
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Phone { get; set; }
    public string? Street { get; set; }
    public string? HouseNumber { get; set; }
    public string? PostalCode { get; set; }
    public string? City { get; set; }
}

/// <summary>
/// DTO for changing password
/// </summary>
public class ChangePasswordDto
{
    public string OldPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}

