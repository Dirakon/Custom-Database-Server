namespace CustomDatabase


open System.Collections.Generic
open CustomDatabase.MiscExtensions
open CustomDatabase.Value
open GeneratedLanguage

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

type EntityInstance = { Pointer: string; Values: Value list }

module EntityInstance =
    let getLabeledValues (instance: EntityInstance, entityDefinition: Entity) : IDictionary<string, Value> =
        let columnNames = entityDefinition.Columns |> Seq.map (fun column -> column.Name)

        instance.Values
        |> Seq.zip columnNames
        |> Seq.append [ ("pointer", String(instance.Pointer)) ]
        |> dict

    let valuesCorrespondWithDefinition (values: Value list, definition: Entity) : bool =
        if values.Length <> definition.Columns.Length then
            false
        else
            let columnTypes = definition.Columns |> Seq.map (fun column -> column.Type)

            values
            |> Seq.zip columnTypes
            |> Seq.forall (fun (columnType, value) -> Value.valueDescribedIsOfType value columnType)

    let correspondsWithDefinition (instance: EntityInstance, definition: Entity) : bool =
        if not (instance.Pointer.StartsWith(definition.Name)) then // TODO: use different (more efficient) pointer construction/checking logic?
            false
        else
            valuesCorrespondWithDefinition (instance.Values, definition)

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
