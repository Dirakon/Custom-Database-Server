namespace CustomDatabase


open System.Collections.Generic
open System.IO
open System.Text.Json
open CustomDatabase.MiscExtensions
open CustomDatabase.Value
open FsToolkit.ErrorHandling
open FsToolkit.ErrorHandling.Operator.Option
open Microsoft.AspNetCore.Hosting
open Microsoft.FSharp.Collections
open FsToolkit.ErrorHandling.Operator.Result
open Microsoft.FSharp.Core



type DataStorage(webHostEnvironment: IWebHostEnvironment) =
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
            | Some lastEntity ->
                EntityInstance.extractIndexFromPointer (entityDefinition, lastEntity.pointer)
                + 1

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
        |> Seq.forall (fun uniqueValuesForAColumn -> uniqueValuesForAColumn.Count = entities.Length)
        |> not

    let tryAppendEntityRowsToFileUnchecked
        (
            entityRows: Value list list,
            entityDefinition: Entity
        ) : Result<unit, string> =
        let entityName = entityDefinition.name

        lock entityDefinition (fun () ->
            Result.fromThrowingFunction (fun () ->
                use readFileStream = File.OpenRead(entityFilePath entityName)

                let previousEntityInstances: EntityInstance list =
                    JsonSerializer.Deserialize(readFileStream, jsonSerializerOptions)

                readFileStream.Close()

                let newEntityInstances =
                    entityRowsToEntityInstances (previousEntityInstances, entityDefinition, entityRows)

                let allEntities = newEntityInstances |> List.append previousEntityInstances

                if hasDuplicateValuesOnUniqueColumns (allEntities, entityDefinition) then
                    Result.Error "Some unique column has duplicate elements!"
                else
                    File.Delete(entityFilePath entityName)
                    use writeFileStream = File.OpenWrite(entityFilePath entityName)

                    JsonSerializer.Serialize(writeFileStream, allEntities, jsonSerializerOptions)

                    Result.Ok())
            |> Result.flatten)

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
                |> Option.toResult $"Could not find a column named '{column.name}' on entity '{entityDefinition.name}'")
            |> List.sequenceResultM

    let rec tryReplaceEntities
        (
            entityDefinition: Entity,
            entityList: EntityInstance list,
            sortedReplacementEntities: (int * (Value list)) list
        ) : Result<EntityInstance list, string> =
        match entityList, sortedReplacementEntities with
        | [], (index, nextReplacementEntity) :: otherReplacementEntities ->
            Result.Error $"Could not find entity with index '{index}'"
        | entityList, [] -> Result.Ok entityList
        | potentialEntity :: otherPotentialEntities, (index, nextReplacementEntity) :: otherReplacementEntities ->
            if EntityInstance.extractIndexFromPointer (entityDefinition, potentialEntity.pointer) = index then
                tryReplaceEntities (entityDefinition, otherPotentialEntities, otherReplacementEntities)
                |> Result.map (
                    List.append
                        [ { potentialEntity with
                              values = nextReplacementEntity } ]
                )
            else
                tryReplaceEntities (entityDefinition, otherPotentialEntities, sortedReplacementEntities)
                |> Result.map (List.append [ potentialEntity ])


    interface IDataStorage with
        member this.addEntities(entityName, entityRows) =
            result {
                let! entityDefinition = getEntityByName entityName |> Option.toResult $"'{entityName}' is not defined"

                let! unlabeledEntityRows =
                    entityRows
                    |> List.map (fun labeledEntityRow -> unlabelEntityRow (labeledEntityRow, entityDefinition))
                    |> List.sequenceResultM

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
                    entities <- (entityDescription :: entities)

                    Result.fromThrowingFunction (fun () ->
                        use globalFileStream = File.OpenWrite(globalEntityInfoFilePath)
                        JsonSerializer.Serialize(globalFileStream, entities, jsonSerializerOptions)

                        use localFileStream = File.OpenWrite(entityFilePath entityDescription.name)
                        JsonSerializer.Serialize(localFileStream, [], jsonSerializerOptions)))

        member this.selectEntities(entityName, maybeFilteringFunction) =
            getEntityByName entityName
            |> Option.toResult $"'{entityName}' is not defined"
            |> Result.bind (fun entityDefinition ->
                Result.fromThrowingFunction (fun () ->
                    use readFileStream = File.OpenRead(entityFilePath entityName)

                    let entityInstances: EntityInstance list =
                        JsonSerializer.Deserialize(readFileStream, jsonSerializerOptions)

                    let entityToLabeledValues =
                        (fun instance -> EntityInstance.getLabeledValues (instance, entityDefinition))

                    match maybeFilteringFunction with
                    | None -> Result.Ok <| List.map entityToLabeledValues entityInstances
                    | Some filteringFunction ->
                        entityInstances
                        |> List.filterResultM (fun instance ->
                            EntityInstance.expressionHolds (instance, entityDefinition, filteringFunction))
                        |> Result.map (List.map entityToLabeledValues)

                ))
            |> Result.flatten

        member this.retrieveEntities(pointers) =
            match pointers with
            | [] -> Result.Ok([])
            | somePointer :: otherPointers ->
                let entityName = EntityInstance.extractEntityNameFromPointer somePointer

                result {
                    let! entityDefinition =
                        getEntityByName entityName |> Option.toResult $"'{entityName}' is not defined"

                    let entityToLabeledValues =
                        (fun instance -> EntityInstance.getLabeledValues (instance, entityDefinition))

                    return!
                        Result.fromThrowingFunction (fun () ->
                            use readFileStream = File.OpenRead(entityFilePath entityName)

                            let entityInstances: EntityInstance list =
                                JsonSerializer.Deserialize(readFileStream, jsonSerializerOptions)

                            pointers
                            |> List.map (fun pointer ->
                                EntityInstance.extractIndexFromPointer (entityDefinition, pointer))
                            |> List.map (fun index ->
                                entityInstances
                                |> Seq.tryItem index
                                |> Option.toResult $"Cannot find element by the index {index}")
                            |> List.sequenceResultM
                            |> Result.map (List.map entityToLabeledValues)

                        )
                        |> Result.flatten
                }

        member this.replaceEntities(pointers, entitiesToAdd) =
            if entitiesToAdd.Length <> pointers.Length then
                Result.Error "Pointer and entities count differ!"
            else
                match pointers with
                | [] -> Result.Ok()
                | somePointer :: otherPointers ->
                    let entityName = EntityInstance.extractEntityNameFromPointer somePointer

                    Result.fromThrowingFunction (fun () ->
                        result {
                            let! entityDefinition =
                                getEntityByName entityName |> Option.toResult $"'{entityName}' is not defined"

                            let! unlabeledEntityRows =
                                entitiesToAdd
                                |> List.map (fun labeledEntityRow ->
                                    unlabelEntityRow (labeledEntityRow, entityDefinition))
                                |> List.sequenceResultM

                            use readFileStream = File.OpenRead(entityFilePath entityName)

                            let entityInstances: EntityInstance list =
                                JsonSerializer.Deserialize(readFileStream, jsonSerializerOptions)

                            readFileStream.Close()

                            let pointerIndices =
                                pointers
                                |> List.map (fun pointer ->
                                    EntityInstance.extractIndexFromPointer (entityDefinition, pointer))

                            let replacementEntitiesSortedByIndex =
                                unlabeledEntityRows |> List.zip pointerIndices |> List.sortBy fst

                            let! updatedEntities =
                                tryReplaceEntities (
                                    entityDefinition,
                                    entityInstances,
                                    replacementEntitiesSortedByIndex
                                )

                            File.Delete(entityFilePath entityName)
                            use writeFileStream = File.OpenWrite(entityFilePath entityName)
                            JsonSerializer.Serialize(writeFileStream, updatedEntities, jsonSerializerOptions)
                        })
                    |> Result.flatten
