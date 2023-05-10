namespace CustomDatabase

open System.Collections.Generic
open CustomDatabase.Value
open GeneratedLanguage


type IDataStorage =

    abstract GetEntityDefinitions: unit -> Entity list

    /// Attempt to create new entity definition and file from the given entity description
    abstract CreateEntity: Entity -> Result<unit, string>

    /// Attempt to remove the entity definition and file based on the entity name
    abstract DropEntity: string -> Result<unit, string>

    /// Attempt to add to the specified entity file the given labeled entity rows
    abstract AddEntities: string * IDictionary<string, Value> list -> Result<string list, string>

    /// Attempt to replace entity instances specified by a given list of pointers
    /// (assuming that all pointers relate to a single entity file)
    /// with new given labeled entity rows, without changing the pointers
    abstract ReplaceEntities: string list * IDictionary<string, Value> list -> Result<unit, string>

    /// Attempt to remove the entity definition and file based on the entity name
    abstract RemoveEntities: string list -> Result<unit, string>

    /// Attempt to choose entity instances from a file related to the given entity name based on some filtering function
    abstract SelectEntities:
        string * Option<QueryLanguageParser.BooleanExpressionContext> -> Result<IDictionary<string, Value> list, string>

    /// Attempt to retrieve entity instances given a list of pointers
    /// (assuming that all pointers relate to a single entity file)
    abstract RetrieveEntities: string list -> Result<IDictionary<string, Value> list, string>
