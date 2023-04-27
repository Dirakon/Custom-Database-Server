namespace CustomDatabase.Controllers


open System.Collections.Generic
open System.ComponentModel.DataAnnotations
open CustomDatabase
open CustomDatabase.Value
open GeneratedLanguage
open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.Logging
open CustomDatabase.Antlr



type Row = Dictionary<string, Value>

[<ApiController>]
[<Route("[controller]")>]
type QueryController(logger: ILogger<QueryController>) =
    inherit ControllerBase()

    // TODO: place in different file/class all actual executions
    member _.ExecuteCreationRequest(context: QueryLanguageParser.EntityCreationContext) = Result.Ok "CREATED" //TODO
    member _.ExecuteAdditionRequest(context: QueryLanguageParser.EntityAdditionContext) = Result.Ok "ADDED" //TODO


    [<HttpGet>] //TODO: do literal RETRIEVE query
    member this.Retrieve([<Required>] query: string) =
        query
        |> QueryParser.parseAsRetrievalQuery
        |> Result.map (fun query -> query.getTextSeparatedBySpace ()) // TODO


    [<HttpPost>]
    member this.CreateOrAdd([<Required>] query: string) =
        let result =
            match (QueryParser.parseAsCreationQuery query, QueryParser.parseAsAdditionQuery query) with
            | Result.Error creationError, Result.Error additionError ->
                let lowerTrimmedQuery = query.Trim().ToLower()

                if lowerTrimmedQuery.StartsWith("create") then
                    Error creationError
                else if lowerTrimmedQuery.StartsWith("add") then
                    Error additionError
                else
                    Error "Unknown query. This HTTP request supports only CREATE and ADD queries."
            | Result.Ok creationContext, _ -> this.ExecuteCreationRequest(creationContext)
            | _, Result.Ok additionContext -> this.ExecuteAdditionRequest(additionContext)

        result


    [<HttpPut>]
    member this.Replace([<Required>] query: string) =
        query
        |> QueryParser.parseAsReplacementQuery
        |> Result.map (fun query -> query.getTextSeparatedBySpace ()) // TODO

    [<HttpDelete>]
    member this.Remove([<Required>] query: string) =
        query
        |> QueryParser.parseAsRemovalQuery
        |> Result.map (fun query -> query.getTextSeparatedBySpace ()) // TODO
