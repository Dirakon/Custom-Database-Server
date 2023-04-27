namespace CustomDatabase


open System.Collections.Generic
open CustomDatabase.Value
open GeneratedLanguage

type ColumnConstraints = { unique: bool }

module ColumnConstraints =
    let emptyConstraints = { unique = false }


type ColumnDescription =
    { name: string
      ``type``: string
      constraints: ColumnConstraints }

// Implicit assumptions: no columns have duplicate names, no column is named "pointer"
type Entity =
    { name: string
      columns: ColumnDescription list }

type EntityInstance = { pointer: string; values: Value list }

module EntityInstance =
    let getLabeledValues (instance: EntityInstance, entityDefinition: Entity) : IDictionary<string, Value> =
        let columnNames = entityDefinition.columns |> Seq.map (fun column -> column.name)

        instance.values
        |> Seq.zip columnNames
        |> Seq.append [ ("pointer", String(instance.pointer)) ]
        |> dict

    let expressionHolds
        (
            instance: EntityInstance,
            entityDefinition: Entity,
            expression: QueryLanguageParser.BooleanExpressionContext
        ) : Result<bool, string> =
        let fields = getLabeledValues (instance, entityDefinition)
        Expressions.tryEvaluateBoolean(expression,fields)
