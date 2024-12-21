namespace Backend.Models.User;

public class RoleModel
{
    public long Id { get; set; }
    public string Name { get; set; } = "Not defined";
    public string[] Permissions { get; set; } = [];
}