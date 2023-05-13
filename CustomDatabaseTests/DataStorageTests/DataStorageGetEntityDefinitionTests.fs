module CustomDatabaseTests.DataStorageTests.DataStorageGetEntityDefinitionTests


open CustomDatabase
open CustomDatabaseTests.DataStorageTests.Base
open NUnit.Framework
open Swensen.Unquote

[<TestFixture>]
type DataStorageGetEntityDefinitionTests() =
    inherit DataStorageTestSuite()

    [<Test>]
    member this.``no entity definitions on start``() =

        test <@ List.isEmpty <| this.DataStorage.GetEntityDefinitions() @>

    [<Test>]
    member this.``entity definition appears in list after addition``() =
        test <@ Result.isOk <| this.DataStorage.CreateEntity(this.TestEntity) @>
        test <@ this.DataStorage.GetEntityDefinitions() = [ this.TestEntity ] @>

    [<Test>]
    member this.``entity definition can be removed``() =
        test <@ Result.isOk <| this.DataStorage.CreateEntity(this.TestEntity) @>
        test <@ this.DataStorage.GetEntityDefinitions() = [ this.TestEntity ] @>
        test <@ Result.isOk <| this.DataStorage.DropEntity(this.TestEntity.Name) @>
        test <@ List.isEmpty <| this.DataStorage.GetEntityDefinitions() @>

    [<Test>]
    member this.``cannot remove non-existing entity``() =
        test <@ Result.isError <| this.DataStorage.DropEntity(this.TestEntity.Name) @>


    [<Test>]
    member this.``cannot add instances of empty entity``() =
        test <@ Result.isOk <| this.DataStorage.CreateEntity(this.TestEntityWithUniqueColumn) @>
