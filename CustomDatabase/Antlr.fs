module CustomDatabase.Antlr

open System
open System.Linq
open Antlr4.Runtime.Tree


type IParseTree with

    member this.getTextSeparatedBySpace() =
        match this with
        | :? ITerminalNode as terminalNode -> terminalNode.GetText()
        | _ ->
            Enumerable.Range(0, this.ChildCount)
            |> Seq.map (fun childIndex -> this.GetChild(childIndex).getTextSeparatedBySpace().Trim())
            |> Seq.filter (fun child -> not (String.IsNullOrWhiteSpace(child)))
            |> Seq.fold (fun a b -> if a = "" then b else a + " " + b) ""
