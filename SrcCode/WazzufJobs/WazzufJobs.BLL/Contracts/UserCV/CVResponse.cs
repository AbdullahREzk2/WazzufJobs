namespace WazzufJobs.BLL.Contracts.UserCV;

// The storage URL is intentionally not exposed: CV files are served only through
// authorized API endpoints (GET api/cv/file, GET api/applications/{id}/cv).
public record CVResponse(
    int Id,
    string FileName,
    DateTime UploadedAt
);

public record CvFileResponse(
    byte[] Content,
    string FileName,
    string ContentType
);
