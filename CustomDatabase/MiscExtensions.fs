module CustomDatabase.MiscExtensions

type Option<'ok> with

    member this.toResult<'err>(errorValue: 'err) =
        match this with
        | Some value -> Result.Ok value
        | None -> Result.Error errorValue

module Option =
    let toResult<'ok, 'err> (errorValue: 'err) (option: Option<'ok>) = option.toResult (errorValue)

module Result =
    let fromThrowingFunction (func: unit -> 'ok) : Result<'ok, string> =
        try
            func () |> Result.Ok
        with e ->
            ("Exception Raised: " + e.Message + "\n" + e.Source) |> Result.Error
