namespace CustomDatabase.Controllers


open System.Collections.Generic
open System.ComponentModel.DataAnnotations
open CustomDatabase
open CustomDatabase.Value
open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.Logging




type Row = Dictionary<string, Value>

[<ApiController>]
[<Route("[controller]")>]
type QueryController(logger: ILogger<QueryController>) =
    inherit ControllerBase()

    [<HttpGet>]
    member _.Get([<Required>] query: string) =
        [ dict [ ("Valv", Int 32); ("Pok", String "ds"); ("Int", List [ Int 32; Int 64 ]) ] ]

    [<HttpPost>]
    member _.Add([<Required>] query: string) =
        QueryParser.parseAsCreationQuery (query)
        |> Result.map (fun context -> context.GetText()) // $"{QueryParser.parseAsCreationQuery(query)}-{QueryParser.parseAsCreationQuery(query)}"

    [<HttpPut>]
    member _.Update([<Required>] body: Value) =


        body

    [<HttpDelete>]
    member _.Delete([<Required>] query: string) = ""
