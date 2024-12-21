namespace Backend.Models;

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
}