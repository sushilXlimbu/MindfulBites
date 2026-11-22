namespace FoodJournalApp.Storage;

// Saves photos to wwwroot/uploads and returns a relative URL served by UseStaticFiles.
// Fine for offline dev. Do NOT use on Railway/Render: their disks are wiped on redeploy.
public class LocalPhotoStorage : IPhotoStorage
{
    private readonly IWebHostEnvironment _env;

    public LocalPhotoStorage(IWebHostEnvironment env) => _env = env;

    public async Task<string> SaveAsync(Stream content, string contentType, string extension, CancellationToken ct = default)
    {
        var fileName = $"{Guid.NewGuid()}{extension}";
        var uploadsDir = Path.Combine(_env.WebRootPath, "uploads");
        Directory.CreateDirectory(uploadsDir);

        await using (var fileStream = new FileStream(Path.Combine(uploadsDir, fileName), FileMode.Create))
        {
            await content.CopyToAsync(fileStream, ct);
        }

        return $"/uploads/{fileName}"; // relative: frontend prepends the API origin
    }
}
