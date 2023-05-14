module CustomDatabaseTests.DataStorageTests.ReplaceEntitiesTest


open System.Linq
open CustomDatabase
open CustomDatabase.Value
open CustomDatabaseTests.DataStorageTests.Base
open FSharpPlus
open NUnit.Framework
open Swensen.Unquote

[<TestFixture>]
type ReplaceEntitiesTest() =
    inherit DataStorageTestSuite()


    [<SetUp>]
    member this.``prepare entity definitions``() =
        this.DataStorage.CreateEntity(this.TestEntity) |> ignore
        this.DataStorage.CreateEntity(this.TestEntityWithUniqueColumn) |> ignore

        this.DataStorage.AddEntities(this.TestEntity.Name, [ this.TestEntityLabeledRow1 ])
        |> ignore

        this.DataStorage.AddEntities(
            this.TestEntityWithUniqueColumn.Name,
            [ this.TestEntityWithUniqueColumnLabeledRow1 ]
        )
        |> ignore

    [<TearDown>]
    member this.``clear entity definitions``() =
        this.DataStorage.DropEntity(this.TestEntity.Name) |> ignore
        this.DataStorage.DropEntity(this.TestEntityWithUniqueColumn.Name) |> ignore

    [<Test>]
    member this.``cannot replace by invalid pointer``() =
        test
            <@
                Result.isError
                <| this.DataStorage.ReplaceEntities([ this.TestEntity.Name + "1234" ], [ this.TestEntityLabeledRow1 ])
            @>

    [<Test>]
    member this.``cannot replace with pointer to non-existent entity definition``() =
        test
            <@
                Result.isError
                <| this.DataStorage.ReplaceEntities(
                    [ this.NonExistentEntityName + "1234" ],
                    [ this.TestEntityLabeledRow1 ]
                )
            @>

    [<Test>]
    member this.``cannot replace on wrong entity definition``() =
        test
            <@
                Result.isError
                <| this.DataStorage.ReplaceEntities(
                    [ this.NonExistentEntityName + "1234" ],
                    [ this.TestEntityLabeledRow1 ]
                )
            @>

    [<Test>]
    member this.``update does not modify pointer``() =
        let initialLabeledRow = this.GetSingularEntityUnsafeByName this.TestEntity.Name
        let pointer = this.GetPointerFromLabeledRowUnsafe initialLabeledRow

        test
            <@
                Result.isOk
                <| this.DataStorage.ReplaceEntities([ pointer ], [ this.TestEntityLabeledRow2 ])
            @>

        let expectedEntity =
            this.TestEntityLabeledRow2
            |> Dict.unionWith (fun a b -> a) (dict [ "pointer", String pointer ])

        test <@ Enumerable.SequenceEqual(this.GetSingularEntityUnsafeByName this.TestEntity.Name, expectedEntity) @>


    [<Test>]
    member this.``cannot replace to ruin unique constraint``() =
        let initialLabeledRow =
            this.GetSingularEntityUnsafeByName this.TestEntityWithUniqueColumn.Name

        let pointer = this.GetPointerFromLabeledRowUnsafe initialLabeledRow

        test
            <@
                Result.isOk
                <| this.DataStorage.AddEntities(
                    this.TestEntityWithUniqueColumn.Name,
                    [ this.TestEntityWithUniqueColumnLabeledRow2 ]
                )
            @>

        test
            <@
                Result.isError
                <| this.DataStorage.ReplaceEntities([ pointer ], [ this.TestEntityLabeledRow2 ])
            @>
