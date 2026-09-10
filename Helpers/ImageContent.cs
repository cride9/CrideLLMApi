using CrideLLMApi.DTO;

namespace CrideLLMApi.Helpers;

public static class ImageContent
{
    /// <summary>
    /// Creates an ImageContentPart from a local image file.
    /// </summary>
    /// <param name="filePath">The path to the image file.</param>
    /// <returns>The created ImageContentPart.</returns>
    /// <exception cref="FileNotFoundException"></exception>
    public static ImageContentPart FromFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(
                $"Image file was not found: {filePath}",
                filePath);
        }

        var bytes = File.ReadAllBytes(filePath);
        var mimeType = GetMimeType(filePath);

        return FromBytes(bytes, mimeType);
    }

    /// <summary>
    /// Creates an ImageContentPart from a byte array and a MIME type.
    /// </summary>
    /// <param name="data">The byte array containing the image data.</param>
    /// <param name="mimeType">The MIME type of the image.</param>
    /// <returns>The created ImageContentPart.</returns>
    public static ImageContentPart FromBytes(byte[] data, string mimeType)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentException.ThrowIfNullOrWhiteSpace(mimeType);

        return new ImageContentPart
        {
            ImageUrl = new ImageUrlContent
            {
                Url =
                    $"data:{mimeType};base64," +
                    Convert.ToBase64String(data)
            }
        };
    }

    /// <summary>
    /// Creates an ImageContentPart from a URL.
    /// </summary>
    /// <param name="url">The URL of the image.</param>
    /// <returns>The created ImageContentPart.</returns>
    public static ImageContentPart FromUrl(string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        return new ImageContentPart
        {
            ImageUrl = new ImageUrlContent
            {
                Url = url
            }
        };
    }

    /// <summary>
    /// Gets the MIME type based on the file extension of the provided file path.
    /// </summary>
    /// <param name="filePath">The path to the image file.</param>
    /// <returns>The MIME type of the image.</returns>
    /// <exception cref="NotSupportedException"></exception>
    private static string GetMimeType(string filePath)
    {
        return Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".gif" => "image/gif",

            _ => throw new NotSupportedException(
                $"Unsupported image format: {Path.GetExtension(filePath)}")
        };
    }
}