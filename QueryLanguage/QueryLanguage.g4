grammar QueryLanguage;
/*
 * Parser Rules
 */
entityCreation      : CREATE ENTITY entityName CURLY_START membersDeclaration CURLY_END ;
membersDeclaration  : () | (memberDeclaration COMA membersDeclaration)| (memberDeclaration);
memberDeclaration   :  (memberName COLON type) |  (memberName COLON type PARA_START constraintsDeclaration PARA_END);
constraintsDeclaration   : ()   |   (constraintDeclaration COMA constraintsDeclaration)|   (constraintDeclaration);
constraintDeclaration   : UNIQUE; // More constraints?
memberName          : VARNAME;
type                : INT | BOOL | list | STRING | FLOAT;
list                : SQUARE_START type SQUARE_END;
entityName          : VARNAME ;

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
fragment LOWERCASE  : [a-z] ;
fragment UPPERCASE  : [A-Z] ;
BOOL                : B O O L;
INT                : I N T ;
STRING                : S T R I N G;
FLOAT                : F L O A T;
SAYS                : S A Y S ;
CREATE              : C R E A T E ;
ENTITY              : E N T I T Y ;
UNIQUE              : U N I Q U E ;
TEXT                : '"' .*? '"' ;
CURLY_START         : '{';
CURLY_END           : '}';
SQUARE_START         : '[';
SQUARE_END           : ']';
PARA_START         : '(';
PARA_END           : ')';
COMA                : ',';
COLON                : ':';
DIGIT                : [0-9];
ALPHA                : [a-zA-Z_];
VARNAME              :ALPHA ( ALPHA | DIGIT )*;
WORD                : (LOWERCASE | UPPERCASE)+ ;
WHITESPACE          : (' '|'\t')+ -> skip ;
NEWLINE             : ('\r'? '\n' | '\r')+ ;
