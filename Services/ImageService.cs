using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;

namespace Kayane.Services;

public class ImageService : IImageService
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<ImageService> _logger;

    private const long MaxInputBytes = 5 * 1024 * 1024;   // 5 MB
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };

    // JPEG encoder — 82 is a good quality/size tradeoff for product photos
    private static readonly JpegEncoder Encoder = new()
    {
        Quality = 82
    };

    public ImageService(
        IWebHostEnvironment environment,
        ILogger<ImageService> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public async Task<ImageProcessResult> ProcessAsync(
        IFormFile file,
        string subFolder,
        int maxWidth,
        int maxHeight,
        int? thumbnailSize = null,
        bool cropToMax = false)
    {
        if (file == null || file.Length == 0)
            return new ImageProcessResult { Error = "No file provided." };

        if (file.Length > MaxInputBytes)
            return new ImageProcessResult { Error = "Image must be smaller than 5 MB." };

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            return new ImageProcessResult { Error = "Only .jpg, .jpeg, .png, and .webp images are allowed." };

        try
        {
            // Load into memory — input is bounded by the 5 MB check above.
            using var inputStream = file.OpenReadStream();
            using var image = await Image.LoadAsync(inputStream);

            // Auto-rotate based on EXIF orientation BEFORE we strip metadata.
            image.Mutate(x => x.AutoOrient());

            // Save the original dimensions for logging
            var originalWidth = image.Width;
            var originalHeight = image.Height;

            // Resize the main image
            if (cropToMax)
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(maxWidth, maxHeight),
                    Mode = ResizeMode.Crop,
                    Position = AnchorPositionMode.Center
                }));
            }
            else
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(maxWidth, maxHeight),
                    Mode = ResizeMode.Max   // keep aspect ratio, fit inside
                }));
            }

            // Prepare output folder
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", subFolder);
            Directory.CreateDirectory(uploadsFolder);

            // Save main image
            var mainFileName = $"{Guid.NewGuid()}.jpg";
            var mainFullPath = Path.Combine(uploadsFolder, mainFileName);

            await using (var output = new FileStream(mainFullPath, FileMode.Create))
            {
                await image.SaveAsync(output, Encoder);
            }

            var result = new ImageProcessResult
            {
                MainPath = $"/uploads/{subFolder}/{mainFileName}"
            };

            // Optional square thumbnail
            if (thumbnailSize.HasValue)
            {
                // Reopen a fresh Image from the saved main file — cleaner than
                // trying to clone mid-mutation.
                using var thumbSource = await Image.LoadAsync(mainFullPath);

                thumbSource.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(thumbnailSize.Value, thumbnailSize.Value),
                    Mode = ResizeMode.Crop,
                    Position = AnchorPositionMode.Center
                }));

                var thumbFileName = $"{Guid.NewGuid()}.jpg";
                var thumbFullPath = Path.Combine(uploadsFolder, thumbFileName);

                await using (var output = new FileStream(thumbFullPath, FileMode.Create))
                {
                    await thumbSource.SaveAsync(output, Encoder);
                }

                result.ThumbnailPath = $"/uploads/{subFolder}/{thumbFileName}";
            }

            _logger.LogInformation(
                "Processed image: {Original}×{OriginalH} → {NewW}×{NewH}, {InputKB}KB → {OutputKB}KB",
                originalWidth, originalHeight,
                image.Width, image.Height,
                file.Length / 1024,
                new FileInfo(mainFullPath).Length / 1024);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Image processing failed for {FileName}", file.FileName);
            return new ImageProcessResult { Error = "Failed to process the image. Please try a different file." };
        }
    }

    public void Delete(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return;
        if (!relativePath.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase)) return;

        var fullPath = Path.Combine(
            _environment.WebRootPath,
            relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

        if (System.IO.File.Exists(fullPath))
        {
            try { System.IO.File.Delete(fullPath); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete image at {Path}", fullPath);
            }
        }
    }
}