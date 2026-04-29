namespace WazzufJobs.DAL.Persistence.Seeders;

public static class AppRoles
{
    public const string AdminRoleName = "Admin";
    public const string UserRoleName = "User";

    public static class Admin
    {
        public const string RoleId = "C1E2A3D4-E5F6-7890-ABCD-EF1234567890";
        public const string Name = "Admin";
        public const string RoleConcurrencyStamp = "A1B2C3D4-E5F6-7890-ABCD-EF1234567891";
    }

    public static class User
    {
        public const string RoleId = "D2E3B4C5-F6A7-8901-BCDE-F01234567892";
        public const string Name = "User";
        public const string RoleConcurrencyStamp = "B2C3D4E5-F6A7-8901-BCDE-F01234567893";
    }
}