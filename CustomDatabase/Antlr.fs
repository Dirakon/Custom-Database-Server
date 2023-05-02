module CustomDatabase.Antlr

open System
open System.Linq
open Antlr4.Runtime.Tree
open GeneratedLanguage
open Microsoft.FSharp.Core


type IParseTree with

    member this.getTextSeparatedBySpace() =
        match this with
        | :? ITerminalNode as terminalNode -> terminalNode.GetText()
        | _ ->
            Enumerable.Range(0, this.ChildCount)
            |> Seq.map (fun childIndex -> this.GetChild(childIndex).getTextSeparatedBySpace().Trim())
            |> Seq.filter (not << String.IsNullOrWhiteSpace)
            |> String.concat " "

type QueryLanguageParser.EntityNameContext with

    member this.getValidName() = this.GetText().ToLower()

type ThrowingErrorListener<'a>() =
    interface Antlr4.Runtime.IAntlrErrorListener<'a> with
        member this.SyntaxError(output, recognizer, offendingSymbol, line, charPositionInLine, msg, e) = failwith msg
