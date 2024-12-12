using System.Text.Json.Serialization;

namespace MessagingRedisCache;

[JsonSerializable(typeof(CacheMessage))]
[JsonSerializable(typeof(Guid))]
[JsonSerializable(typeof(string))]
internal partial class SourceGenerationContext :
    JsonSerializerContext
{
}