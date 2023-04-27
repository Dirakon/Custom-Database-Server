namespace CustomDatabase.Value


open Microsoft.FSharp.Reflection


//[<JsonConverter(typeof<ValueResolver>)>]
type Value =
    | Int of int
    | String of string
    | Bool of bool
    | List of Value list

    member this.TypeName =
        match FSharpValue.GetUnionFields(this, this.GetType()) with
        | case, _ -> case.Name
