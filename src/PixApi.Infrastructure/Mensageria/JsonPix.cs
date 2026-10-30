using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PixApi.Infrastructure.Mensageria;

// Opções de JSON compartilhadas pela API e pelas mensagens: camelCase e datas no formato do Instant do Java.
public static class JsonPix
{
    public static readonly JsonSerializerOptions Opcoes = Configurar(new JsonSerializerOptions(JsonSerializerDefaults.Web));

    public static JsonSerializerOptions Configurar(JsonSerializerOptions opcoes)
    {
        opcoes.Converters.Add(new InstanteUtcJsonConverter());
        return opcoes;
    }
}

// O System.Text.Json escreveria "2026-10-25T12:00:00+00:00". O contrato do pix-api (Instant do Java)
// é "2026-10-25T12:00:00Z": sempre UTC, com "Z" e sem zeros à direita na fração.
public sealed class InstanteUtcJsonConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        DateTimeOffset.Parse(reader.GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToUniversalTime();

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'", CultureInfo.InvariantCulture));
}
