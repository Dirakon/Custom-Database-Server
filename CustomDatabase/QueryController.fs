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

    let rec processAndAggregateRecursiveStructure
        (
            someStructure: 'structure,
            singleElementExtractor: ('structure -> 'single),
            otherElementsExtractor: ('structure -> 'structure),
            processingFunction: ('single -> Result<'processedSingle, 'err>),
            aggregationFunction: ('processedSingle -> 'processedMultiple -> Result<'processedMultiple, 'err>),
            defaultValue: 'processedMultiple
        ) : Result<'processedMultiple, 'err> =
        match someStructure with
        | null -> Result.Ok defaultValue
        | _ ->
            result {
                let! otherElementsOutput =
                    processAndAggregateRecursiveStructure (
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


    let aggregateConstraints (constraintText: string) otherConstraints =
        let lowerConstraint = constraintText.ToLower()

        match lowerConstraint with
        | "unique" -> Result.Ok { otherConstraints with unique = true }
        | _ -> Result.Error $"Unknown constraint keyword: {lowerConstraint}"

    let parseConstraintsRecursively (constraintsDeclaration: QueryLanguageParser.ConstraintsDeclarationContext) =
        processAndAggregateRecursiveStructure (
            constraintsDeclaration,
            (fun constraints -> constraints.constraintDeclaration ()),
            (fun constraints -> constraints.constraintsDeclaration ()),
            (fun constraintContext -> Result.Ok(constraintContext.GetText())),
            aggregateConstraints,
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


    let parseColumnsRecursively (membersDeclaration: QueryLanguageParser.MembersDeclarationContext) =
        processAndAggregateRecursiveStructure (
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


    let parsePointersRecursively (pointers: QueryLanguageParser.MultipleRawPointersContext) =
        processAndAggregateRecursiveStructure (
            pointers,
            (fun pointers -> pointers.rawPointer ()),
            (fun pointers -> pointers.multipleRawPointers ()),
            (fun pointer -> Result.Ok <| pointer.GetText()),
            (fun singleItem otherItems -> Result.Ok(singleItem :: otherItems)),
            []
        )

    let executeCreationRequest (context: QueryLanguageParser.EntityCreationContext) =
        result {
            let! describedEntity = parseEntity context
            return! dataStorage.createEntity describedEntity
        }
        |> Result.map (fun _ -> "Successful!")
        |> Result.map JsonConverter.serialize

    let executeAdditionRequest (context: QueryLanguageParser.EntityAdditionContext) =
        result {
            if notNull (context.entitySingleAddition ()) then
                let additionRequest = context.entitySingleAddition ()
                let entityName = additionRequest.entityName().GetText()

                return!
                    dataStorage.addEntities (
                        entityName,
                        [ JsonConverter.parseSingleRow (additionRequest.jsonObj().getTextSeparatedBySpace ()) ]
                    )
            else if notNull (context.entityGroupAddition ()) then
                let additionRequest = context.entityGroupAddition ()
                let entityName = additionRequest.entityName().GetText()

                return!
                    dataStorage.addEntities (
                        entityName,
                        JsonConverter.parseMultipleRows (additionRequest.jsonArr().getTextSeparatedBySpace ())
                    )
            else
                return! Result.Error $"Cannot analyze query type of '{context.getTextSeparatedBySpace ()}'"
        }
        |> Result.map (fun _ -> "Successful!")
        |> Result.map JsonConverter.serialize


    let executeReplacementQuery (replacementQuery: QueryLanguageParser.EntityReplacementContext) =
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
                    parsePointersRecursively (replacementQuery.entityGroupReplacement().multipleRawPointers ())

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
        |> Result.map JsonConverter.serialize

    let executeRetrievalRequest (retrievalQuery: QueryLanguageParser.EntityRetrievalContext) =
        result {

            if notNull (retrievalQuery.entitySingleRetrieval ()) then
                let pointer = retrievalQuery.entitySingleRetrieval().rawPointer().GetText()
                return! dataStorage.retrieveEntities ([ pointer ])
            else if notNull (retrievalQuery.entityGroupRetrieval ()) then
                let! pointers = parsePointersRecursively (retrievalQuery.entityGroupRetrieval().multipleRawPointers ())
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
                        getQuery.entitySingleSelection().entityName().GetText(),
                        filteringFunction
                    )

                return!
                    match allSelectedEntities with
                    | [] -> Result.Error "No entities found!"
                    | firstEntity :: _ -> Result.Ok [ firstEntity ]
            else if notNull (getQuery.entityGroupSelection ()) then
                return!
                    dataStorage.selectEntities (
                        getQuery.entityGroupSelection().entityName().GetText(),
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
                    executeReplacementQuery (parsedQuery.entityReplacement ())
                else
                    Result.Error("Cannot identify the query type!")

        }
