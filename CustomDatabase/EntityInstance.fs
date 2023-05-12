namespace CustomDatabase

open System.Collections.Generic
open CustomDatabase.MiscExtensions
open CustomDatabase.Value
open FSharpPlus
open FsToolkit.ErrorHandling
open GeneratedLanguage


type EntityInstance = { Pointer: string; Values: Value list }

type UnlabeledEntityRow = Value list

type LabeledEntityRow = IDictionary<string, Value>


module UnlabeledEntityRow =
    let correspondsWithDefinition (definition: Entity) (values: UnlabeledEntityRow) : bool =
        if values.Length <> definition.Columns.Length then
            false
        else
            let columnTypes = definition.Columns |> Seq.map (fun column -> column.Type)

            values
            |> Seq.zip columnTypes
            |> Seq.forall (fun (columnType, value) -> Value.valueDescribedIsOfType value columnType)

module EntityInstance =
    let asLabeledRowKnowingDefinition (entityDefinition: Entity) (instance: EntityInstance) : LabeledEntityRow =
        let columnNames = entityDefinition.Columns |> Seq.map (fun column -> column.Name)

        instance.Values
        |> Seq.zip columnNames
        |> Seq.append [ ("pointer", String(instance.Pointer)) ]
        |> dict

    let correspondsWithDefinition (definition: Entity) (instance: EntityInstance) : bool =
        if not (instance.Pointer.StartsWith(definition.Name)) then // TODO: use different (more efficient) pointer construction/checking logic?
            false
        else
            UnlabeledEntityRow.correspondsWithDefinition definition instance.Values



    let expressionHolds
        (
            instance: EntityInstance,
            entityDefinition: Entity,
            expression: QueryLanguageParser.BooleanExpressionContext
        ) : Result<bool, string> =
        let fields = asLabeledRowKnowingDefinition entityDefinition instance
        Expressions.tryEvaluateBoolean (expression, fields)

    let fromEntityRows
        (
            previousEntityInstances: seq<EntityInstance>,
            entityDefinition: Entity,
            entityRows: UnlabeledEntityRow list
        ) : EntityInstance list =
        let entityName = entityDefinition.Name

        let upperBoundOnPointerIndex =
            match Seq.tryLast previousEntityInstances with
            | None -> 1
            | Some lastEntity -> 1 + Pointer.toIndexKnowingEntityName entityName lastEntity.Pointer

        let getNthNewPointer index =
            entityName + string (index + upperBoundOnPointerIndex)

        entityRows
        |> List.mapi (fun index row ->
            { Pointer = getNthNewPointer index
              Values = row })


module LabeledEntityRow =

    let unlabelSingleKnowingDefinition
        (entityDefinition: Entity)
        (entityRow: LabeledEntityRow)
        : Result<Value list, string> =
        if entityRow.Count <> entityDefinition.Columns.Length then
            Result.Error
                $"Number of fields is not correct! Expected {entityDefinition.Columns.Length}, received {entityRow.Count}"
        else
            entityDefinition.Columns
            |> List.map (fun column ->
                entityRow
                |> Seq.tryFind (fun (KeyValue(name, value)) -> name = column.Name)
                |> Option.map (fun (KeyValue(name, value)) -> value)
                |> Option.toResultWith
                    $"Could not find a column named '{column.Name}' on entity '{entityDefinition.Name}'")
            |> List.sequenceResultM

    let unlabelMultipleKnowingDefinition entityDefinition entityRows =
        entityRows
        |> List.map (unlabelSingleKnowingDefinition entityDefinition)
        |> List.sequenceResultM
