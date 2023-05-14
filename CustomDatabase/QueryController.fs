namespace CustomDatabase.Controllers


open System.ComponentModel.DataAnnotations
open CustomDatabase
open CustomDatabase.MiscExtensions
open CustomDatabase.Expressions
open FSharpPlus
open FSharpPlus.Data
open GeneratedLanguage
open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.Logging
open CustomDatabase.Antlr
open FsToolkit.ErrorHandling
open FsToolkit.ErrorHandling.Operator.Result

[<ApiController>]
[<Route("[controller]")>]
type QueryController(logger: ILogger<QueryController>, dataStorage: IDataStorage) =
    inherit ControllerBase()

    let successfulUnitOperationDescription = "Successful!"

    let executeCreationRequest (context: QueryLanguageParser.EntityCreationContext) =
        result {
            let entityNames =
                dataStorage.GetEntityDefinitions() |> List.map (fun entity -> entity.Name)

            let! describedEntity = QueryParser.parseEntity (context, entityNames)
            return! dataStorage.CreateEntity describedEntity
        }
        |> Result.map (fun _ -> successfulUnitOperationDescription)
        |> Result.map JsonConverter.serialize

    let executeAdditionRequest (context: QueryLanguageParser.EntityAdditionContext) =
        result {
            let entityName = context.entityName().GetValidName()

            return!
                dataStorage.AddEntities(
                    entityName,
                    JsonConverter.parseMultipleRows (context.jsonArr().GetTextSeparatedBySpace())
                )
        }
        |> Result.map JsonConverter.serialize


    let executeReplacementRequest (replacementQuery: QueryLanguageParser.EntityReplacementContext) =
        result {

            let! pointers = QueryParser.parsePointersRecursively (replacementQuery.multipleRawPointers ())

            return!
                dataStorage.ReplaceEntities(
                    pointers,
                    JsonConverter.parseMultipleRows (replacementQuery.jsonArr().GetTextSeparatedBySpace())
                )
        }
        |> Result.map (fun _ -> successfulUnitOperationDescription)
        |> Result.map JsonConverter.serialize

    let executeRetrievalRequest (retrievalQuery: QueryLanguageParser.EntityRetrievalContext) =
        result {

            let! pointers = QueryParser.parsePointersRecursively (retrievalQuery.multipleRawPointers ())

            return! dataStorage.RetrieveEntities pointers
        }
        |> Result.map JsonConverter.serialize

    let executeSelectionRequest (getQuery: QueryLanguageParser.EntitySelectionContext) =
        result {

            let filteringFunction = Option.fromNullable (getQuery.booleanExpression ())

            return! dataStorage.SelectEntities(getQuery.entityName().GetValidName(), filteringFunction)
        }
        |> Result.map JsonConverter.serialize

    let executeDroppingRequest (context: QueryLanguageParser.EntityDroppingContext) =
        result {
            let entityName = context.entityName().GetValidName()

            return! dataStorage.DropEntity(entityName)
        }
        |> Result.map (fun _ -> successfulUnitOperationDescription)
        |> Result.map JsonConverter.serialize

    let executeRemovalRequest (context: QueryLanguageParser.EntityRemovalContext) =
        result {
            let! pointers = QueryParser.parsePointersRecursively (context.multipleRawPointers ())

            return! dataStorage.RemoveEntities(pointers)
        }
        |> Result.map (fun _ -> successfulUnitOperationDescription)
        |> Result.map JsonConverter.serialize


    /// The main endpoint which parses the query and chooses the appropriate method to process it
    [<HttpGet>]
    member this.ExecuteQuery([<Required>] query: string) =
        logger.LogInformation($"Got query: '{query}'")

        let response =
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

        let responseAsString =
            response
            |> Result.map (fun json -> "Ok: " + json.GetRawText())
            |> Result.valueOr ((+) "Error: ")

        logger.LogInformation($"Sending response: {responseAsString}")
        response
