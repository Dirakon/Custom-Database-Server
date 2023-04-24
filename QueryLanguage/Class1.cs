using Antlr4.Runtime;
using GeneratedLanguage;

namespace QueryLanguage;

public class Class1
{
    public static void DoStuff()
    {
        // AntlrInputStream inputStream = new ("john says \"hello\" \n michael says \"world\" \n");
        // QueryLanguageLexer speakLexer = new (inputStream);
        // CommonTokenStream commonTokenStream = new (speakLexer);
        // QueryLanguageParser languageParser = new (commonTokenStream);
        // QueryLanguageParser.ChatContext chatContext = languageParser.chat();
        // while (chatContext.nonEmptyChat() != null)
        // {
        //     var line = chatContext.nonEmptyChat().line();
        //     Console.WriteLine($"{line.name().GetText()} has said {line.opinion().GetText()}");
        //     chatContext = chatContext.nonEmptyChat().chat();
        // }


        AntlrInputStream inputStream = new("cReAte entity bughati{thing:string,thing2:string (unique),}");
        QueryLanguageLexer lexer = new(inputStream);
        CommonTokenStream commonTokenStream = new(lexer);
        QueryLanguageParser languageParser = new(commonTokenStream);
        var creationContext = languageParser.entityCreation();
        Console.WriteLine(
            $"{creationContext.entityName().GetText()}:{creationContext.membersDeclaration().memberDeclaration().type().GetText()}");
        Console.WriteLine(
            $"{creationContext.entityName().GetText()}:{creationContext.membersDeclaration().membersDeclaration().memberDeclaration().memberName().GetText()}");
        Console.WriteLine(
            $"{creationContext.entityName().GetText()}:{creationContext.membersDeclaration().membersDeclaration().membersDeclaration().GetText()}");
        // while (chatContext.nonEmptyChat() != null)
        // {
        //     var line = chatContext.nonEmptyChat().line();
        //     Console.WriteLine($"{line.name().GetText()} has said {line.opinion().GetText()}");
        //     chatContext = chatContext.nonEmptyChat().chat();
        // }


        // foreach(var line in chatContext)
        // {
        //     Console.WriteLine($"{line.name().GetText()} has said {line.opinion().GetText()}");
        // }
    }
}