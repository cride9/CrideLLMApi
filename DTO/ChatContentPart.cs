using System.Text.Json.Serialization;

namespace CrideLLMApi.DTO;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TextContentPart), "text")]
[JsonDerivedType(typeof(ImageContentPart), "image_url")]
public abstract record ChatContentPart;

public sealed record TextContentPart : ChatContentPart
{
    [JsonPropertyName("text")]
    public required string Text { get; init; }
}

public sealed record ImageContentPart : ChatContentPart
{
    [JsonPropertyName("image_url")]
    public required ImageUrlContent ImageUrl { get; init; }
}

public sealed record ImageUrlContent
{
    [JsonPropertyName("url")]
    public required string Url { get; init; }
}