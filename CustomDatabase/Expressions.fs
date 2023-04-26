namespace CustomDatabase

open System.Collections.Generic
open System.Linq
open CustomDatabase.Value
open GeneratedLanguage

module Expressions =
    let toResult<'ok,'err> (errorValue:'err) (option:Option<'ok>) =
        match option with
            | Some value -> Result.Ok value
            | None -> Result.Error errorValue
    
    let rec tryEvaluateBoolean(expression:QueryLanguageParser.BooleanExpressionContext, variables: IDictionary<string, Value>):Result<bool,string> =
        if not (expression.``false``() = null) then
            Result.Ok(false)
        else if not (expression.``true``() = null) then
             Result.Ok(true)
        else if not (expression.VARNAME() = null) then
             let variableName = expression.VARNAME().GetText()
             variables
                |> Seq.tryFind (fun (KeyValue(name, value)) -> name = variableName)
                |> toResult $"Could not find the field {variableName}!"
                |> Result.bind (fun (KeyValue(name, value)) ->
                    match value with
                        | Bool booleanValue -> Result.Ok(booleanValue)
                        | _ -> Result.Error($"Cannot treat the field {variableName} as a boolean expression!")
                    )
        else if not (expression.NOT() = null) then
            // NOT token only appears when there is a single boolean expression to negate
            tryEvaluateBoolean(expression.booleanExpression()[0],variables)
                |> Result.map(not) 
        else if not (expression.booleanBinary() = null) then
            // booleanBinary token only appears when there are exactly two boolean expressions to combine
            failwith "todo"
            // do {
            //     expr1 <- tryEvaluateBoolean(expression.booleanExpression()[0],variables)
            //     expr1
            // }
        else // TODO: handle other cases, get appropriate error here
            failwith "todo"
        
    let tryEvaluateArithmetic(expression:QueryLanguageParser.ArithmeticExpressionContext, variables: IDictionary<string, Value>):Result<Value,string> =
        failwith "todo"

