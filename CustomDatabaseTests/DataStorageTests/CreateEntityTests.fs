module CustomDatabaseTests.DataStorageTests.CreateEntityTests



open CustomDatabaseTests.DataStorageTests.Base
open NUnit.Framework
open Swensen.Unquote


[<TestFixture>]
type CreateEntityTests() =
    inherit DataStorageTestSuite()

    [<Test>]
    member this.``cannot create duplicate entities``() =
        test <@ Result.isOk <| this.DataStorage.CreateEntity(this.TestEntity) @>
        test <@ Result.isError <| this.DataStorage.CreateEntity(this.TestEntity) @>
