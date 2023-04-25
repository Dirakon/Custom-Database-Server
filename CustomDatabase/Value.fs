namespace CustomDatabase.Value


open System.Text.Json
open System.Text.Json.Serialization

//[<JsonConverter(typeof<ValueResolver>)>]
type Value =
    | Int of int
    | String of string
    | Bool of bool
    | List of Value list

and ValueResolver() =
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
        | _ -> failwith ("Invalid value type")


// Newtonsoft version:
// [<JsonConverter(typeof<ValueResolver>)>]
// type Value =
//         | Int of int
//         | String of string
//         | Bool of bool
//         | List of Value list
// and ValueResolver() =
//     inherit JsonConverter()
//
//     override _.CanConvert(t: System.Type) = t = typedefof<Value>
//
//     override _.WriteJson(writer: JsonWriter, value: obj, serializer: JsonSerializer) =
//
//         let token =
//             match value with
//             | :? Value as value ->
//                 match value with
//                 | Int x -> JToken.FromObject(x)
//                 | String x -> JToken.FromObject(x)
//                 | Bool x -> JToken.FromObject(x)
//                 | List x -> JToken.FromObject(x, serializer)
//             | _ -> failwith $"ValueResolver resolver can only resolve Value, but was provided with {value}"
//
//         token.WriteTo(writer)
//
//     override _.ReadJson(reader: JsonReader, targetType: System.Type, existingValue: obj, serializer: JsonSerializer) =
//         let token = JToken.Load(reader)
//         match token.Type with
//         | JTokenType.Integer -> Int(token.Value<int>())
//         | JTokenType.String -> String(token.Value<string>())
//         | JTokenType.Boolean -> Bool(token.Value<bool>())
//         | JTokenType.Array -> List(token.ToObject<Value list>(serializer))
//         | _ -> failwith "Invalid value type"
