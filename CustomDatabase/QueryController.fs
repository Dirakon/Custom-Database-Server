namespace CustomDatabase.Controllers


open System.Collections.Generic
open System.ComponentModel.DataAnnotations
open CustomDatabase
open CustomDatabase.Value
open CustomDatabase.Expressions
open GeneratedLanguage
open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.Logging
open CustomDatabase.Antlr
open FsToolkit.ErrorHandling
open FsToolkit.ErrorHandling.Operator.Result



type Row = Dictionary<string, Value>

[<ApiController>]
[<Route("[controller]")>]
type QueryController(logger: ILogger<QueryController>, dataStorage: IDataStorage) =
    inherit ControllerBase()


    let executeCreationRequest (context: QueryLanguageParser.EntityCreationContext) =
        result {
            let entityNames =
                dataStorage.getEntityDefinitions () |> List.map (fun entity -> entity.name)

            let! describedEntity = QueryParser.parseEntity (context, entityNames)
            return! dataStorage.createEntity describedEntity
        }
        |> Result.map (fun _ -> "Successful!")
        |> Result.map JsonConverter.serialize

    let executeAdditionRequest (context: QueryLanguageParser.EntityAdditionContext) =
        result {
            if notNull (context.entitySingleAddition ()) then
                let additionRequest = context.entitySingleAddition ()
                let entityName = additionRequest.entityName().GetText().ToLower()

                return!
                    dataStorage.addEntities (
                        entityName,
                        [ JsonConverter.parseSingleRow (additionRequest.jsonObj().getTextSeparatedBySpace ()) ]
                    )
            else if notNull (context.entityGroupAddition ()) then
                let additionRequest = context.entityGroupAddition ()
                let entityName = additionRequest.entityName().GetText().ToLower()

                return!
                    dataStorage.addEntities (
                        entityName,
                        JsonConverter.parseMultipleRows (additionRequest.jsonArr().getTextSeparatedBySpace ())
                    )
            else
                return! Result.Error $"Cannot analyze query type of '{context.getTextSeparatedBySpace ()}'"
        }
        |> Result.map JsonConverter.serialize


    let executeReplacementRequest (replacementQuery: QueryLanguageParser.EntityReplacementContext) =
        result {

            if notNull (replacementQuery.entitySingleReplacement ()) then
                let pointer = replacementQuery.entitySingleReplacement().rawPointer().GetText()

                return!
                    dataStorage.replaceEntities (
                        [ pointer ],
                        [ JsonConverter.parseSingleRow (
                              replacementQuery.entitySingleReplacement().jsonObj().getTextSeparatedBySpace ()
                          ) ]
                    )
            else if notNull (replacementQuery.entityGroupReplacement ()) then
                let! pointers =
                    QueryParser.parsePointersRecursively (
                        replacementQuery.entityGroupReplacement().multipleRawPointers ()
                    )

                return!
                    dataStorage.replaceEntities (
                        pointers,
                        JsonConverter.parseMultipleRows (
                            replacementQuery.entityGroupReplacement().jsonArr().getTextSeparatedBySpace ()
                        )
                    )
            else
                return! Result.Error $"Cannot analyze query type of '{replacementQuery.getTextSeparatedBySpace ()}'"
        }
        |> Result.map (fun _ -> "Successful!")
        |> Result.map JsonConverter.serialize

    let executeRetrievalRequest (retrievalQuery: QueryLanguageParser.EntityRetrievalContext) =
        result {

            if notNull (retrievalQuery.entitySingleRetrieval ()) then
                let pointer = retrievalQuery.entitySingleRetrieval().rawPointer().GetText()
                return! dataStorage.retrieveEntities ([ pointer ])
            else if notNull (retrievalQuery.entityGroupRetrieval ()) then
                let! pointers =
                    QueryParser.parsePointersRecursively (retrievalQuery.entityGroupRetrieval().multipleRawPointers ())

                return! dataStorage.retrieveEntities (pointers)
            else
                return! Result.Error $"Cannot analyze query type of '{retrievalQuery.getTextSeparatedBySpace ()}'"
        }
        |> Result.map JsonConverter.serialize

    let executeSelectionRequest (getQuery: QueryLanguageParser.EntitySelectionContext) =
        result {

            let filteringFunction =
                if notNull (getQuery.booleanExpression ()) then
                    Some(getQuery.booleanExpression ())
                else
                    None

            if notNull (getQuery.entitySingleSelection ()) then
                let! allSelectedEntities =
                    dataStorage.selectEntities (
                        getQuery.entitySingleSelection().entityName().GetText().ToLower(),
                        filteringFunction
                    )

                return!
                    match allSelectedEntities with
                    | [] -> Result.Error "No entities found!"
                    | firstEntity :: _ -> Result.Ok [ firstEntity ]
            else if notNull (getQuery.entityGroupSelection ()) then
                return!
                    dataStorage.selectEntities (
                        getQuery.entityGroupSelection().entityName().GetText().ToLower(),
                        filteringFunction
                    )
            else
                return! Result.Error $"Cannot analyze query type of '{getQuery.getTextSeparatedBySpace ()}'"
        }
        |> Result.map JsonConverter.serialize


    [<HttpGet>]
    member this.ExecuteQuery([<Required>] query: string) =
        result {
            let! parsedQuery = QueryParser.parseAsSomeQuery (query)

            return!
                if notNull (parsedQuery.entityAddition ()) then
                    executeAdditionRequest (parsedQuery.entityAddition ())
                elif notNull (parsedQuery.entityCreation ()) then
                    executeCreationRequest (parsedQuery.entityCreation ())
                elif notNull (parsedQuery.entitySelection ()) then
                    executeSelectionRequest (parsedQuery.entitySelection ())
                elif notNull (parsedQuery.entityReplacement ()) then
                    executeReplacementRequest (parsedQuery.entityReplacement ())
                elif notNull (parsedQuery.entityRetrieval ()) then
                    executeRetrievalRequest (parsedQuery.entityRetrieval ())
                else
                    Result.Error("Cannot identify the query type!")

        }
