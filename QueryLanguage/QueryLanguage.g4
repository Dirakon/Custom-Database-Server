grammar QueryLanguage;
//@parser::members {
//  @Override
//  public void reportError(RecognitionException e) {
//    throw new RuntimeException("I quit!\n" + e.getMessage()); 
//  }
//}
//
//@lexer::members {
//  @Override
//  public void reportError(RecognitionException e) {
//    throw new RuntimeException("I quit!\n" + e.getMessage()); 
//  }
//}
/*
 * Parser Rules
 */
entityCreation      : CREATE ENTITY entityName '{' membersDeclaration '}' ;
membersDeclaration  : () | (memberDeclaration ',' membersDeclaration)| (memberDeclaration);
memberDeclaration   :  (memberName ':' type) |  (memberName ':' type '(' constraintsDeclaration ')');
constraintsDeclaration   : ()   |   (constraintDeclaration ',' constraintsDeclaration)|   (constraintDeclaration);
constraintDeclaration   : UNIQUE; // More constraints?
memberName          : VARNAME;
type                : INT | BOOL | list | STRING | FLOAT | ('&' entityName);
list                : '[' type ']';
entityName          : VARNAME ;

entityAddition      :  entityGroupAddition | entitySingleAddition;
entitySingleAddition      : ADD entityName jsonObj;
entityGroupAddition      : ADD '[' entityName ']' jsonArr ;

entityReplacement     :  entityGroupReplacement | entitySingleReplacement;
entitySingleReplacement     : REPLACE raw_pointer jsonObj;
entityGroupReplacement     : REPLACE '[' multiple_raw_pointers ']' jsonArr;
multiple_raw_pointers   : () | (raw_pointer) | (raw_pointer ',' multiple_raw_pointers) ;
raw_pointer : VARNAME;

entityRetrieval     :  entityGroupRetrieval | entitySingleRetrieval;
entitySingleRetrieval     : GET entityName; // TODO: filters?
entityGroupRetrieval     : GET '[' entityName ']';

jsonObj
   : '{' jsonPair (',' jsonPair)* '}'
   | '{' '}'
   ;

jsonPair
   : QUOTED_STRING ':' jsonValue
   ;

jsonArr
   : '[' jsonValue (',' jsonValue)* ']'
   | '[' ']'
   ;

jsonValue
   : QUOTED_STRING
   | NUMBER
   | jsonObj
   | jsonArr
   | 'true'
   | 'false'
   | 'null'
   ;

/*
 * Lexer Rules
 */
fragment I          : ('I'|'i') ;
fragment N          : ('N'|'n') ;
fragment T          : ('T'|'t') ;
fragment E          : ('E'|'e') ;
fragment R          : ('R'|'r') ;
fragment C          : ('C'|'c') ;
fragment A          : ('A'|'a') ;
fragment S          : ('S'|'s') ;
fragment Y          : ('Y'|'y') ;
fragment G          : ('G'|'g') ;
fragment O          : ('O'|'o') ;
fragment L          : ('L'|'l') ;
fragment B          : ('B'|'b') ;
fragment F          : ('F'|'f') ;
fragment U          : ('U'|'u') ;
fragment Q          : ('Q'|'q') ;
fragment D          : ('D'|'d') ;
fragment P          : ('P'|'p') ;
fragment LOWERCASE  : [a-z] ;
fragment UPPERCASE  : [A-Z] ;

fragment ESC
   : '\\' (["\\/bfnrt] | UNICODE)
   ;
fragment UNICODE
   : 'u' HEX HEX HEX HEX
   ;
fragment HEX
   : [0-9a-fA-F]
   ;
fragment SAFECODEPOINT
   : ~ ["\\\u0000-\u001F]
   ;

fragment EXP
   // exponent number permits leading 0s (e.g. `1e01`)
   : [Ee] [+\-]? [0-9]+
   ;

fragment INTEGER_PART
   // integer part forbis leading 0s (e.g. `01`)
   : '0' | [1-9] DIGIT*
   ;
   
GET                 : G E T;
REPLACE             : R E P L A C E;
BOOL                : B O O L;
ADD                : A D D;
INT                : I N T ;
STRING                : S T R I N G;
FLOAT                : F L O A T;
SAYS                : S A Y S ;
CREATE              : C R E A T E ;
ENTITY              : E N T I T Y ;
UNIQUE              : U N I Q U E ;
DIGIT                : [0-9];

QUOTED_STRING
   : '"' (ESC | SAFECODEPOINT)* '"'
   ;


NUMBER
   : '-'? INTEGER_PART ('.' DIGIT +)? EXP?
   ;


ALPHA                : [a-zA-Z_];
VARNAME              :ALPHA ( ALPHA | DIGIT )*;
WORD                : (LOWERCASE | UPPERCASE)+ ;

WS
   : [ \t\n\r] + -> skip
   ;
   
WHITESPACE          : (' '|'\t')+ -> skip ;