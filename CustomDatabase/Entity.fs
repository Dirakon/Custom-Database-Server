namespace CustomDatabase


open CustomDatabase.Value

type ColumnConstraints = {unique:bool}
module ColumnConstraints =
    let emptyConstraints = {unique=false}
    
    
type ColumnDescription = {name:string;``type``:string; constraints: ColumnConstraints}

type Entity =
    {name:string;columns:ColumnDescription list}
type EntityInstance =
    {pointer:string;values: Value list}