namespace CustomDatabase

open System.Collections.Generic
open Antlr4.Runtime
open CustomDatabase.Antlr
open CustomDatabase.Value
open FsToolkit.ErrorHandling
open GeneratedLanguage

module QueryParser =
    let parseAsContext<'a when 'a :> ParserRuleContext>
        (
            query: string,
            contextSelection: QueryLanguageParser -> 'a
        ) : Result<'a, string> =
        let parser = QueryLanguage.QueryLanguage.GetParser(query)
        parser.RemoveErrorListeners()
        parser.AddErrorListener(ThrowingErrorListener<IToken>())

        try
            let parsedType: 'a = contextSelection parser
            Result.Ok parsedType
        with error ->
            Result.Error(error.Message)

    let parseAsSomeQuery query =
        parseAsContext (query, (fun parser -> parser.someQuery ()))

    let rec processAndAggregateRecursiveStructure
        (
            someStructure: 'structure,
            singleElementExtractor: 'structure -> 'single,
            otherElementsExtractor: 'structure -> 'structure,
            recursionEndFunction: 'structure -> bool,
            processingFunction: 'single -> Result<'processedSingle, 'err>,
            aggregationFunction: 'processedSingle -> 'processedMultiple -> Result<'processedMultiple, 'err>,
            defaultValue: 'processedMultiple
        ) : Result<'processedMultiple, 'err> =
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
        | "unique" -> Result.Ok { otherConstraints with unique = true }
        | _ -> Result.Error $"Unknown constraint keyword: {lowerConstraint}"

    let parseConstraintsRecursively (constraintsDeclaration: QueryLanguageParser.ConstraintsDeclarationContext) =
        processAndAggregateRecursiveStructure (
            constraintsDeclaration,
            (fun constraints -> constraints.constraintDeclaration ()),
            (fun constraints -> constraints.constraintsDeclaration ()),
            (fun constraints -> constraints = null || constraints.constraintDeclaration () = null),
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
                { name = memberDeclaration.memberName().GetText()
                  ``type`` = ``type``
                  constraints = constraints }
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
            (fun declaration -> declaration = null || declaration.memberDeclaration () = null),
            parseColumn entityNames,
            (fun singleItem otherItems -> Result.Ok(singleItem :: otherItems)),
            []
        )

    let parseEntity (context: QueryLanguageParser.EntityCreationContext, entityNames: string list) =
        let entityName = context.entityName().getValidName ()
        let entityNamesIncludingSelf = entityName :: entityNames

        result {
            let! columns = parseColumnsRecursively (context.membersDeclaration (), entityNamesIncludingSelf)
            let uniqueColumnNames = HashSet(columns |> Seq.map (fun column -> column.name))

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
                return { name = entityName; columns = columns }
        }


    let parsePointersRecursively (pointers: QueryLanguageParser.MultipleRawPointersContext) =
        processAndAggregateRecursiveStructure (
            pointers,
            (fun pointers -> pointers.rawPointer ()),
            (fun pointers -> pointers.multipleRawPointers ()),
            (fun pointers -> pointers = null || pointers.rawPointer () = null),
            (fun pointer -> Result.Ok <| pointer.GetText()),
            (fun singleItem otherItems -> Result.Ok(singleItem :: otherItems)),
            []
        )
