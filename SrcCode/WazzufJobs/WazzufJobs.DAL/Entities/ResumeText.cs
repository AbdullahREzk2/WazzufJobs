namespace WazzufJobs.DAL.Entities;

/// <summary>Text extracted from a resume, kept off the resume row (Phase 02, §1).</summary>
public class ResumeText
{
    public int ResumeId { get; set; }
    public string ExtractedText { get; set; } = string.Empty;

    // Navigation
    public Resume Resume { get; set; } = null!;
}
