namespace CustomDatabase

open Antlr4.Runtime
open CustomDatabase.ThrowErrorListener

module QueryParser =
    let parseAsContext<'a when 'a :> ParserRuleContext>
        (
            query: string,
            contextSelection: GeneratedLanguage.QueryLanguageParser -> 'a
        ) : Result<'a, string> =
        let parser = QueryLanguage.QueryLanguage.GetParser(query)
        parser.RemoveErrorListeners()
        parser.AddErrorListener(ThrowingErrorListener<IToken>())

        try
            let parsedType: 'a = contextSelection (parser)
            Result.Ok parsedType
        with error ->
            Result.Error(error.Message)


    let parseAsAdditionQuery query =
        parseAsContext (query, (fun parser -> parser.entityAddition ()))

    let parseAsRetrievalQuery query =
        parseAsContext (query, (fun parser -> parser.entityRetrieval ()))

    let parseAsRemovalQuery query =
        parseAsContext (query, (fun parser -> parser.entityRemoval ()))

    let parseAsCreationQuery query =
        parseAsContext (query, (fun parser -> parser.entityCreation ()))

    let parseAsReplacementQuery query =
        parseAsContext (query, (fun parser -> parser.entityReplacement ()))
