using Amazon.S3;
using FoodJournalApp.Security;

namespace FoodJournalApp.Storage;

public static class PhotoStorageRegistration
{
    // Picks the IPhotoStorage implementation from PhotoStorage:Provider ("R2" or "Local").
    // Defaults to R2. R2 settings are validated at startup so a misconfigured deploy
    // fails immediately instead of on the first meal upload.
    public static IServiceCollection AddPhotoStorage(this IServiceCollection services, IConfiguration config)
    {
        var provider = config["PhotoStorage:Provider"];
        if (string.IsNullOrWhiteSpace(provider))
            provider = "R2";

        if (provider.Equals("Local", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IPhotoStorage, LocalPhotoStorage>();
            return services;
        }

        if (!provider.Equals("R2", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Unknown PhotoStorage:Provider '{provider}'. Use \"R2\" or \"Local\".");

        var options = new R2Options
        {
            ServiceUrl = SecretConfig.Require(config, "R2:ServiceUrl"),
            AccessKeyId = SecretConfig.Require(config, "R2:AccessKeyId"),
            SecretAccessKey = SecretConfig.Require(config, "R2:SecretAccessKey"),
            BucketName = SecretConfig.Require(config, "R2:BucketName"),
            PublicBaseUrl = SecretConfig.Require(config, "R2:PublicBaseUrl"),
            KeyPrefix = config["R2:KeyPrefix"] ?? "meals/"
        };

        services.AddSingleton(options);
        services.AddSingleton<IAmazonS3>(_ => R2PhotoStorage.CreateClient(options));
        services.AddSingleton<IPhotoStorage, R2PhotoStorage>();
        return services;
    }
}
