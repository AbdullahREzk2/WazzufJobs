namespace WazzufJobs.DAL.Entities;
public class CV
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;

    // Cloudinary
    public string Url { get; set; } = string.Empty;
    public string PublicId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string? ExtractedText { get; set; } 

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public AppUser User { get; set; } = null!;
}
