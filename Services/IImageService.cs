namespace Kayane.Services;

public interface IImageService
{
    /// <summary>
    /// Processes an uploaded image: resizes to fit maxWidth × maxHeight,
    /// strips metadata, saves as JPEG (quality 82), and optionally generates
    /// a square thumbnail. Returns relative paths for both.
    /// </summary>
    Task<ImageProcessResult> ProcessAsync(
        IFormFile file,
        string subFolder,
        int maxWidth,
        int maxHeight,
        int? thumbnailSize = null,
        bool cropToMax = false);

    /// <summary>
    /// Deletes a previously stored image by its relative path.
    /// No-op if path is null/empty or file doesn't exist.
    /// </summary>
    void Delete(string? relativePath);
}

public class ImageProcessResult
{
    public string? MainPath { get; set; }
    public string? ThumbnailPath { get; set; }
    public string? Error { get; set; }

    public bool Success => Error == null;
}