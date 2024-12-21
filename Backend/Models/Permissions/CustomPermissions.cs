namespace Backend.Models.Permissions;

/// <summary>
/// This class can be used in the same way as the Permissions class.
/// It just serves to keep permissions for basic backend functions and more customized functions seperated.
/// </summary>
public class CustomPermissions
{
    public static class Products
    {
        public const string Create = "CustomPermissions.Products.Create";
        public const string Delete = "CustomPermissions.Products.Delete";
        public const string Edit = "CustomPermissions.Products.Edit";
        public const string View = "CustomPermissions.Products.View";
    }
}