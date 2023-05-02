namespace CustomDatabase.Controllers


open System.Collections.Generic
open System.ComponentModel.DataAnnotations
open CustomDatabase
open CustomDatabase.MiscExtensions
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
            let entityName = context.entityName().getValidName ()

            return!
                dataStorage.addEntities (
                    entityName,
                    JsonConverter.parseMultipleRows (context.jsonArr().getTextSeparatedBySpace ())
                )
        }
        |> Result.map JsonConverter.serialize


    let executeReplacementRequest (replacementQuery: QueryLanguageParser.EntityReplacementContext) =
        result {

            let! pointers = QueryParser.parsePointersRecursively (replacementQuery.multipleRawPointers ())

            return!
                dataStorage.replaceEntities (
                    pointers,
                    JsonConverter.parseMultipleRows (replacementQuery.jsonArr().getTextSeparatedBySpace ())
                )
        }
        |> Result.map (fun _ -> "Successful!")
        |> Result.map JsonConverter.serialize

    let executeRetrievalRequest (retrievalQuery: QueryLanguageParser.EntityRetrievalContext) =
        result {

            let! pointers = QueryParser.parsePointersRecursively (retrievalQuery.multipleRawPointers ())

            return! dataStorage.retrieveEntities pointers
        }
        |> Result.map JsonConverter.serialize

    let executeSelectionRequest (getQuery: QueryLanguageParser.EntitySelectionContext) =
        result {

            let filteringFunction = Option.fromNullable (getQuery.booleanExpression ())

            return! dataStorage.selectEntities (getQuery.entityName().getValidName (), filteringFunction)
        }
        |> Result.map JsonConverter.serialize

    let executeDroppingRequest (context: QueryLanguageParser.EntityDroppingContext) =
        result {
            let entityName = context.entityName().getValidName ()

            return! dataStorage.dropEntity (entityName)
        }
        |> Result.map (fun _ -> "Successful!")
        |> Result.map JsonConverter.serialize

    let executeRemovalRequest (context: QueryLanguageParser.EntityRemovalContext) =
        result {
            let! pointers = QueryParser.parsePointersRecursively (context.multipleRawPointers ())

            return! dataStorage.removeEntities (pointers)
        }
        |> Result.map (fun _ -> "Successful!")
        |> Result.map JsonConverter.serialize


    [<HttpGet>]
    member this.ExecuteQuery([<Required>] query: string) =
        result {
            let! parsedQuery = QueryParser.parseAsSomeQuery query

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
                elif notNull (parsedQuery.entityRemoval ()) then
                    executeRemovalRequest (parsedQuery.entityRemoval ())
                elif notNull (parsedQuery.entityDropping ()) then
                    executeDroppingRequest (parsedQuery.entityDropping ())
                else
                    Result.Error("Cannot identify the query type!")

        }
