namespace CustomDatabase


open System.IO
open System.Text.Json
open CustomDatabase.MiscExtensions
open CustomDatabase.Value
open FSharpPlus
open FSharpPlus.Data
open FsToolkit.ErrorHandling
open FsToolkit.ErrorHandling.Operator.Option
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Logging
open Microsoft.FSharp.Collections
open FsToolkit.ErrorHandling.Operator.Result
open Microsoft.FSharp.Core



type DataStorage(webHostEnvironment: IWebHostEnvironment, logger: ILogger<DataStorage>) =
    let dataFolderPath = Path.Join(webHostEnvironment.ContentRootPath, "data")
    let globalEntityInfoFilePath = Path.Join(dataFolderPath, "entity.json")

    let entityFilePath (entityName: string) : string =
        Path.Join(dataFolderPath, $"{entityName}.json")

    let mutable entities: Entity list = []

    let jsonSerializerOptions = JsonConverter.serializerOptions


    do
        lock entities (fun () ->
            if File.Exists(globalEntityInfoFilePath) then
                use fileStream = File.OpenRead(globalEntityInfoFilePath)
                entities <- JsonSerializer.Deserialize(fileStream, jsonSerializerOptions)
            else
                logger.LogInformation "No entity information file found! First initialization..."
                Directory.CreateDirectory(dataFolderPath) |> ignore
                use fileStream = File.OpenWrite(globalEntityInfoFilePath)
                JsonSerializer.Serialize(fileStream, entities, jsonSerializerOptions))


    let readEntityInstances entityName =
        Result.fromThrowingFunction (fun () ->
            use readFileStream = File.OpenRead(entityFilePath entityName)

            let entityInstances: EntityInstance list =
                JsonSerializer.Deserialize(readFileStream, jsonSerializerOptions)

            entityInstances)

    let writeEntityInstances entityName entities =
        Result.fromThrowingFunction (fun () ->
            File.Delete(entityFilePath entityName)
            use writeFileStream = File.OpenWrite(entityFilePath entityName)

            JsonSerializer.Serialize(writeFileStream, entities, jsonSerializerOptions)

        )

    let removeEntityInstances entityName =
        Result.fromThrowingFunction (fun () -> File.Delete(entityFilePath entityName))

    let updateEntityListWith newEntities =
        Result.fromThrowingFunction (fun () ->
            File.Delete(globalEntityInfoFilePath)
            use globalFileStream = File.OpenWrite(globalEntityInfoFilePath)
            JsonSerializer.Serialize(globalFileStream, newEntities, jsonSerializerOptions)
            entities <- newEntities)

    let getEntityByName (entityName: string) : Entity option =
        entities |> Seq.tryFind (fun entity -> entity.Name = entityName)

    let isEntityDefined (entityDescription: Entity) : bool =
        (getEntityByName entityDescription.Name).IsSome


    let tryAppendEntityRowsToFileUnchecked
        (
            entityRows: Value list list,
            entityDefinition: Entity
        ) : Result<string list, string> =
        let entityName = entityDefinition.Name

        lock entityDefinition (fun () ->
            result {
                let! previousEntityInstances = readEntityInstances entityName

                let newEntityInstances =
                    EntityInstance.fromEntityRows (previousEntityInstances, entityDefinition, entityRows)

                let allEntities = newEntityInstances |> List.append previousEntityInstances

                if EntityInstance.hasDuplicateValuesOnUniqueColumnsUnchecked (allEntities, entityDefinition) then
                    return! Result.Error "Some 'unique'-constrained column has duplicate elements!"
                else
                    do! writeEntityInstances entityName allEntities
                    return (newEntityInstances |> map (fun instance -> instance.Pointer))
            })


    interface IDataStorage with
        member this.AddEntities(entityName, entityRows) =
            result {
                let! entityDefinition =
                    getEntityByName entityName
                    |> Option.toResultWith $"'{entityName}' is not defined"

                let! unlabeledEntityRows = LabeledEntityRow.unlabelMultipleKnowingDefinition entityDefinition entityRows

                if
                    unlabeledEntityRows
                    |> Seq.forall (UnlabeledEntityRow.correspondsWithDefinition entityDefinition)
                then
                    return! tryAppendEntityRowsToFileUnchecked (unlabeledEntityRows, entityDefinition)
                else
                    return!
                        Result.Error("Some values have incorrect types that don't correspond with entity definition")
            }

        member this.CreateEntity(entityDescription) =
            if isEntityDefined entityDescription then
                Result.Error $"'{entityDescription.Name}' is already defined"
            elif String.endsWithDigit entityDescription.Name then
                Result.Error $"'{entityDescription.Name}' ends with a digit, which is not allowed"
            else

                lock entities (fun () ->
                    let newEntities = (entityDescription :: entities)

                    result {
                        do! updateEntityListWith newEntities
                        return! writeEntityInstances entityDescription.Name []
                    })

        member this.SelectEntities(entityName, maybeFilteringFunction) =
            result {
                let! entityDefinition =
                    getEntityByName entityName
                    |> Option.toResultWith $"'{entityName}' is not defined"

                let! entityInstances = readEntityInstances entityName

                let entityToLabeledValues =
                    (EntityInstance.asLabeledRowKnowingDefinition entityDefinition)

                return!
                    match maybeFilteringFunction with
                    | None -> entityInstances |> List.map entityToLabeledValues |> Result.Ok
                    | Some filteringFunction ->
                        entityInstances
                        |> List.filterResultM (fun instance ->
                            EntityInstance.expressionHolds (instance, entityDefinition, filteringFunction))
                        |> Result.map (List.map entityToLabeledValues)

            }

        member this.RetrieveEntities(pointers) =
            match pointers with
            | [] -> Result.Ok([])
            | somePointer :: otherPointers ->
                let entityName = Pointer.toEntityName somePointer

                result {
                    let! entityDefinition =
                        getEntityByName entityName
                        |> Option.toResultWith $"'{entityName}' is not defined"

                    let! entityInstances = readEntityInstances entityName

                    let entityToLabeledValues =
                        (EntityInstance.asLabeledRowKnowingDefinition entityDefinition)

                    return!
                        pointers
                        |> List.map (
                            (Pointer.toIndexKnowingEntityName entityName)
                            >> (fun index ->
                                entityInstances
                                |> Seq.tryFind (fun instance ->
                                    Pointer.toIndexKnowingEntityName entityDefinition.Name instance.Pointer = index)
                                |> Option.toResultWith $"Cannot find element by the index {index}")
                        )
                        |> List.sequenceResultM
                        |> Result.map (List.map entityToLabeledValues)

                }

        member this.ReplaceEntities(pointers, replacementEntities) =
            if replacementEntities.Length <> pointers.Length then
                Result.Error "Pointer and entity count differ!"
            else
                match pointers |> NonEmptyList.tryOfList with
                | None -> Result.Ok()
                | Some somePointers ->
                    result {
                        let! (entityName, pointerIndices) = somePointers |> Pointers.asSingleEntityIndices

                        let! entityDefinition =
                            (getEntityByName entityName)
                            |> Option.toResultWith $"'{entityName}' is not defined"

                        let! entityInstances = readEntityInstances entityName

                        let! unlabeledEntityRows =
                            replacementEntities
                            |> LabeledEntityRow.unlabelMultipleKnowingDefinition entityDefinition

                        let replacementEntitiesSortedByIndex =
                            unlabeledEntityRows |> List.zip pointerIndices |> List.sortBy fst

                        let! updatedEntities =
                            EntityInstance.tryReplaceEntities (
                                entityDefinition,
                                entityInstances,
                                replacementEntitiesSortedByIndex
                            )

                        if
                            EntityInstance.hasDuplicateValuesOnUniqueColumnsUnchecked (
                                updatedEntities,
                                entityDefinition
                            )
                        then
                            return! Result.Error "Some 'unique'-constrained column has duplicate elements!"
                        else
                            return! writeEntityInstances entityName updatedEntities
                    }

        member this.GetEntityDefinitions() = entities

        member this.DropEntity(entityName) =
            match (getEntityByName entityName) with
            | None -> Result.Error $"'{entityName}' is not defined"
            | Some entityDefinition ->
                lock entities (fun () ->
                    result {
                        let newEntities = entities |> List.filter (fun entity -> entity.Name <> entityName)
                        do! updateEntityListWith newEntities
                        return! removeEntityInstances entityName
                    })

        member this.RemoveEntities(pointers) =
            match NonEmptyList.tryOfList pointers with
            | None -> Result.Ok()
            | Some somePointers ->

                result {
                    let! (entityName, pointerIndices) = somePointers |> Pointers.asSingleEntityIndices

                    let! entityDefinition =
                        getEntityByName entityName
                        |> Option.toResultWith $"'{entityName}' is not defined"

                    let! entityInstances = readEntityInstances entityName

                    let sortedPointerIndices = pointerIndices |> List.sort

                    let! updatedEntities =
                        EntityInstance.tryRemoveEntities (entityDefinition, entityInstances, sortedPointerIndices)

                    return! writeEntityInstances entityName updatedEntities
                }
