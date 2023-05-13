module CustomDatabaseTests.BooleanExpressionTests


open CustomDatabase
open CustomDatabase.Value
open NUnit.Framework
open Swensen.Unquote


let noVariables = dict []

let parseBoolean stringExpression =
    QueryParser.parseAsContext (stringExpression, (fun parser -> parser.booleanExpression ()))

let parseAndEvaluateBoolean stringExpression variables =
    parseBoolean stringExpression
    |> Result.bind (fun context -> Expressions.tryEvaluateBoolean (context, variables))

let parseBooleanUnsafe = parseBoolean >> (Result.defaultWith failwith)

let parseAndEvaluateBooleanUnsafe stringExpression variables =
    parseAndEvaluateBoolean stringExpression variables
    |> (Result.defaultWith failwith)

[<Test>]
let ``basic equality`` () =
    let evaluatedExpression = parseAndEvaluateBooleanUnsafe "1 = 1" noVariables
    test <@ evaluatedExpression @>

[<Test>]
let ``basic inequality`` () =
    let evaluatedExpression = parseAndEvaluateBooleanUnsafe "1 != 1" noVariables
    test <@ not evaluatedExpression @>

[<Test>]
let ``basic gt`` () =
    let evaluatedExpression = parseAndEvaluateBooleanUnsafe "1 > 2" noVariables
    test <@ not evaluatedExpression @>

[<Test>]
let ``basic lt`` () =
    let evaluatedExpression = parseAndEvaluateBooleanUnsafe "1 < 2" noVariables
    test <@ evaluatedExpression @>

[<Test>]
let ``basic gte`` () =
    let evaluatedExpression = parseAndEvaluateBooleanUnsafe "1 >= 1" noVariables
    test <@ evaluatedExpression @>

[<Test>]
let ``basic lte`` () =
    let evaluatedExpression = parseAndEvaluateBooleanUnsafe "1 <= 2" noVariables
    test <@ evaluatedExpression @>

[<Test>]
let ``basic and`` () =
    let evaluatedExpression =
        parseAndEvaluateBooleanUnsafe "true and (2 = 2)" noVariables

    test <@ evaluatedExpression @>

[<Test>]
let ``basic or`` () =
    let evaluatedExpression =
        parseAndEvaluateBooleanUnsafe "false or (1 <= 2)" noVariables

    test <@ evaluatedExpression @>

[<Test>]
let ``nested expressions dictate order`` () =
    let evaluatedExpression =
        parseAndEvaluateBooleanUnsafe "true or (false and false)" noVariables

    test <@ evaluatedExpression @>

[<Test>]
let ``invalid operator`` () =
    let evaluatedExpression =
        parseAndEvaluateBoolean "true or (false xor false)" noVariables

    test <@ Result.isError evaluatedExpression @>

[<Test>]
let ``invalid variable`` () =
    let evaluatedExpression =
        parseAndEvaluateBoolean "true or (false and varName)" noVariables

    test <@ Result.isError evaluatedExpression @>

[<Test>]
let ``invalid type`` () =
    let evaluatedExpression =
        parseAndEvaluateBoolean "1 or (false and true)" noVariables

    test <@ Result.isError evaluatedExpression @>

[<Test>]
let ``valid variable`` () =
    let evaluatedExpression =
        parseAndEvaluateBooleanUnsafe "true or (false and varName)" (dict [ "varName", Bool false ])

    test <@ evaluatedExpression @>
