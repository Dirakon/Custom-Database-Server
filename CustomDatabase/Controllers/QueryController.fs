namespace CustomDatabase.Controllers


open System.Collections.Generic
open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.Logging
open Newtonsoft.Json
open Newtonsoft.Json.Linq


[<JsonConverter(typeof<ValueResolver>)>]
type Value =
        | Int of int
        | String of string
        | Bool of bool
        | List of Value list
and ValueResolver() =
    inherit JsonConverter()

    override _.CanConvert(t: System.Type) = t = typedefof<Value>

    override _.WriteJson(writer: JsonWriter, value: obj, serializer: JsonSerializer) =

        let token =
            match value with
            | :? Value as value ->
                match value with
                | Int x -> JToken.FromObject(x)
                | String x -> JToken.FromObject(x)
                | Bool x -> JToken.FromObject(x)
                | List x -> JToken.FromObject(x, serializer)
            | _ -> failwith $"ValueResolver resolver can only resolve Value, but was provided with {value}"

        token.WriteTo(writer)

    override _.ReadJson(reader: JsonReader, targetType: System.Type, existingValue: obj, serializer: JsonSerializer) =
        let token = JToken.Load(reader)
        match token.Type with
        | JTokenType.Integer -> Int(token.Value<int>())
        | JTokenType.String -> String(token.Value<string>())
        | JTokenType.Boolean -> Bool(token.Value<bool>())
        | JTokenType.Array -> List(token.ToObject<Value list>(serializer))
        | _ -> failwith "Invalid value type"
      
   



type Row = Dictionary<string, Value>

[<ApiController>]
[<Route("[controller]")>]
type QueryController(logger: ILogger<QueryController>) =
    inherit ControllerBase()

    [<HttpGet>]
    member _.Get(query: string) =
        [ dict [ ("Valv", Int 32); ("Pok", String "ds"); ("Int", List [ Int 32; Int 64 ]) ] ]

    [<HttpPost>]
    member _.Add(query: string) = ""

    [<HttpPut>]
    member _.Update(body:Value) = body

    [<HttpDelete>]
    member _.Delete(query: string) = ""
