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

    member this.AsIdentifyingString =
        match this with
        | Int i -> string i
        | String s -> s
        | Bool b -> string b
        | List values ->
            "["
            + String.concat "," (values |> Seq.map (fun value -> value.AsIdentifyingString))
            + "]"

module Value =
    let examplesOfEveryType = [ Int 1; String ""; Bool true; List [] ]

    let isValidType (``type``: string) =
        examplesOfEveryType
        |> List.map (fun value -> value.TypeName)
        |> List.contains ``type``
