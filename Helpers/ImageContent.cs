using CrideLLMApi.DTO;

namespace CrideLLMApi.Helpers;

public static class ImageContent
{
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

    public static ImageContentPart FromBytes(
        byte[] data,
        string mimeType)
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