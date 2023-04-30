namespace CustomDatabase

open System.Collections.Generic
open CustomDatabase.Value
open GeneratedLanguage


type IDataStorage =
    // TODO: interface of singleton responsible for storing/retrieving data for different entities
    // entity table per file?
    // hashtable: pointer->file?
    // what files to load on get? (i.e. if only ids are needed for filtering, first choose ids, then open files)
    // entity pointer should somehow contain both entity type and entity id

    abstract createEntity: Entity -> Result<unit, string>
    abstract addEntities: string * (Value list) list -> Result<unit, string>

    abstract getEntities:
        string * Option<QueryLanguageParser.BooleanExpressionContext> -> Result<IDictionary<string, Value> list, string>
//  abstract removeEntities: Entity*(Value list) list -> Result<unit,string>
