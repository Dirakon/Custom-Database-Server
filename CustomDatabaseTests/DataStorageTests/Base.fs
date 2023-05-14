module CustomDatabaseTests.DataStorageTests.Base


open System.Collections.Generic
open System.IO
open CustomDatabase
open CustomDatabase.Value
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Logging.Abstractions
open NUnit.Framework


type MockedWebHostEnvironment() =
    interface IWebHostEnvironment with
        member this.ApplicationName = "ApplicationName"
        member this.ContentRootFileProvider = failwith "ContentRootFileProvider not supported"
        member this.ContentRootPath = "testData"
        member this.EnvironmentName = "EnvironmentName"
        member this.WebRootFileProvider = failwith "WebRootFileProvider not supported"
        member this.WebRootPath = "testData"

        member this.ApplicationName
            with set _ = failwith "Setting ApplicationName is not supported"

        member this.ContentRootFileProvider
            with set _ = failwith "Setting ContentRootFileProvider is not supported"

        member this.ContentRootPath
            with set _ = failwith "Setting ContentRootPath is not supported"

        member this.EnvironmentName
            with set _ = failwith "Setting EnvironmentName is not supported"

        member this.WebRootFileProvider
            with set _ = failwith "Setting WebRootFileProvider is not supported"

        member this.WebRootPath
            with set _ = failwith "Setting WebRootPath is not supported"

let mockedWebHostEnvironment: IWebHostEnvironment = MockedWebHostEnvironment()

let getNewDataStorage () =
    DataStorage(mockedWebHostEnvironment, NullLogger<DataStorage>.Instance)

type DataStorageTestSuite() =

    let mutable _dataStorage: IDataStorage = getNewDataStorage ()
    member this.DataStorage = _dataStorage
    member this.NonExistentEntityName = "don_exist"

    member this.AsSingletonListUnsafe someList =
        match someList with
        | [ singleValue ] -> singleValue
        | _ -> failwith "The given list is not singleton!"

    member this.GetSingularEntityUnsafeByName entityName =
        this.DataStorage.SelectEntities(entityName, None)
        |> Result.defaultWith failwith
        |> this.AsSingletonListUnsafe

    member this.GetPointerFromLabeledRowUnsafe(row: IDictionary<string, Value>) =
        match row["pointer"] with
        | String s -> s
        | _ -> failwith "Inconsistent row! Pointer is not string."

    member this.TestColumn: ColumnDescription =
        { Name = "column_name"
          Type = "string"
          Constraints = ColumnConstraints.emptyConstraints }

    member this.TestEntity: Entity =
        { Name = "test_entity"
          Columns = [ this.TestColumn ] }

    member this.TestEntityLabeledRow1 = dict [ this.TestColumn.Name, String "value" ]
    member this.TestEntityLabeledRow2 = dict [ this.TestColumn.Name, String "value2" ]

    member this.TestUniqueColumn: ColumnDescription =
        { Name = "column_name_unique"
          Type = "string"
          Constraints =
            { ColumnConstraints.emptyConstraints with
                Unique = true } }

    member this.TestEntityWithUniqueColumn: Entity =
        { Name = "test_entity_with_unique"
          Columns = [ this.TestUniqueColumn ] }

    member this.TestEntityWithUniqueColumnLabeledRow1 =
        dict [ this.TestUniqueColumn.Name, String "value" ]

    member this.TestEntityWithUniqueColumnLabeledRow2 =
        dict [ this.TestUniqueColumn.Name, String "value2" ]

    member this.UnusedValue = String "some unused value"

    [<SetUp>]
    member _.``prepare data storage``() = _dataStorage <- getNewDataStorage ()

    [<TearDown>]
    member _.``clear state``() =
        let dataFolderPath = Path.Join(mockedWebHostEnvironment.ContentRootPath, "data")

        Directory.Delete(dataFolderPath, true)
