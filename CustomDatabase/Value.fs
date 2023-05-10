namespace CustomDatabase.Value


open Microsoft.FSharp.Reflection


/// A value which can represent the following things: int, string, bool, list of values, a pointer (which is also string)
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
        | String s -> "\"" + s + "\""
        | Bool b -> string b
        | List values ->
            "["
            + String.concat "," (values |> Seq.map (fun value -> value.AsIdentifyingString))
            + "]"

module Value =
    let examplesOfEveryType = [ Int 1; String ""; Bool true; List [] ]

    let isValidType (``type``: string) (allEntityNames: string list) =
        let lowerType = ``type``.ToLower()

        let allPrimitiveTypesLower =
            examplesOfEveryType
            |> List.map (fun typeExample -> typeExample.TypeName.ToLower())

        let entityNamesLower =
            allEntityNames |> List.map (fun entityName -> entityName.ToLower())

        let rec recursiveValidation (typePart: string) =
            if typePart.StartsWith "[" && typePart.EndsWith "]" then
                recursiveValidation (typePart.Substring(1, typePart.Length - 2))
            else if typePart.StartsWith "&" then
                entityNamesLower |> List.contains (typePart.Substring(1))
            else
                allPrimitiveTypesLower |> List.contains typePart

        recursiveValidation lowerType

    let rec valueDescribedIsOfType (value: Value) (columnType: string) : bool =
        match value with
        | String s when columnType.StartsWith "&" -> s.StartsWith(columnType.Substring(1))
        | List values ->
            if columnType.StartsWith "[" && columnType.EndsWith "]" then
                let columnSubType = columnType.Substring(1, columnType.Length - 2)

                values
                |> Seq.forall (fun subValue -> valueDescribedIsOfType subValue columnSubType)
            else
                false
        | _ -> value.TypeName.ToLower() = columnType
