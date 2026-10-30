using System.Collections.Concurrent;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.UI.Services;
using WazzufJobs.BLL.Services;
using WazzufJobs.BLL.Services.Storage;

namespace WazzufJobs.Tests.Infrastructure;

/// <summary>In-memory private document storage. Records every call so tests can assert storage behaviour.</summary>
public class FakeDocumentStorage : IDocumentStorage
{
    public static readonly byte[] SamplePdf = "%PDF-1.4\n% fake cv for tests\n%%EOF"u8.ToArray();

    public string ProviderName => "Fake";
    public ConcurrentQueue<string> PrivateUploads { get; } = new();
    public ConcurrentQueue<(string PublicId, string DeliveryType)> Downloads { get; } = new();
    public ConcurrentQueue<(string PublicId, string DeliveryType)> RawDeletes { get; } = new();

    public Task<StoredDocument?> StorePrivateAsync(Stream content, string folder, CancellationToken cancellationToken = default)
    {
        var key = $"{folder}/{Guid.NewGuid():N}";
        PrivateUploads.Enqueue(key);
        return Task.FromResult<StoredDocument?>(new StoredDocument(
            key, CvStorage.AuthenticatedType, $"https://res.cloudinary.com/test/raw/authenticated/s--sig--/v1/{key}"));
    }

    public Task<byte[]?> ReadAsync(string key, string accessType, CancellationToken cancellationToken = default)
    {
        Downloads.Enqueue((key, accessType));
        return Task.FromResult<byte[]?>(SamplePdf);
    }

    public Task<bool> DeleteAsync(string key, string accessType, CancellationToken cancellationToken = default)
    {
        RawDeletes.Enqueue((key, accessType));
        return Task.FromResult(true);
    }
}

/// <summary>Public image hosting stand-in.</summary>
public class FakeImageService : ICloudinaryService
{
    public Task<CloudinaryUploadResult> UploadImageAsync(IFormFile file, string folder, CancellationToken cancellationToken = default)
        => Task.FromResult(new CloudinaryUploadResult($"https://res.cloudinary.com/test/image/upload/{folder}/img.jpg", $"{folder}/img", true));

    public Task<bool> DeleteAsync(string publicId, CancellationToken cancellationToken = default) => Task.FromResult(true);
}

public class FakeEmailSender : IEmailSender
{
    public ConcurrentQueue<(string To, string Subject)> Sent { get; } = new();

    public Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        Sent.Enqueue((email, subject));
        return Task.CompletedTask;
    }
}

/// <summary>Captures enqueued Hangfire jobs without a Hangfire server or SQL storage.</summary>
public class FakeBackgroundJobClient : IBackgroundJobClient
{
    public ConcurrentQueue<Job> Jobs { get; } = new();

    public string Create(Job job, IState state)
    {
        Jobs.Enqueue(job);
        return Guid.NewGuid().ToString("N");
    }

    public bool ChangeState(string jobId, IState state, string expectedState) => true;
}
