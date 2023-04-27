namespace CustomDatabase

open System.Text.Json
open System.Text.Json.Serialization
open CustomDatabase.Value

type ValueResolver() =
    inherit JsonConverter<Value>()

    override _.CanConvert(t: System.Type) =
        // Comparing by namespace because both typeof and typedef of don't work consistently on union types:
        // Sometimes it shows as Value+List (or similar) instead of Value
        t.Namespace = typeof<Value>.Namespace


    override _.Write(writer: Utf8JsonWriter, value: Value, options: JsonSerializerOptions) =
        match value with
        | Int x -> writer.WriteNumberValue(x)
        | String x -> writer.WriteStringValue(x)
        | Bool x -> writer.WriteBooleanValue(x)
        | List x ->
            writer.WriteStartArray()
            x |> List.iter (fun v -> JsonSerializer.Serialize(writer, v, options))
            writer.WriteEndArray()

    override _.Read(reader: byref<Utf8JsonReader>, typeToConvert: System.Type, options: JsonSerializerOptions) =
        let token = JsonDocument.ParseValue(&reader).RootElement

        match token.ValueKind with
        | JsonValueKind.Number -> Int(token.GetInt32())
        | JsonValueKind.String -> String(token.GetString())
        | JsonValueKind.True -> Bool(true)
        | JsonValueKind.False -> Bool(false)
        | JsonValueKind.Array ->
            let values =
                token.EnumerateArray()
                |> Seq.map (fun v -> JsonSerializer.Deserialize<Value>(v.GetRawText(), options))

            List(Seq.toList values)
        | _ -> failwith "Invalid value type"

module JsonConverter =
    let addConvertersTo (options: JsonSerializerOptions) =
        JsonFSharpOptions.FSharpLuLike().AddToJsonSerializerOptions(options)

        options.Converters.Insert(0, ValueResolver())

    let createSerializerOptions =
        let options = JsonSerializerOptions()
        addConvertersTo (options)
        options
