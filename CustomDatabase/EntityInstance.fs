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


    // Scan entity instances for duplicate values on columns with 'unique' constraint,
    // assuming that entity instances are valid
    // (valid here meaning: 1. same amount of values on each instance, 2. correct types on values)
    let hasDuplicateValuesOnUniqueColumnsUnchecked (entities: EntityInstance list, entityDefinition: Entity) : bool =
        let indicesOfUniqueColumns =
            entityDefinition.Columns
            |> Seq.mapi (fun index column -> (index, column))
            |> Seq.filter (fun (index, column) -> column.Constraints.Unique)
            |> Seq.map fst

        let allUniqueValuesForEachUniqueColumn =
            indicesOfUniqueColumns
            |> Seq.map (fun uniqueColumnIndex ->
                HashSet(
                    entities
                    |> Seq.map (fun entity -> entity.Values.Item(uniqueColumnIndex).AsIdentifyingString)
                ))

        allUniqueValuesForEachUniqueColumn
        |> Seq.exists (fun uniqueValuesForAColumn -> uniqueValuesForAColumn.Count <> entities.Length)

    let rec tryRemoveEntities
        (
            entityDefinition: Entity,
            entityList: EntityInstance list,
            removalIndices: int list
        ) : Result<EntityInstance list, string> =
        match entityList, removalIndices with
        | [], (unusedIndex :: _) -> Result.Error $"Could not find entity with index '{unusedIndex}'"
        | entities, [] -> Result.Ok entities
        | potentialEntity :: otherPotentialEntities, index :: otherRemovalIndices ->
            if Pointer.toIndexKnowingEntityName entityDefinition.Name potentialEntity.Pointer = index then
                tryRemoveEntities (entityDefinition, otherPotentialEntities, otherRemovalIndices)
            else
                tryRemoveEntities (entityDefinition, otherPotentialEntities, removalIndices)
                |> Result.map (List.append [ potentialEntity ])

    let rec tryReplaceEntities
        (
            entityDefinition: Entity,
            entityList: EntityInstance list,
            sortedReplacementEntities: (int * Value list) list
        ) : Result<EntityInstance list, string> =
        match entityList, sortedReplacementEntities with
        | [], (index, nextReplacementEntity) :: otherReplacementEntities ->
            Result.Error $"Could not find entity with index '{index}'"
        | entityList, [] -> Result.Ok entityList
        | potentialEntity :: otherPotentialEntities, (index, nextReplacementEntity) :: otherReplacementEntities ->
            if Pointer.toIndexKnowingEntityName entityDefinition.Name potentialEntity.Pointer = index then
                tryReplaceEntities (entityDefinition, otherPotentialEntities, otherReplacementEntities)
                |> Result.map (
                    List.append
                        [ { potentialEntity with
                              Values = nextReplacementEntity } ]
                )
            else
                tryReplaceEntities (entityDefinition, otherPotentialEntities, sortedReplacementEntities)
                |> Result.map (List.append [ potentialEntity ])

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
