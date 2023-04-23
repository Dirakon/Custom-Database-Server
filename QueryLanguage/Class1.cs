using Antlr4.Runtime;
using GeneratedLanguage;

namespace QueryLanguage;
//
// public class BasicSpeakVisitor : QueryLanguageBaseVisitor<object>
// {
//     public List<QueryLanguageParser.LineContext> Lines = new();
//     public override object VisitLine(QueryLanguageParser.LineContext context)
//     {            
//         // QueryLanguageParser.NameContext name = context.name();
//         // QueryLanguageParser.OpinionContext opinion = context.opinion();
//         // SpeakLine line = new SpeakLine() { Person = name.GetText(), Text = opinion.GetText().Trim('"') };
//         // Lines.Add(line);
//         // return line;
//     }
// }
public class Class1
{
    public static void DoStuff()
    {
        AntlrInputStream inputStream = new ("john says \"hello\" \n michael says \"world\" \n");
        QueryLanguageLexer speakLexer = new (inputStream);
        CommonTokenStream commonTokenStream = new (speakLexer);
        QueryLanguageParser languageParser = new (commonTokenStream);
        QueryLanguageParser.ChatContext chatContext = languageParser.chat();
        foreach(var line in chatContext.line())
        {
            Console.WriteLine("{0} has said {1}", line.name().GetText(), line.opinion().GetText());
        }
    }
}