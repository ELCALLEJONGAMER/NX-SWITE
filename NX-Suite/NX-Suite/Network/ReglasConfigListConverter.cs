using NX_Suite.Models;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NX_Suite.Network
{
    /// <summary>
    /// Permite que el campo ReglasConfig del JSON acepte tanto un objeto único (formato
    /// antiguo) como un array de objetos (formato nuevo con múltiples archivos).
    /// Así los JSONs publicados antes del cambio siguen siendo compatibles.
    /// </summary>
    internal sealed class ReglasConfigListConverter : JsonConverter<List<ReglasConfig>>
    {
        public override List<ReglasConfig> Read(
            ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.StartArray)
            {
                // Formato nuevo: [ { ... }, { ... } ]
                var lista = new List<ReglasConfig>();
                reader.Read();
                while (reader.TokenType != JsonTokenType.EndArray)
                {
                    var item = JsonSerializer.Deserialize<ReglasConfig>(ref reader, options);
                    if (item != null) lista.Add(item);
                    reader.Read();
                }
                return lista;
            }

            if (reader.TokenType == JsonTokenType.StartObject)
            {
                // Formato antiguo: { ... }  ? envolvemos en lista
                var item = JsonSerializer.Deserialize<ReglasConfig>(ref reader, options);
                return item != null ? new List<ReglasConfig> { item } : new List<ReglasConfig>();
            }

            // null u otro token inesperado ? lista vacía
            reader.Skip();
            return new List<ReglasConfig>();
        }

        public override void Write(
            Utf8JsonWriter writer, List<ReglasConfig> value, JsonSerializerOptions options)
            => JsonSerializer.Serialize(writer, value, options);
    }
}
