module CustomDatabaseTests.DataStorageTests.DataStorageEntityAdditionTests


open CustomDatabase
open CustomDatabaseTests.DataStorageTests.Base
open NUnit.Framework
open Swensen.Unquote

[<TestFixture>]
type DataStorageEntityAdditionTests() =
    inherit DataStorageTestSuite()

    let asSingletonListUnsafe someList =
        match someList with
        | [ singleValue ] -> singleValue
        | _ -> failwith "The given list is not singleton!"

    [<SetUp>]
    member this.``prepare entity definitions``() =
        this.DataStorage.CreateEntity(this.TestEntity) |> ignore
        this.DataStorage.CreateEntity(this.TestEntityWithUniqueColumn) |> ignore

    [<TearDown>]
    member this.``clear entity definitions``() =
        this.DataStorage.DropEntity(this.TestEntity.Name) |> ignore
        this.DataStorage.DropEntity(this.TestEntityWithUniqueColumn.Name) |> ignore

    [<Test>]
    member this.``cannot add instance of non-existent entity``() =
        test
            <@
                Result.isError
                <| this.DataStorage.AddEntities(this.NonExistentEntityName, [ this.TestEntityLabeledRow ])
            @>

    [<Test>]
    member this.``no entity instances before addition``() =
        test <@ this.DataStorage.SelectEntities(this.TestEntity.Name, None) = Ok [] @>

    [<Test>]
    member this.``entity instance appears after addition``() =
        test
            <@
                Result.isOk
                <| this.DataStorage.AddEntities(this.TestEntity.Name, [ this.TestEntityLabeledRow ])
            @>

        let selectedEntity =
            this.DataStorage.SelectEntities(this.TestEntity.Name, None)
            |> Result.defaultWith failwith
            |> asSingletonListUnsafe

        test <@ set selectedEntity.Keys = set [ "pointer"; this.TestColumn.Name ] @>
        test <@ selectedEntity[this.TestColumn.Name] = this.TestEntityLabeledRow[this.TestColumn.Name] @>

    [<Test>]
    member this.``entity instance disappears after removal``() =
        test
            <@
                Result.isOk
                <| this.DataStorage.AddEntities(this.TestEntity.Name, [ this.TestEntityLabeledRow ])
            @>

        let selectedEntity =
            this.DataStorage.SelectEntities(this.TestEntity.Name, None)
            |> Result.defaultWith failwith
            |> asSingletonListUnsafe

        test
            <@
                Result.isOk
                <| this.DataStorage.RemoveEntities([ this.GetPointerFromLabeledRowUnsafe selectedEntity ])
            @>

        test <@ this.DataStorage.SelectEntities(this.TestEntity.Name, None) = Ok [] @>

    [<Test>]
    member this.``cannot add duplicates for entity with unique columns``() =
        test
            <@
                Result.isOk
                <| this.DataStorage.AddEntities(
                    this.TestEntityWithUniqueColumn.Name,
                    [ this.TestEntityWithUniqueColumnLabeledRow ]
                )
            @>

        test
            <@
                Result.isError
                <| this.DataStorage.AddEntities(
                    this.TestEntityWithUniqueColumn.Name,
                    [ this.TestEntityWithUniqueColumnLabeledRow ]
                )
            @>

    [<Test>]
    member this.``cannot add invalid instance for entity``() =
        test
            <@
                Result.isError
                <| this.DataStorage.AddEntities(this.TestEntityWithUniqueColumn.Name, [ this.TestEntityLabeledRow ])
            @>
