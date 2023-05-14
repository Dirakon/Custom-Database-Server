module CustomDatabaseTests.DataStorageTests.AddEntitiesTests


open CustomDatabase
open CustomDatabaseTests.DataStorageTests.Base
open NUnit.Framework
open Swensen.Unquote

[<TestFixture>]
type AddEntitiesTests() =
    inherit DataStorageTestSuite()

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
                <| this.DataStorage.AddEntities(this.NonExistentEntityName, [ this.TestEntityLabeledRow1 ])
            @>

    [<Test>]
    member this.``no entity instances before addition``() =
        test <@ this.DataStorage.SelectEntities(this.TestEntity.Name, None) = Ok [] @>

    [<Test>]
    member this.``entity instance appears after addition``() =
        test
            <@
                Result.isOk
                <| this.DataStorage.AddEntities(this.TestEntity.Name, [ this.TestEntityLabeledRow1 ])
            @>

        let selectedEntity = this.GetSingularEntityUnsafeByName this.TestEntity.Name

        test <@ set selectedEntity.Keys = set [ "pointer"; this.TestColumn.Name ] @>
        test <@ selectedEntity[this.TestColumn.Name] = this.TestEntityLabeledRow1[this.TestColumn.Name] @>

    [<Test>]
    member this.``entity instance disappears after removal``() =
        test
            <@
                Result.isOk
                <| this.DataStorage.AddEntities(this.TestEntity.Name, [ this.TestEntityLabeledRow1 ])
            @>

        let selectedEntity = this.GetSingularEntityUnsafeByName this.TestEntity.Name

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
                    [ this.TestEntityWithUniqueColumnLabeledRow1 ]
                )
            @>

        test
            <@
                Result.isError
                <| this.DataStorage.AddEntities(
                    this.TestEntityWithUniqueColumn.Name,
                    [ this.TestEntityWithUniqueColumnLabeledRow1 ]
                )
            @>

    [<Test>]
    member this.``cannot add invalid instance for entity``() =
        test
            <@
                Result.isError
                <| this.DataStorage.AddEntities(this.TestEntityWithUniqueColumn.Name, [ this.TestEntityLabeledRow1 ])
            @>
