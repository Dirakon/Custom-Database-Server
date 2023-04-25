namespace CustomDatabase.ThrowErrorListener

type ThrowingErrorListener<'a>() =
    interface Antlr4.Runtime.IAntlrErrorListener<'a> with
        member this.SyntaxError(output, recognizer, offendingSymbol, line, charPositionInLine, msg, e) = failwith msg
