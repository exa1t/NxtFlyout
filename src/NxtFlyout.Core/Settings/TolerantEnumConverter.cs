using System.Text.Json;
using System.Text.Json.Serialization;

namespace NxtFlyout.Core.Settings;

/// <summary>
/// Writes enums as their names and reads names or numbers. An unknown value does not fail
/// the whole file: it becomes an undefined enum value, which <see cref="AppSettings.Normalize"/>
/// then replaces with that setting's default. This keeps a settings file written by a newer
/// build (or edited by hand) loadable.
/// </summary>
internal sealed class TolerantEnumConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum;

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var converterType = typeof(TolerantEnumConverter<>).MakeGenericType(typeToConvert);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }
}

internal sealed class TolerantEnumConverter<T> : JsonConverter<T>
    where T : struct, Enum
{
    /// <summary>A value no enum in this app defines; Normalize() treats it as "use the default".</summary>
    private static readonly T Invalid = (T)Enum.ToObject(typeof(T), -1);

    public override bool HandleNull => true;

    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                var text = reader.GetString();
                if (Enum.TryParse<T>(text, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
                {
                    return parsed;
                }

                return Invalid;

            case JsonTokenType.Number:
                return reader.TryGetInt32(out var number) ? (T)Enum.ToObject(typeof(T), number) : Invalid;

            default:
                // null, true/false, or an object/array: consume it and fall back.
                reader.TrySkip();
                return Invalid;
        }
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
