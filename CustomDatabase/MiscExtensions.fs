module CustomDatabase.MiscExtensions

type Option<'ok> with

    member this.toResult<'err>(errorValue: 'err) =
        match this with
        | Some value -> Result.Ok value
        | None -> Result.Error errorValue
