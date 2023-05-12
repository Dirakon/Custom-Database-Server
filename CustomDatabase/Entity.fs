namespace CustomDatabase



type ColumnConstraints = { Unique: bool }

module ColumnConstraints =
    let emptyConstraints = { Unique = false }


type ColumnDescription =
    { Name: string
      Type: string
      Constraints: ColumnConstraints }

// Implicit assumptions: no columns have duplicate names, no column is named "pointer"
type Entity =
    { Name: string
      Columns: ColumnDescription list }
