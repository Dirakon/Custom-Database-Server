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

    let parseAsSomeQuery query =
        parseAsContext (query, (fun parser -> parser.someQuery ()))
