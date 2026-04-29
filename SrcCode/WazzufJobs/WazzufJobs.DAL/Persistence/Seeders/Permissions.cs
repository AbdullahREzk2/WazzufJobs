namespace WazzufJobs.DAL.Persistence.Seeders;

public static class Permissions
{
    public const string Type = "permissions";

    // Jobs
    public const string JobsRead = "jobs:read";
    public const string JobsCreate = "jobs:create";
    public const string JobsUpdate = "jobs:update";
    public const string JobsDelete = "jobs:delete";

    // Applications
    public const string ApplicationsRead = "applications:read";
    public const string ApplicationsCreate = "applications:create";
    public const string ApplicationsUpdate = "applications:update";

    // Categories
    public const string CategoriesRead = "categories:read";
    public const string CategoriesCreate = "categories:create";
    public const string CategoriesUpdate = "categories:update";
    public const string CategoriesDelete = "categories:delete";

    // Users
    public const string UsersRead = "users:read";
    public const string UsersUpdate = "users:update";
    public const string UsersDelete = "users:delete";
    public const string UsersCreate = "users:create";

    // Roles
    public const string RolesRead = "roles:read";
    public const string RolesCreate = "roles:create";
    public const string RolesUpdate = "roles:update";
    public const string RolesDelete = "roles:delete";

    // CV
    public const string CVUpload = "cv:upload";
    public const string CVDelete = "cv:delete";

    // SavedJobs
    public const string SavedJobsRead = "savedjobs:read";
    public const string SavedJobsCreate = "savedjobs:create";
    public const string SavedJobsDelete = "savedjobs:delete";

    public static List<string> GetAllPermissions() =>
    [
        JobsRead, JobsCreate, JobsUpdate, JobsDelete,
        ApplicationsRead, ApplicationsCreate, ApplicationsUpdate,
        CategoriesRead, CategoriesCreate, CategoriesUpdate, CategoriesDelete,
        UsersRead, UsersCreate, UsersUpdate, UsersDelete,
        RolesRead, RolesCreate, RolesUpdate, RolesDelete,
        CVUpload, CVDelete,
        SavedJobsRead, SavedJobsCreate, SavedJobsDelete
    ];

    public static List<string> GetUserPermissions() =>
    [
        JobsRead,
        ApplicationsRead, ApplicationsCreate,
        CategoriesRead,
        CVUpload, CVDelete,
        SavedJobsRead, SavedJobsCreate, SavedJobsDelete
    ];
}