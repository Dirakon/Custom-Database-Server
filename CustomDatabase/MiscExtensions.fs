module CustomDatabase.MiscExtensions

open FsToolkit.ErrorHandling

type Option<'Ok> with

    member this.ToResult<'Err>(errorValue: 'Err) =
        match this with
        | Some value -> Result.Ok value
        | None -> Result.Error errorValue

module Char =
    let isDigit (character: char) : bool = character >= '0' && character <= '9'

module String =
    let endsWithDigit (str: string) : bool =
        if str.Length = 0 then
            false
        else
            let lastChar = str[str.Length - 1]
            Char.isDigit lastChar

module Option =
    let fromNullable nullableObject =
        match nullableObject with
        | null -> None
        | _ -> Some(nullableObject)


module Result =
    let fromThrowingFunction (func: unit -> 'Ok) : Result<'Ok, string> =
        try
            func () |> Result.Ok
        with e ->
            ("Exception Raised: " + e.Message + "\n" + e.Source) |> Result.Error

module List =
    let filterResultM<'Ok, 'Err>
        (filteringFunction: 'Ok -> Result<bool, 'Err>)
        (list: 'Ok list)
        : Result<'Ok list, 'Err> =
        result {
            let! itemsAndFilteringFunctionResults =
                list
                |> List.map (fun item -> filteringFunction item |> Result.map (fun res -> (item, res)))
                |> List.sequenceResultM

            return itemsAndFilteringFunctionResults |> List.filter snd |> List.map fst
        }
