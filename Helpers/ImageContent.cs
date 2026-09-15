using CrideLLMApi.DTO;

namespace CrideLLMApi.Helpers;

/// <summary>
/// Creates OpenAI-compatible image content parts from local files, byte arrays or URLs.
/// </summary>
public static class ImageContent
{
    private const string ComponentName = "ImageContent";

    private const string SupportedFormats = ".png, .jpg, .jpeg, .webp and .gif";

    /// <summary>
    /// Creates an ImageContentPart from a local image file.
    /// </summary>
    /// <param name="filePath">The path to the image file.</param>
    /// <returns>The created ImageContentPart.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="filePath"/> is null, empty or whitespace.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the image file does not exist.</exception>
    /// <exception cref="NotSupportedException">
    /// Thrown when the file extension is not a supported image format
    /// (<see cref="SupportedFormats"/>).
    /// </exception>
    /// <exception cref="CrideApiException">
    /// Thrown when the image file exists but cannot be read, for example because it is locked by
    /// another process or the process has no read permission.
    /// </exception>
    public static ImageContentPart FromFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(
                $"Image file was not found: {filePath}",
                filePath);
        }

        var mimeType = GetMimeType(filePath);

        byte[] bytes;

        try
        {
            bytes = File.ReadAllBytes(filePath);
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            System.Security.SecurityException)
        {
            throw new CrideApiException(
                message:
                    $"The image file '{filePath}' could not be read: {exception.Message}",
                component: ComponentName,
                operation: "Load an image from a local file",
                innerException: exception,
                hint:
                    "Check that the file is not locked by another process and that the current process has read permission for it.");
        }

        return FromBytes(bytes, mimeType);
    }

    /// <summary>
    /// Creates an ImageContentPart from a byte array and a MIME type.
    /// </summary>
    /// <param name="data">The byte array containing the image data.</param>
    /// <param name="mimeType">The MIME type of the image.</param>
    /// <returns>The created ImageContentPart.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="mimeType"/> is null, empty or whitespace.</exception>
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
    /// <param name="url">The absolute http, https or data URL of the image.</param>
    /// <returns>The created ImageContentPart.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="url"/> is null, empty or whitespace.</exception>
    /// <exception cref="CrideApiException">
    /// Thrown when the value is not an absolute http, https or data URL, because such a
    /// reference cannot be resolved by the provider.
    /// </exception>
    public static ImageContentPart FromUrl(string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp &&
             uri.Scheme != Uri.UriSchemeHttps &&
             !string.Equals(uri.Scheme, "data", StringComparison.OrdinalIgnoreCase)))
        {
            throw new CrideApiException(
                message:
                    $"The value '{url}' is not a usable image URL.",
                component: ComponentName,
                operation: "Create an image content part from a URL",
                hint:
                    "Pass an absolute http, https or data URI, or use ImageContent.FromFile for a local image file.");
        }

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
    /// <exception cref="NotSupportedException">Thrown when the image format is not supported.</exception>
    private static string GetMimeType(string filePath)
    {
        return Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".gif" => "image/gif",

            _ => throw new NotSupportedException(
                $"Unsupported image format '{Path.GetExtension(filePath)}' for file '{filePath}'. " +
                $"Supported formats: {SupportedFormats}.")
        };
    }
}
