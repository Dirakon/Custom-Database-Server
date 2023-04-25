namespace CustomDatabase

open Microsoft.FSharp.Core


type DataStorage() =
    let mutable entities:Entity list = []
    interface IDataStorage with
        member this.addEntities(entityName, entityRows) =
            if (entities |> Seq.tryFind (fun entity -> entity.name = entityName)) = Option.None then
                //TODO: add, create file, update entities global file: all that
                lock entities (fun () -> 
                    let filename = entityName
                    
                    )
                Result.Ok ()
            else
                Result.Error $"{entityName} already defined"
        member this.createEntity(entityDescription) = failwith "todo"

