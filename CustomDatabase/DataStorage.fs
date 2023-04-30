namespace CustomDatabase


open System.IO
open System.Text.Json
open CustomDatabase.MiscExtensions
open CustomDatabase.Value
open FsToolkit.ErrorHandling
open Microsoft.AspNetCore.Hosting
open Microsoft.FSharp.Collections


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

    let appendEntityRowsToFileUnchecked (entityRows: Value list list, entityDefinition: Entity) : Result<unit, string> =
        let entityName = entityDefinition.name

        lock entityDefinition (fun () ->
            Result.fromThrowingFunction (fun () ->
                // TODO: check if maybe two two open streams (read and write) cause problems?
                use readFileStream = File.OpenRead(entityFilePath entityName)

                let previousEntityInstances: EntityInstance list =
                    JsonSerializer.Deserialize(readFileStream, jsonSerializerOptions)

                let newEntityInstances =
                    entityRowsToEntityInstances (previousEntityInstances, entityDefinition, entityRows)

                use writeFileStream = File.OpenWrite(entityFilePath entityName)

                JsonSerializer.Serialize(
                    writeFileStream,
                    newEntityInstances |> List.append previousEntityInstances,
                    jsonSerializerOptions
                )))

    interface IDataStorage with
        member this.addEntities(entityName, entityRows) =
            getEntityByName entityName
            |> Option.toResult $"'{entityName}' is not defined"
            |> Result.bind (fun entityDefinition ->
                if
                    entityRows
                    |> Seq.forall (fun entityValues ->
                        EntityInstance.valuesCorrespondWithDefinition (entityValues, entityDefinition))
                then
                    Result.Ok(entityDefinition)
                else
                    Result.Error("Some values have incorrect types that don't correspond with entity definition"))
            |> Result.bind (fun entityDefinition -> appendEntityRowsToFileUnchecked (entityRows, entityDefinition))

        member this.createEntity(entityDescription) =
            if isEntityDefined entityDescription then
                Result.Error $"'{entityDescription.name}' is already defined"
            else

                lock entities (fun () ->
                    entities <- (entityDescription :: entities)

                    Result.fromThrowingFunction (fun () ->
                        use globalFileStream = File.OpenWrite(globalEntityInfoFilePath)
                        JsonSerializer.Serialize(globalFileStream, entities, jsonSerializerOptions)

                        use localFileStream = File.OpenWrite(entityFilePath entityDescription.name)
                        JsonSerializer.Serialize(localFileStream, [], jsonSerializerOptions)))

        member this.getEntities(entityName, maybeFilteringFunction) =
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
