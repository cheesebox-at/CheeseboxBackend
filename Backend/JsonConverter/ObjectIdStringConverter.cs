using System.Text.Json;
using System.Text.Json.Serialization;
using MongoDB.Bson;

namespace Backend.JsonConverter;

public class ObjectIdStringConverter : JsonConverter<ObjectId>
{
    public override void Write(Utf8JsonWriter writer, ObjectId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }

    public override ObjectId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var objectIdAsString = reader.GetString();
        return ObjectId.TryParse(objectIdAsString, out var objectId)
            ? objectId
            : throw new JsonException($"Invalid ObjectId: {objectIdAsString}");
    }
}