using Amazon.S3;
using Amazon.S3.Model;

namespace FoodJournalApp.Storage;

// Bound from the "R2" config section. Secrets come from user-secrets locally and
// env vars (R2__AccessKeyId etc.) on the host, never from appsettings.json.
public class R2Options
{
    public const string SectionName = "R2";

    public string ServiceUrl { get; set; } = "";       // https://<accountid>.r2.cloudflarestorage.com
    public string AccessKeyId { get; set; } = "";
    public string SecretAccessKey { get; set; } = "";
    public string BucketName { get; set; } = "";
    public string PublicBaseUrl { get; set; } = "";    // https://pub-xxxx.r2.dev or https://photos.yourdomain.com
    public string KeyPrefix { get; set; } = "meals/";
}

// Uploads photos to a Cloudflare R2 bucket via the S3 API and returns a public URL.
// Assumes the bucket has public read enabled (r2.dev URL or custom domain), so the
// returned URL can go straight into an <img> tag with no signing.
public class R2PhotoStorage : IPhotoStorage
{
    private readonly IAmazonS3 _s3;
    private readonly R2Options _options;

    public R2PhotoStorage(IAmazonS3 s3, R2Options options)
    {
        _s3 = s3;
        _options = options;
    }

    // Builds an S3 client configured for R2's quirks. Registered as a singleton:
    // the client is thread-safe and holds a connection pool, so reuse one.
    public static IAmazonS3 CreateClient(R2Options options)
    {
        var config = new AmazonS3Config
        {
            ServiceURL = options.ServiceUrl,
            // R2 ignores regions, but SigV4 signing needs one. "auto" is what Cloudflare documents.
            AuthenticationRegion = "auto",
            // Path-style: https://<account>.r2.cloudflarestorage.com/<bucket>/<key>
            // instead of virtual-hosted <bucket>.<account>.r2..., which R2's endpoint does not resolve.
            ForcePathStyle = true,
            // AWSSDK v4 computes CRC checksums on every request by default. R2 does not
            // support all of those, so only send/validate them when an operation requires it.
            RequestChecksumCalculation = Amazon.Runtime.RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = Amazon.Runtime.ResponseChecksumValidation.WHEN_REQUIRED
        };

        return new AmazonS3Client(options.AccessKeyId, options.SecretAccessKey, config);
    }

    public async Task<string> SaveAsync(Stream content, string contentType, string extension, CancellationToken ct = default)
    {
        var prefix = _options.KeyPrefix.Trim('/');
        var key = string.IsNullOrEmpty(prefix)
            ? $"{Guid.NewGuid()}{extension}"
            : $"{prefix}/{Guid.NewGuid()}{extension}";

        var request = new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = key,
            InputStream = content,
            ContentType = contentType, // so browsers render it instead of downloading it
            // R2 does not support streaming (chunked) SigV4 payload signing. Cloudflare's
            // .NET guidance is to disable payload signing; HTTPS still protects the body.
            DisablePayloadSigning = true,
            // Keep the caller's stream open; the controller owns its lifetime.
            AutoCloseStream = false,
            // Photo names are random Guids and never overwritten, so cache hard at the edge.
            Headers = { CacheControl = "public, max-age=31536000, immutable" }
        };

        await _s3.PutObjectAsync(request, ct);

        return $"{_options.PublicBaseUrl.TrimEnd('/')}/{key}";
    }
}
