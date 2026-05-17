// WazzufJobs.BLL/Contracts/Applications/GroqResponse.cs
namespace WazzufJobs.BLL.Contracts.Applications;

public class GroqResponse
{
    public List<GroqChoice>? choices { get; set; }
}

public class GroqChoice
{
    public GroqMessage? message { get; set; }
}

public class GroqMessage
{
    public string? content { get; set; }
}