namespace WazzufJobs.BLL.Services.Storage;

public class StorageSettings
{
    public const string SectionName = "Storage";

    /// <summary>Private document provider. Supported: "Cloudinary".</summary>
    public string DocumentProvider { get; set; } = "Cloudinary";
}
