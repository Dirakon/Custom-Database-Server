namespace CustomDatabase

open System.Collections.Generic
open Antlr4.Runtime
open CustomDatabase.Antlr
open CustomDatabase.Value
open FsToolkit.ErrorHandling
open GeneratedLanguage

module QueryParser =
    let parseAsContext<'A when 'A :> ParserRuleContext>
        (
            query: string,
            contextSelection: QueryLanguageParser -> 'A
        ) : Result<'A, string> =
        let parser = QueryLanguage.QueryLanguage.GetParser(query)
        parser.RemoveErrorListeners()
        parser.AddErrorListener(ThrowingErrorListener<IToken>())

        try
            let parsedType: 'A = contextSelection parser
            Result.Ok parsedType
        with error ->
            Result.Error(error.Message)

    let parseAsSomeQuery query =
        parseAsContext (query, (fun parser -> parser.someQuery ()))

    let rec processAndAggregateRecursiveStructure
        (
            someStructure: 'Structure,
            singleElementExtractor: 'Structure -> 'Single,
            otherElementsExtractor: 'Structure -> 'Structure,
            recursionEndFunction: 'Structure -> bool,
            processingFunction: 'Single -> Result<'ProcessedSingle, 'Err>,
            aggregationFunction: 'ProcessedSingle -> 'ProcessedMultiple -> Result<'ProcessedMultiple, 'Err>,
            defaultValue: 'ProcessedMultiple
        ) : Result<'ProcessedMultiple, 'Err> =
        if (recursionEndFunction someStructure) then
            Result.Ok defaultValue
        else
            result {
                let! otherElementsOutput =
                    processAndAggregateRecursiveStructure (
                        otherElementsExtractor someStructure,
                        singleElementExtractor,
                        otherElementsExtractor,
                        recursionEndFunction,
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
        | "unique" -> Result.Ok { otherConstraints with Unique = true }
        | _ -> Result.Error $"Unknown constraint keyword: {lowerConstraint}"

    let parseConstraintsRecursively (constraintsDeclaration: QueryLanguageParser.ConstraintsDeclarationContext) =
        processAndAggregateRecursiveStructure (
            constraintsDeclaration,
            (fun constraints -> constraints.constraintDeclaration ()),
            (fun constraints -> constraints.constraintsDeclaration ()),
            (fun constraints -> isNull constraints ||isNull (constraints.constraintDeclaration())),
            (fun constraintContext -> Result.Ok(constraintContext.GetText())),
            aggregateConstraints,
            ColumnConstraints.emptyConstraints
        )

    let parseType (``type``: string, entityNames: string list) =
        if ``type``.Length = 0 then
            Result.Error "Cannot have an empty type"
        else if Value.isValidType ``type`` entityNames then
            Result.Ok(``type``.ToLower())
        else
            Result.Error $"Unknown type: '{``type``}'"

    let parseColumn (entityNames: string list) (memberDeclaration: QueryLanguageParser.MemberDeclarationContext) =
        result {
            let! constraints = parseConstraintsRecursively (memberDeclaration.constraintsDeclaration ())
            let! ``type`` = parseType (memberDeclaration.``type``().GetText(), entityNames)

            return
                { Name = memberDeclaration.memberName().GetText()
                  Type = ``type``
                  Constraints = constraints }
        }


    let parseColumnsRecursively
        (
            membersDeclaration: QueryLanguageParser.MembersDeclarationContext,
            entityNames: string list
        ) =
        processAndAggregateRecursiveStructure (
            membersDeclaration,
            (fun declaration -> declaration.memberDeclaration ()),
            (fun declaration -> declaration.membersDeclaration ()),
            (fun declaration -> isNull declaration || isNull(declaration.memberDeclaration ())),
            parseColumn entityNames,
            (fun singleItem otherItems -> Result.Ok(singleItem :: otherItems)),
            []
        )

    let parseEntity (context: QueryLanguageParser.EntityCreationContext, entityNames: string list) =
        let entityName = context.entityName().GetValidName ()
        let entityNamesIncludingSelf = entityName :: entityNames

        result {
            let! columns = parseColumnsRecursively (context.membersDeclaration (), entityNamesIncludingSelf)
            let uniqueColumnNames = HashSet(columns |> Seq.map (fun column -> column.Name))

            if
                entityNames
                |> List.map (fun entityName -> entityName.ToLower())
                |> List.contains (entityName.ToLower())
            then
                return! Result.Error "Entity name already defined!"
            else if uniqueColumnNames.Count < columns.Length then
                return! Result.Error "Detected duplicate column names!"
            else if uniqueColumnNames.Contains("pointer") then
                return! Result.Error "Detected a column named 'pointer', which is not allowed!"
            else
                return { Name = entityName; Columns = columns }
        }


    let parsePointersRecursively (pointers: QueryLanguageParser.MultipleRawPointersContext) =
        processAndAggregateRecursiveStructure (
            pointers,
            (fun pointers -> pointers.rawPointer ()),
            (fun pointers -> pointers.multipleRawPointers ()),
            (fun pointers -> isNull pointers  || isNull(pointers.rawPointer ())),
            (fun pointer -> Result.Ok <| pointer.GetText()),
            (fun singleItem otherItems -> Result.Ok(singleItem :: otherItems)),
            []
        )
