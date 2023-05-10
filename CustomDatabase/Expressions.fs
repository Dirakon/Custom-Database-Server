namespace CustomDatabase

open System
open System.Collections.Generic
open CustomDatabase.MiscExtensions
open CustomDatabase.Antlr
open CustomDatabase.Value
open FsToolkit.ErrorHandling
open FsToolkit.ErrorHandling.Operator.Result
open GeneratedLanguage
open Microsoft.FSharp.Core

module Expressions =
    let notNull x =
        match x with
        | null -> false
        | _ -> true

    let tryParseInt (potentialInt: string) =
        try
            potentialInt |> int |> Result.Ok
        with :? FormatException ->
            Result.Error $"Cannot parse as integer: {potentialInt}"

    let tryFindVariable (variables, variableName) =
        variables
        |> Seq.tryFind (fun (KeyValue(name, value)) -> name = variableName)
        |> Option.map (fun (KeyValue(name, value)) -> value)
        |> (fun option -> option.ToResult $"Could not find the field '{variableName}'!")

    let rec valuesEqual (value1: Value, value2: Value) =
        match (value1, value2) with
        | Bool bool1, Bool bool2 -> Result.Ok(bool1 = bool2)
        | Int int1, Int int2 -> Result.Ok(int1 = int2)
        | List list1, List list2 -> listsAreEqual list1 list2
        | String str1, String str2 -> Result.Ok(str1 = str2)
        | _ -> Result.Error($"Cannot apply equality comparator to types {value1.TypeName} and {value2.TypeName}")

    and listsAreEqual (list1: Value list) (list2: Value list) =
        result {
            if List.length list1 <> List.length list2 then
                return false
            else
                let! pairwiseEqualities = list1 |> Seq.zip list2 |> Seq.map valuesEqual |> Seq.sequenceResultM
                return pairwiseEqualities |> Seq.forall id
        }

    let compareArithmetic (value1: Value, operator: QueryLanguageParser.ArithmeticComparatorContext, value2: Value) =
        match (value1, value2) with
        | value1, value2 when notNull (operator.notEqual ()) -> valuesEqual (value1, value2) |> Result.map not
        | value1, value2 when notNull (operator.equal ()) -> valuesEqual (value1, value2)
        | Int int1, Int int2 when notNull (operator.gt ()) -> Result.Ok(int1 > int2)
        | Int int1, Int int2 when notNull (operator.lt ()) -> Result.Ok(int1 < int2)
        | Int int1, Int int2 when notNull (operator.gte ()) -> Result.Ok(int1 >= int2)
        | Int int1, Int int2 when notNull (operator.lte ()) -> Result.Ok(int1 <= int2)
        | _ ->
            Result.Error(
                $"Cannot apply operator '{operator.GetTextSeparatedBySpace()}' to types {value1.TypeName} and {value2.TypeName}"
            )

    let tryExtractValueFromAtom (atom: QueryLanguageParser.ArithmeticAtomContext) variables =
        if notNull (atom.VARNAME()) then
            tryFindVariable (variables, atom.VARNAME().GetText())
        else if notNull (atom.QUOTED_STRING()) then
            let quotedString = atom.QUOTED_STRING().GetText()
            let unquotedString = quotedString.Substring(1, quotedString.Length - 2)
            Result.Ok(String unquotedString)
        else if notNull (atom.NUMBER()) then
            tryParseInt (atom.NUMBER().GetText()) |> Result.map Int
        else if notNull (atom.``true`` ()) then
            Result.Ok(Bool true)
        else if notNull (atom.``false`` ()) then
            Result.Ok(Bool false)
        else
            Result.Error $"Cannot identify arithmetic atom: '{atom.GetText()}'"

    /// Attempts to evaluate ArithmeticExpressionContext (as specified in the Custom Query Language)
    let rec tryEvaluateArithmetic
        (
            expression: QueryLanguageParser.ArithmeticExpressionContext,
            variables: IDictionary<string, Value>
        ) : Result<Value, string> =
        if notNull (expression.arithmeticAtom ()) then
            let atom = expression.arithmeticAtom ()
            tryExtractValueFromAtom atom variables
        else if
            isNull (expression.arithmeticExpression ())
            || (expression.arithmeticExpression().Length = 0)
        then
            Result.Error
                $"Cannot evaluate expression '{expression.GetTextSeparatedBySpace()}': not an arithmetic unit, and not a combination of arithmetic expressions"
        else if expression.arithmeticExpression().Length = 2 then
            result {
                let! expr1 = tryEvaluateArithmetic (expression.arithmeticExpression().[0], variables)
                let! expr2 = tryEvaluateArithmetic (expression.arithmeticExpression().[1], variables)

                return!
                    match (expr1, expr2) with
                    | Int int1, Int int2 when notNull (expression.plus ()) -> Result.Ok(Int(int1 + int2))
                    | Int int1, Int int2 when notNull (expression.power ()) ->
                        Result.Ok(Int(int (Math.Pow(int1, int2))))
                    | Int int1, Int int2 when notNull (expression.multplication ()) -> Result.Ok(Int(int1 * int2))
                    | Int int1, Int int2 when notNull (expression.minus ()) -> Result.Ok(Int(int1 - int2))
                    | Int int1, Int int2 when notNull (expression.division ()) -> Result.Ok(Int(int1 / int2))
                    | String str1, String str2 when notNull (expression.plus ()) -> Result.Ok(String(str1 + str2))
                    | _ ->
                        Result.Error
                            $"Cannot evaluate expression '{expression.GetTextSeparatedBySpace()}': operation not permitted for types {expr1.TypeName} and {expr2.TypeName}"
            }
        else if expression.arithmeticExpression().Length = 1 then
            result {
                let! (expr: Value) = tryEvaluateArithmetic (expression.arithmeticExpression().[0], variables)
                let hasPlus = notNull (expression.plus ())
                let hasMinus = notNull (expression.minus ())

                return!
                    match expr with
                    | Int int when hasPlus -> Result.Ok(Int int)
                    | _ when hasPlus -> Result.Error $"Cannot apply unary '+' to expression of type {expr.TypeName}"
                    | Int int when hasMinus -> Result.Ok(Int(-int))
                    | _ when hasMinus -> Result.Error $"Cannot apply unary '-' to expression of type {expr.TypeName}"
                    | value -> Result.Ok value
            }
        else
            Result.Error $"Cannot evaluate expression '{expression.GetTextSeparatedBySpace()}'"

    let compareBoolean (value1: bool, operator: QueryLanguageParser.BooleanBinaryContext, value2: bool) =
        if notNull (operator.OR()) then
            Result.Ok(value1 || value2)
        else if notNull (operator.AND()) then
            Result.Ok(value1 && value2)
        else if notNull (operator.equal ()) then
            Result.Ok(value1 = value2)
        else if notNull (operator.notEqual ()) then
            Result.Ok <| (value1 <> value2)
        else
            Result.Error($"Undefined binary boolean operator: '{operator.GetText()}'")

    /// Attempts to evaluate BooleanExpressionContext (as specified in the Custom Query Language)
    let rec tryEvaluateBoolean
        (
            expression: QueryLanguageParser.BooleanExpressionContext,
            variables: IDictionary<string, Value>
        ) : Result<bool, string> =
        if notNull (expression.``false`` ()) then
            Result.Ok(false)
        else if notNull (expression.``true`` ()) then
            Result.Ok(true)
        else if notNull (expression.VARNAME()) then
            let variableName = expression.VARNAME().GetText()

            tryFindVariable (variables, variableName)
            |> Result.bind (fun value ->
                match value with
                | Bool booleanValue -> Result.Ok(booleanValue)
                | _ ->
                    Result.Error(
                        $"Cannot treat the field '{variableName}' as a boolean expression (type found: {value.TypeName})!"
                    ))
        else if notNull (expression.NOT()) then
            // NOT token only appears when there is a single boolean expression to negate
            tryEvaluateBoolean (expression.booleanExpression().[0], variables)
            |> Result.map not
        else if notNull (expression.booleanBinary ()) then
            // booleanBinary token only appears when there are exactly two boolean expressions to combine
            result {
                let! expr1 = tryEvaluateBoolean (expression.booleanExpression().[0], variables)
                let! expr2 = tryEvaluateBoolean (expression.booleanExpression().[1], variables)

                return! compareBoolean (expr1, expression.booleanBinary (), expr2)
            }
        else if expression.booleanExpression().Length = 1 then
            // We have only one boolean expression (without NOT) only when it is '('+expression+')'
            tryEvaluateBoolean (expression.booleanExpression().[0], variables)
        else if notNull (expression.arithmeticComparator ()) then
            result {
                let! expr1 = tryEvaluateArithmetic (expression.arithmeticExpression().[0], variables)
                let! expr2 = tryEvaluateArithmetic (expression.arithmeticExpression().[1], variables)

                return! compareArithmetic (expr1, expression.arithmeticComparator (), expr2)
            }
        else
            Result.Error($"Cannot handle binary expression: '{expression.GetTextSeparatedBySpace()}'")
