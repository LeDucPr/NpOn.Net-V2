using System.Text.Json;

namespace Common.Applications.NpOn.CommonApplication.Extensions;

public class GuidEmptyAsNullConverter : System.Text.Json.Serialization.JsonConverter<Guid?>
{
    public override void Write(Utf8JsonWriter writer, Guid? value, JsonSerializerOptions options)
    {
        if (value == null || value == Guid.Empty)
        {
            // null → JSON engine will be ignored property WhenWritingNull
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value.Value);
    }

    public override Guid? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.GetGuid();
    }
}