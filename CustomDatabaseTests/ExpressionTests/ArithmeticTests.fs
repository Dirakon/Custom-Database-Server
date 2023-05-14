module CustomDatabaseTests.ExpressionTests.ArithmeticTests

open CustomDatabase
open CustomDatabase.Value
open NUnit.Framework
open Swensen.Unquote


let noVariables = dict []

let parseArithmetic stringExpression =
    QueryParser.parseAsContext (stringExpression, (fun parser -> parser.arithmeticExpression ()))

let parseAndEvaluateArithmetic stringExpression variables =
    parseArithmetic stringExpression
    |> Result.bind (fun context -> Expressions.tryEvaluateArithmetic (context, variables))

let parseArithmeticUnsafe = parseArithmetic >> (Result.defaultWith failwith)

let parseAndEvaluateArithmeticUnsafe stringExpression variables =
    parseAndEvaluateArithmetic stringExpression variables
    |> (Result.defaultWith failwith)

[<Test>]
let ``basic addition`` () =
    let evaluatedExpression = parseAndEvaluateArithmeticUnsafe "1 + 1" noVariables
    test <@ evaluatedExpression = Int 2 @>

[<Test>]
let ``basic subtraction`` () =
    let evaluatedExpression = parseAndEvaluateArithmeticUnsafe "1 - 1" noVariables
    test <@ evaluatedExpression = Int 0 @>

[<Test>]
let ``basic multiplication`` () =
    let evaluatedExpression = parseAndEvaluateArithmeticUnsafe "3 * 2" noVariables
    test <@ evaluatedExpression = Int 6 @>

[<Test>]
let ``basic power`` () =
    let evaluatedExpression = parseAndEvaluateArithmeticUnsafe "3^2" noVariables
    test <@ evaluatedExpression = Int 9 @>

[<Test>]
let ``basic negation`` () =
    let evaluatedExpression = parseAndEvaluateArithmeticUnsafe "-69" noVariables
    test <@ evaluatedExpression = Int -69 @>

[<Test>]
let ``multiplication takes priority`` () =
    let evaluatedExpression = parseAndEvaluateArithmeticUnsafe "2 + 2*2" noVariables
    test <@ evaluatedExpression = Int 6 @>

[<Test>]
let ``priority can be overriden with parenthesis`` () =
    let evaluatedExpression = parseAndEvaluateArithmeticUnsafe "(2 + 2)*2" noVariables
    test <@ evaluatedExpression = Int 8 @>

[<Test>]
let ``nested expressions`` () =
    let evaluatedExpression =
        parseAndEvaluateArithmeticUnsafe "(2 + (2 + (2 + 2)))" noVariables

    test <@ evaluatedExpression = Int 8 @>

[<Test>]
let ``invalid operator`` () =
    let evaluatedExpression =
        parseAndEvaluateArithmetic "(2 + (2 % (2 + 2)))" noVariables

    test <@ Result.isError evaluatedExpression @>

[<Test>]
let ``invalid types`` () =
    let evaluatedExpression = parseAndEvaluateArithmetic "2 + true" noVariables
    test <@ Result.isError evaluatedExpression @>

[<Test>]
let ``incomplete parenthesis`` () =
    let evaluatedExpression = parseAndEvaluateArithmetic "2 + (1 - 1" noVariables
    test <@ Result.isError evaluatedExpression @>

[<Test>]
let ``invalid variable`` () =
    let evaluatedExpression = parseAndEvaluateArithmetic "2 + (1 - varName)" noVariables
    test <@ Result.isError evaluatedExpression @>

[<Test>]
let ``valid variable`` () =
    let evaluatedExpression =
        parseAndEvaluateArithmeticUnsafe "2 + (1 - varName)" (dict [ "varName", Int 3 ])

    test <@ evaluatedExpression = Int 0 @>
