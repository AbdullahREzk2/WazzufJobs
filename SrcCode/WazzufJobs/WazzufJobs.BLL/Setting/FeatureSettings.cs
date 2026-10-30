namespace WazzufJobs.BLL.Setting;

public class FeatureSettings
{
    public const string SectionName = "Features";

    /// <summary>
    /// Phase C0: CV uploads stay disabled until authenticated Cloudinary storage is verified.
    /// When enabled, uploads are always stored as authenticated assets (never public).
    /// </summary>
    public bool CvUploadsEnabled { get; set; }
}
