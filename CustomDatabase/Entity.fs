namespace CustomDatabase


open System.Collections.Generic
open CustomDatabase.MiscExtensions
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

    let valuesCorrespondWithDefinition (values: Value list, definition: Entity) : bool =
        if values.Length <> definition.columns.Length then
            false
        else
            let columnTypes = definition.columns |> Seq.map (fun column -> column.``type``)

            values
            |> Seq.zip columnTypes
            |> Seq.forall (fun (columnType, value) -> Value.valueDescribedIsOfType value columnType)

    let correspondsWithDefinition (instance: EntityInstance, definition: Entity) : bool =
        if not (instance.pointer.StartsWith(definition.name)) then // TODO: use different (more efficient) pointer construction/checking logic?
            false
        else
            valuesCorrespondWithDefinition (instance.values, definition)

    let extractIndexFromPointer (entityName: string, pointer: string) =
        int (pointer.Substring(entityName.Length))

    let rec extractEntityNameFromPointer (pointer: string) =
        if pointer |> String.endsWithDigit then
            extractEntityNameFromPointer (pointer.Substring(0, pointer.Length - 1))
        else
            pointer

    let expressionHolds
        (
            instance: EntityInstance,
            entityDefinition: Entity,
            expression: QueryLanguageParser.BooleanExpressionContext
        ) : Result<bool, string> =
        let fields = getLabeledValues (instance, entityDefinition)
        Expressions.tryEvaluateBoolean (expression, fields)
