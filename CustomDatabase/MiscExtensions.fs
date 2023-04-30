module CustomDatabase.MiscExtensions

open FsToolkit.ErrorHandling

type Option<'ok> with

    member this.toResult<'err>(errorValue: 'err) =
        match this with
        | Some value -> Result.Ok value
        | None -> Result.Error errorValue

module Option =
    let toResult<'ok, 'err> (errorValue: 'err) (option: Option<'ok>) = option.toResult (errorValue)

module Result =
    let flatten (result: Result<Result<'a, 'b>, 'b>) = result |> Result.bind id

    let fromThrowingFunction (func: unit -> 'ok) : Result<'ok, string> =
        try
            func () |> Result.Ok
        with e ->
            ("Exception Raised: " + e.Message + "\n" + e.Source) |> Result.Error

module List =
    let filterResultM<'a, 'b> (filteringFunction: ('a) -> Result<bool, 'b>) (list: 'a list) : Result<'a list, 'b> =
        result {
            let! itemsAndFilteringFunctionResults =
                list
                |> List.map (fun item -> filteringFunction (item) |> Result.map (fun res -> (item, res)))
                |> List.sequenceResultM

            return itemsAndFilteringFunctionResults |> List.filter snd |> List.map fst
        }
