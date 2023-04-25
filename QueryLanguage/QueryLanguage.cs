using Antlr4.Runtime;
using GeneratedLanguage;

namespace QueryLanguage;

public static class QueryLanguage
{
    public static QueryLanguageParser GetParser(string input)
    {
        AntlrInputStream inputStream = new(input);
        QueryLanguageLexer lexer = new(inputStream);
        CommonTokenStream commonTokenStream = new(lexer);
        return new QueryLanguageParser(commonTokenStream);
    }

}