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

    let correspondsWithDefinition (instance: EntityInstance, definition: Entity) : bool =
        if not (instance.pointer.StartsWith(definition.name)) then // TODO: use different (more efficient) pointer construction/checking logic?
            false
        else if instance.values.Length <> definition.columns.Length then
            false
        else
            let columnTypes = definition.columns |> Seq.map (fun column -> column.``type``)

            instance.values
            |> Seq.map (fun value -> value.TypeName)
            |> Seq.zip columnTypes
            |> Seq.forall (fun (columnType, actualType) -> columnType = actualType)

    let extractIndexFromPointer (entityDefinition: Entity, pointer: string) =
        int (pointer.Substring(entityDefinition.name.Length))

    let expressionHolds
        (
            instance: EntityInstance,
            entityDefinition: Entity,
            expression: QueryLanguageParser.BooleanExpressionContext
        ) : Result<bool, string> =
        let fields = getLabeledValues (instance, entityDefinition)
        Expressions.tryEvaluateBoolean (expression, fields)
