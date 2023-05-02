namespace CustomDatabase


open System.Collections.Generic
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

    let getEntityByName (entityName: string) : Entity option =
        entities |> Seq.tryFind (fun entity -> entity.name = entityName)

    let isEntityDefined (entityDescription: Entity) : bool =
        (getEntityByName entityDescription.name).IsSome


    let entityRowsToEntityInstances
        (
            previousEntityInstances: seq<EntityInstance>,
            entityDefinition: Entity,
            entityRows: Value list list
        ) : EntityInstance list =
        let entityName = entityDefinition.name

        let upperBoundOnPointerIndex =
            match Seq.tryLast previousEntityInstances with
            | None -> 1
            | Some lastEntity -> EntityInstance.extractIndexFromPointer (entityName, lastEntity.pointer) + 1

        let getNthNewPointer index =
            entityName + string (index + upperBoundOnPointerIndex)

        entityRows
        |> List.mapi (fun index row ->
            { pointer = getNthNewPointer index
              values = row })

    // Scan entity instances for duplicate values on columns with 'unique' constraint,
    // assuming that entity instances are valid
    // (valid here meaning: 1. same amount of values on each instance, 2. correct types on values)
    let hasDuplicateValuesOnUniqueColumns (entities: EntityInstance list, entityDefinition: Entity) : bool =
        let indicesOfUniqueColumns =
            entityDefinition.columns
            |> Seq.mapi (fun index column -> (index, column))
            |> Seq.filter (fun (index, column) -> column.constraints.unique)
            |> Seq.map fst

        let allUniqueValuesForEachUniqueColumn =
            indicesOfUniqueColumns
            |> Seq.map (fun uniqueColumnIndex ->
                HashSet(
                    entities
                    |> Seq.map (fun entity -> entity.values.Item(uniqueColumnIndex).AsIdentifyingString)
                ))

        allUniqueValuesForEachUniqueColumn
        |> Seq.exists (fun uniqueValuesForAColumn -> uniqueValuesForAColumn.Count <> entities.Length)

    let tryAppendEntityRowsToFileUnchecked
        (
            entityRows: Value list list,
            entityDefinition: Entity
        ) : Result<string list, string> =
        let entityName = entityDefinition.name

        lock entityDefinition (fun () ->
            result {
                let! previousEntityInstances = readEntityInstances entityName

                let newEntityInstances =
                    entityRowsToEntityInstances (previousEntityInstances, entityDefinition, entityRows)

                let allEntities = newEntityInstances |> List.append previousEntityInstances

                if hasDuplicateValuesOnUniqueColumns (allEntities, entityDefinition) then
                    return! Result.Error "Some 'unique'-constrained column has duplicate elements!"
                else
                    do! writeEntityInstances entityName allEntities
                    return (newEntityInstances |> map (fun instance -> instance.pointer))
            })

    let unlabelEntityRow
        (
            entityRow: IDictionary<string, Value>,
            entityDefinition: Entity
        ) : Result<Value list, string> =
        if entityRow.Count <> entityDefinition.columns.Length then
            Result.Error
                $"Number of fields is not correct! Expected {entityDefinition.columns.Length}, received {entityRow.Count}"
        else
            entityDefinition.columns
            |> List.map (fun column ->
                entityRow
                |> Seq.tryFind (fun (KeyValue(name, value)) -> name = column.name)
                |> Option.map (fun (KeyValue(name, value)) -> value)
                |> Option.toResultWith
                    $"Could not find a column named '{column.name}' on entity '{entityDefinition.name}'")
            |> List.sequenceResultM

    let unlabelEntityRows entityDefinition entityRows =
        entityRows
        |> List.map (fun labeledEntityRow -> unlabelEntityRow (labeledEntityRow, entityDefinition))
        |> List.sequenceResultM


    let rec tryRemoveEntities
        (
            entityDefinition: Entity,
            entityList: EntityInstance list,
            removalIndices: int list
        ) : Result<EntityInstance list, string> =
        match entityList, removalIndices with
        | [], (unusedIndex :: _) -> Result.Error $"Could not find entity with index '{unusedIndex}'"
        | entities, [] -> Result.Ok entities
        | potentialEntity :: otherPotentialEntities, index :: otherRemovalIndices ->
            if EntityInstance.extractIndexFromPointer (entityDefinition.name, potentialEntity.pointer) = index then
                tryRemoveEntities (entityDefinition, otherPotentialEntities, otherRemovalIndices)
            else
                tryRemoveEntities (entityDefinition, otherPotentialEntities, removalIndices)
                |> Result.map (List.append [ potentialEntity ])

    let rec tryReplaceEntities
        (
            entityDefinition: Entity,
            entityList: EntityInstance list,
            sortedReplacementEntities: (int * Value list) list
        ) : Result<EntityInstance list, string> =
        match entityList, sortedReplacementEntities with
        | [], (index, nextReplacementEntity) :: otherReplacementEntities ->
            Result.Error $"Could not find entity with index '{index}'"
        | entityList, [] -> Result.Ok entityList
        | potentialEntity :: otherPotentialEntities, (index, nextReplacementEntity) :: otherReplacementEntities ->
            if EntityInstance.extractIndexFromPointer (entityDefinition.name, potentialEntity.pointer) = index then
                tryReplaceEntities (entityDefinition, otherPotentialEntities, otherReplacementEntities)
                |> Result.map (
                    List.append
                        [ { potentialEntity with
                              values = nextReplacementEntity } ]
                )
            else
                tryReplaceEntities (entityDefinition, otherPotentialEntities, sortedReplacementEntities)
                |> Result.map (List.append [ potentialEntity ])

    let pointersAsSingleEntityIndices (pointers: string NonEmptyList) : Result<string * int list, string> =

        let entityName = EntityInstance.extractEntityNameFromPointer pointers.Head

        if
            pointers.Tail
            |> List.exists (fun otherPointer -> EntityInstance.extractEntityNameFromPointer otherPointer <> entityName)
        then
            Result.Error "Pointers correspond with different entities, which is not supported yet!"
        else
            Result.Ok(
                entityName,
                pointers
                |> NonEmptyList.toList
                |> List.map (fun pointer -> EntityInstance.extractIndexFromPointer (entityName, pointer))
            )


    interface IDataStorage with
        member this.addEntities(entityName, entityRows) =
            result {
                let! entityDefinition =
                    getEntityByName entityName
                    |> Option.toResultWith $"'{entityName}' is not defined"

                let! unlabeledEntityRows = unlabelEntityRows entityDefinition entityRows

                if
                    unlabeledEntityRows
                    |> Seq.forall (fun entityValues ->
                        EntityInstance.valuesCorrespondWithDefinition (entityValues, entityDefinition))
                then
                    return! tryAppendEntityRowsToFileUnchecked (unlabeledEntityRows, entityDefinition)
                else
                    return!
                        Result.Error("Some values have incorrect types that don't correspond with entity definition")
            }

        member this.createEntity(entityDescription) =
            if isEntityDefined entityDescription then
                Result.Error $"'{entityDescription.name}' is already defined"
            elif String.endsWithDigit entityDescription.name then
                Result.Error $"'{entityDescription.name}' ends with a digit, which is not allowed"
            else

                lock entities (fun () ->
                    let newEntities = (entityDescription :: entities)

                    result {
                        do! updateEntityListWith newEntities
                        return! writeEntityInstances entityDescription.name []
                    })

        member this.selectEntities(entityName, maybeFilteringFunction) =
            result {
                let! entityDefinition =
                    getEntityByName entityName
                    |> Option.toResultWith $"'{entityName}' is not defined"

                let! entityInstances = readEntityInstances entityName

                let entityToLabeledValues =
                    (fun instance -> EntityInstance.getLabeledValues (instance, entityDefinition))

                return!
                    match maybeFilteringFunction with
                    | None -> Result.Ok <| List.map entityToLabeledValues entityInstances
                    | Some filteringFunction ->
                        entityInstances
                        |> List.filterResultM (fun instance ->
                            EntityInstance.expressionHolds (instance, entityDefinition, filteringFunction))
                        |> Result.map (List.map entityToLabeledValues)

            }

        member this.retrieveEntities(pointers) =
            match pointers with
            | [] -> Result.Ok([])
            | somePointer :: otherPointers ->
                let entityName = EntityInstance.extractEntityNameFromPointer somePointer

                result {
                    let! entityDefinition =
                        getEntityByName entityName
                        |> Option.toResultWith $"'{entityName}' is not defined"

                    let! entityInstances = readEntityInstances entityName

                    let entityToLabeledValues =
                        (fun instance -> EntityInstance.getLabeledValues (instance, entityDefinition))

                    return!
                        pointers
                        |> List.map (fun pointer ->
                            EntityInstance.extractIndexFromPointer (entityDefinition.name, pointer))
                        |> List.map (fun index ->
                            entityInstances
                            |> Seq.tryFind (fun instance ->
                                EntityInstance.extractIndexFromPointer (entityDefinition.name, instance.pointer) = index)
                            |> Option.toResultWith $"Cannot find element by the index {index}")
                        |> List.sequenceResultM
                        |> Result.map (List.map entityToLabeledValues)

                }

        member this.replaceEntities(pointers, replacementEntities) =
            if replacementEntities.Length <> pointers.Length then
                Result.Error "Pointer and entities count differ!"
            else
                match pointers |> NonEmptyList.tryOfList with
                | None -> Result.Ok()
                | Some somePointers ->
                    result {
                        let! (entityName, pointerIndices) = somePointers |> pointersAsSingleEntityIndices

                        let! entityDefinition =
                            (getEntityByName entityName)
                            |> Option.toResultWith $"'{entityName}' is not defined"

                        let! entityInstances = readEntityInstances entityName
                        let! unlabeledEntityRows = unlabelEntityRows entityDefinition replacementEntities

                        let replacementEntitiesSortedByIndex =
                            unlabeledEntityRows |> List.zip pointerIndices |> List.sortBy fst

                        let! updatedEntities =
                            tryReplaceEntities (entityDefinition, entityInstances, replacementEntitiesSortedByIndex)

                        return! writeEntityInstances entityName updatedEntities
                    }

        member this.getEntityDefinitions() = entities

        member this.dropEntity(entityName) =
            match (getEntityByName entityName) with
            | None -> Result.Error $"'{entityName}' is not defined"
            | Some entityDefinition ->
                lock entities (fun () ->
                    result {
                        let newEntities = entities |> List.filter (fun entity -> entity.name <> entityName)
                        do! updateEntityListWith newEntities
                        return! removeEntityInstances entityName
                    })

        member this.removeEntities(pointers) =
            match pointers |> NonEmptyList.tryOfList with
            | None -> Result.Ok()
            | Some somePointers ->

                result {
                    let! (entityName, pointerIndices) = somePointers |> pointersAsSingleEntityIndices

                    let! entityDefinition =
                        getEntityByName entityName
                        |> Option.toResultWith $"'{entityName}' is not defined"

                    let! entityInstances = readEntityInstances entityName

                    let sortedPointerIndices = pointerIndices |> List.sort

                    let! updatedEntities = tryRemoveEntities (entityDefinition, entityInstances, sortedPointerIndices)
                    return! writeEntityInstances entityName updatedEntities
                }
