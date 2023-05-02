namespace CustomDatabase

open System.Collections.Generic
open CustomDatabase.Value
open GeneratedLanguage


type IDataStorage =

    abstract getEntityDefinitions: unit -> Entity list

    abstract createEntity: Entity -> Result<unit, string>
    abstract addEntities: string * IDictionary<string, Value> list -> Result<string list, string>
    abstract replaceEntities: string list * IDictionary<string, Value> list -> Result<unit, string>

    abstract selectEntities:
        string * Option<QueryLanguageParser.BooleanExpressionContext> -> Result<IDictionary<string, Value> list, string>

    abstract retrieveEntities: string list -> Result<IDictionary<string, Value> list, string>
    abstract removeEntities: string list -> Result<unit, string>
    abstract dropEntity: string -> Result<unit, string>
