namespace CustomDatabase


open System.IO
open System.Text.Json
open Microsoft.AspNetCore.Hosting


type DataStorage(webHostEnvironment: IWebHostEnvironment) =
    let dataFolderPath = Path.Join(webHostEnvironment.ContentRootPath, "data")

    let globalEntityInfoFilePath = Path.Join(dataFolderPath, "entity.json")

    let entityFilePath (entityName: string) =
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

    let getEntityByName (entityName: string) =
        entities |> Seq.tryFind (fun entity -> entity.name = entityName)

    let entityDefined (entityDescription: Entity) =
        (getEntityByName entityDescription.name).IsSome

    interface IDataStorage with
        member this.addEntities(entityName, entityRows) =
            match getEntityByName entityName with
            | Some entityDefinition ->
                lock entityDefinition (fun () ->
                    // TODO: check if maybe two there are two open streams (read and write) causing problems?
                    use readFileStream = File.OpenRead(entityFilePath entityName)

                    let thisEntities: EntityInstance list =
                        JsonSerializer.Deserialize(readFileStream, jsonSerializerOptions)

                    let upperBoundOnPointerIndex =
                        match Seq.tryLast thisEntities with
                        | None -> 1
                        | Some lastEntity ->
                            EntityInstance.extractIndexFromPointer (entityDefinition, lastEntity.pointer)
                            + 1

                    let entityPointers =
                        entityRows
                        |> List.mapi (fun index _ -> entityName + string (index + upperBoundOnPointerIndex))

                    let entityInstances =
                        entityRows
                        |> List.zip entityPointers
                        |> List.map (fun (pointer, row) -> { pointer = pointer; values = row })

                    if
                        entityInstances
                        |> Seq.forall (fun instance ->
                            EntityInstance.correspondsWithDefinition (instance, entityDefinition))
                    then
                        use writeFileStream = File.OpenWrite(entityFilePath entityName)

                        JsonSerializer.Serialize(
                            writeFileStream,
                            thisEntities |> List.append entityInstances,
                            jsonSerializerOptions
                        )

                        Result.Ok()
                    else
                        Result.Error "Some values have incorrect types that don't correspond with entity definition")
            | None -> Result.Error $"'{entityName}' is not defined"

        member this.createEntity(entityDescription) =
            if not (entityDefined entityDescription) then

                lock entities (fun () ->
                    entities <- (entityDescription :: entities)

                    use globalFileStream = File.OpenWrite(globalEntityInfoFilePath)
                    JsonSerializer.Serialize(globalFileStream, entities, jsonSerializerOptions)

                    use localFileStream = File.OpenWrite(entityFilePath entityDescription.name)
                    JsonSerializer.Serialize(localFileStream, [], jsonSerializerOptions))

                Result.Ok()
            else
                Result.Error $"'{entityDescription.name}' is already defined"
