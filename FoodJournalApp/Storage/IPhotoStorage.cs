namespace FoodJournalApp.Storage;

// The photo storage seam. The controller only knows "save these bytes, give me a URL
// the client can put in an <img> tag". Where the bytes live (local disk, R2, S3...)
// is chosen once in Program.cs.
public interface IPhotoStorage
{
    // Stores the photo and returns the URL to save on Meal.PhotoUrl.
    // `extension` (e.g. ".jpg") is already derived from a validated content type.
    // The returned URL may be relative ("/uploads/x.jpg", local) or absolute
    // ("https://pub-xxx.r2.dev/meals/x.jpg", R2). Clients should use absolute URLs
    // as-is and prefix relative ones with the API origin.
    Task<string> SaveAsync(Stream content, string contentType, string extension, CancellationToken ct = default);
}
