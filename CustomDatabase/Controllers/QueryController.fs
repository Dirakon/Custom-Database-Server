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
    // TODO: place in different file/class all actual query executions

    let rec recursivelyProcessAStructure
        ((someStructure,
          singleElementExtractor,
          otherElementsExtractor,
          processingFunction,
          aggregationFunction,
          defaultValue):
            'structure *
            ('structure -> 'single) *
            ('structure -> 'structure) *
            ('single -> Result<'processedSingle, 'err>) *
            ('processedSingle -> 'processedMultiple -> Result<'processedMultiple, 'err>) *
            'processedMultiple)
        : Result<'processedMultiple, 'err> =
        match someStructure with
        | null -> Result.Ok defaultValue
        | _ ->
            result {
                let! otherElementsOutput =
                    recursivelyProcessAStructure (
                        otherElementsExtractor someStructure,
                        singleElementExtractor,
                        otherElementsExtractor,
                        processingFunction,
                        aggregationFunction,
                        defaultValue
                    )

                let! thisElementProcessed = someStructure |> singleElementExtractor |> processingFunction
                let! elementsAggregated = aggregationFunction thisElementProcessed otherElementsOutput
                return elementsAggregated
            }


    let parseConstraint (constraintDeclaration: QueryLanguageParser.ConstraintDeclarationContext) otherConstraints =
        let lowerConstraint = constraintDeclaration.GetText().ToLower()

        match lowerConstraint with
        | "unique" -> Result.Ok { otherConstraints with unique = true }
        | _ -> Result.Error $"Unknown constraint keyword: {lowerConstraint}"

    let rec parseConstraintsRecursively (constraintsDeclaration: QueryLanguageParser.ConstraintsDeclarationContext) =
        recursivelyProcessAStructure (
            constraintsDeclaration,
            (fun constraints -> constraints.constraintDeclaration ()),
            (fun constraints -> constraints.constraintsDeclaration ()),
            Result.Ok,
            parseConstraint,
            ColumnConstraints.emptyConstraints
        )

    let parseType (``type``: string) =
        if ``type``.Length = 0 then
            Result.Error "Cannot have an empty type"
        else
            let lowerType = ``type``.ToLower()

            let typeWithCapitalizedStart =
                lowerType[0].ToString().ToUpper() + lowerType.Substring(1)

            if Value.isValidType typeWithCapitalizedStart then
                Result.Ok typeWithCapitalizedStart
            else
                Result.Error $"Unknown type: '{typeWithCapitalizedStart}'"

    let parseColumn (memberDeclaration: QueryLanguageParser.MemberDeclarationContext) =
        result {
            let! constraints = parseConstraintsRecursively (memberDeclaration.constraintsDeclaration ())
            let! ``type`` = parseType (memberDeclaration.``type``().GetText())

            return
                { name = memberDeclaration.memberName().GetText()
                  ``type`` = ``type``
                  constraints = constraints }
        }


    let rec parseColumnsRecursively (membersDeclaration: QueryLanguageParser.MembersDeclarationContext) =
        recursivelyProcessAStructure (
            membersDeclaration,
            (fun constraints -> constraints.memberDeclaration ()),
            (fun constraints -> constraints.membersDeclaration ()),
            parseColumn,
            (fun singleItem otherItems -> Result.Ok(singleItem :: otherItems)),
            []
        )

    let parseEntity (context: QueryLanguageParser.EntityCreationContext) =
        let entityName = context.entityName().GetText()

        result {
            let! columns = parseColumnsRecursively (context.membersDeclaration ())
            let uniqueColumnNames = HashSet(columns |> Seq.map (fun column -> column.name))

            if uniqueColumnNames.Count < columns.Length then
                return! Result.Error "Detected duplicate column names!"
            else if uniqueColumnNames.Contains("pointer") then
                return! Result.Error "Detected a column named 'pointer', which is not allowed!"
            else
                return { name = entityName; columns = columns }
        }

    let executeCreationRequest (context: QueryLanguageParser.EntityCreationContext) =
        result {
            let! describedEntity = parseEntity context
            return! dataStorage.createEntity describedEntity
        }
        |> Result.map (fun _ -> "Successful!")

    let executeAdditionRequest (context: QueryLanguageParser.EntityAdditionContext) =
        result {
            if notNull (context.entitySingleAddition ()) then
                return! Result.Error "TODO: single entity retrieval (get first)"
            else if notNull (context.entityGroupAddition ()) then
                let additionRequest = context.entityGroupAddition ()
                let entityName = additionRequest.entityName().GetText()
                printfn $"ur json: {additionRequest.jsonArr().getTextSeparatedBySpace ()}"

                return!
                    dataStorage.addEntities (
                        entityName,
                        // TODO: this parsing might be wrong. We expect dict [column name -> value]
                        // Which we need to translate to [value] ordered by columns
                        // Good luck
                        JsonConverter.parseMultipleRows (additionRequest.jsonArr().getTextSeparatedBySpace ())
                    )
            else
                return! Result.Error $"Cannot analyze query type of '{context.getTextSeparatedBySpace ()}'"
        }
        |> Result.map (fun _ -> "Successful!")


    [<HttpGet>] //TODO: do literal RETRIEVE query
    member this.Retrieve([<Required>] query: string) =
        result {
            let! retrievalQuery = QueryParser.parseAsRetrievalQuery query

            let filteringFunction =
                if notNull (retrievalQuery.booleanExpression ()) then
                    Some(retrievalQuery.booleanExpression ())
                else
                    None

            if notNull (retrievalQuery.entitySingleRetrieval ()) then
                return! Result.Error "TODO: single entity retrieval (get first)"
            else if notNull (retrievalQuery.entityGroupRetrieval ()) then
                return!
                    dataStorage.getEntities (
                        retrievalQuery.entityGroupRetrieval().entityName().GetText(),
                        filteringFunction
                    )
            else
                return! Result.Error $"Cannot analyze query type of '{retrievalQuery.getTextSeparatedBySpace ()}'"
        }


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
            | Result.Ok creationContext, _ -> executeCreationRequest (creationContext)
            | _, Result.Ok additionContext -> executeAdditionRequest (additionContext)

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
