namespace CustomDatabase

open CustomDatabase.MiscExtensions
open FSharpPlus
open FSharpPlus.Data

module Pointer =
    let rec toEntityName (pointer: string) =
        if pointer |> String.endsWithDigit then
            toEntityName (pointer.Substring(0, pointer.Length - 1))
        else
            pointer

    let toIndexKnowingEntityName (entityName: string) (pointer: string) =
        int (pointer.Substring(entityName.Length))


module Pointers =
    let asSingleEntityIndices (pointers: string NonEmptyList) : Result<string * int list, string> =
        let entityName = pointers.Head |> Pointer.toEntityName

        if
            pointers.Tail
            |> List.exists (fun otherPointer -> Pointer.toEntityName otherPointer <> entityName)
        then
            Result.Error "Pointers correspond with different entities, which is not supported yet!"
        else
            Result.Ok(
                entityName,
                pointers
                |> NonEmptyList.toList
                |> List.map (Pointer.toIndexKnowingEntityName entityName)
            )
