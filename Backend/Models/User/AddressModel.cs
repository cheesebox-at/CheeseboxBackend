namespace Backend.Models.Identity;

public class AddressModel
{
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string Street { get; set; }
    public required string City { get; set; }
    public required string ZipCode { get; set; }
    public required string State { get; set; }
    public required string Country { get; set; }
}