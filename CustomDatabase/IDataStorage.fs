namespace CustomDatabase

open System.Collections.Generic
open CustomDatabase.Value
open GeneratedLanguage


type IDataStorage =

    abstract GetEntityDefinitions: unit -> Entity list

    abstract CreateEntity: Entity -> Result<unit, string>
    abstract DropEntity: string -> Result<unit, string>

    abstract AddEntities: string * IDictionary<string, Value> list -> Result<string list, string>
    abstract ReplaceEntities: string list * IDictionary<string, Value> list -> Result<unit, string>
    abstract RemoveEntities: string list -> Result<unit, string>

    abstract SelectEntities:
        string * Option<QueryLanguageParser.BooleanExpressionContext> -> Result<IDictionary<string, Value> list, string>

    abstract RetrieveEntities: string list -> Result<IDictionary<string, Value> list, string>
