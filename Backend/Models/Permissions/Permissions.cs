namespace Backend.Models.Permissions;

public class Permissions
{
    public static class Main
    {
        public const string IsAdmin = "Permissions.Main.IsAdmin";
    }

    public class Users
    {
        public const string Create = "Permissions.Users.Create";
        public const string Delete = "Permissions.Users.Delete";
        public const string Edit = "Permissions.Users.Edit";
        public const string View = "Permissions.Users.View";
    }
    public class Roles
    {
        public const string Create = "Permissions.Roles.Create";
        public const string Delete = "Permissions.Roles.Delete";
        public const string Edit = "Permissions.Roles.Edit";
        public const string View = "Permissions.Roles.View";
    }
}