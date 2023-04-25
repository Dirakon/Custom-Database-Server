namespace CustomDatabase

open System
open System.Linq
open Antlr4.Runtime
open Antlr4.Runtime.Tree
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

    let rec getContextTextSeparatedBySpace (context: IParseTree) =
        match context with
        | :? ITerminalNode as terminalNode -> terminalNode.GetText()
        | _ ->
            Enumerable.Range(0, context.ChildCount)
            |> Seq.map (fun childIndex -> getContextTextSeparatedBySpace(context.GetChild(childIndex)).Trim())
            |> Seq.filter (fun child -> not (String.IsNullOrWhiteSpace(child)))
            |> Seq.fold (fun a b -> if a = "" then b else a + " " + b) ""

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
