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
            |> Seq.filter (not << String.IsNullOrWhiteSpace)
            |> Seq.fold
                (fun accumulator element ->
                    if accumulator = "" then
                        element
                    else
                        accumulator + " " + element)
                ""
