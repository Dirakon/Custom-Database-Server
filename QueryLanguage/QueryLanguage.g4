grammar QueryLanguage;
/*
 * Parser Rules
 */
entityCreation      : CREATE ENTITY entityName '{' membersDeclaration '}' ;
membersDeclaration  : () | (memberDeclaration ',' membersDeclaration)| (memberDeclaration);
memberDeclaration   :  (memberName ':' type) |  (memberName ':' type '(' constraintsDeclaration ')');
constraintsDeclaration   : ()   |   (constraintDeclaration ',' constraintsDeclaration)|   (constraintDeclaration);
constraintDeclaration   : UNIQUE; // More constraints?
memberName          : VARNAME;
type                : INT | BOOL | list | STRING | FLOAT | pointer;
list                : '[' type ']';
pointer             : ('&' entityName);
entityName          : VARNAME ;

entityAddition      :  entityGroupAddition | entitySingleAddition;
entitySingleAddition      : ADD entityName jsonObj;
entityGroupAddition      : ADD '[' entityName ']' jsonArr ;

entityReplacement     :  entityGroupReplacement | entitySingleReplacement;
entitySingleReplacement     : REPLACE rawPointer jsonObj;
entityGroupReplacement     : REPLACE '[' multipleRawPointers ']' jsonArr;
multipleRawPointers   : () | (rawPointer) | (rawPointer ',' multipleRawPointers) ;
rawPointer : VARNAME;  // Maybe number?

entityRemoval    :  entityGroupRemoval | entitySingleRemoval;
entitySingleRemoval     : REMOVE rawPointer;
entityGroupRemoval     : REMOVE '[' multipleRawPointers ']';

entityRetrieval     :  (entityGroupRetrieval | entitySingleRetrieval) (WHERE booleanExpression)?;
entitySingleRetrieval     : GET entityName; 
entityGroupRetrieval     : GET '[' entityName ']';

// Maybe rename to GET and make CREATE also ADD? This way each HTTP path only corresponds with one keyword.
// However, that would make it unclear which parsing error to return. Return both? Return none? TODO: Ponder over it
entityPointerRetrieval     :  (entityPointerSingleRetrieval | entityPointerGroupRetrieval);
entityPointerSingleRetrieval     : RETRIEVE rawPointer; 
entityPointerGroupRetrieval     : RETRIEVE '[' multipleRawPointers ']';


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

//TODO: clear up the ambiguity between boolean and arithmetic when booleans are concerned
// (VARNAME, for instance can be ambigous).
// Furthermore, currently it's impossible to do any arithmetic operations with booleans which could potentially show up
// in arithmeticExpression (can this even happen in a valid query?)
// VARNAME = (VARNAME = VARNAME) is probably proccessed in a valid manner even now?
// VARNAME = (VARNAME = (VARNAME AND true)) might(?) throw error because of AND/true in arithmetic expression, 
// even though it should be valid .
// ---
// Possile (easy) fix: remove boolean type from entities.
// Possile (easy) fix: remove '=' and '!=' operators from booleans. <- sounds ok
// Possile (hard) fix: unify boolean and arithemtic expressions (a lot more checks needed then).
// ---
booleanExpression
   : arithmeticExpression arithmeticComparator arithmeticExpression
   | booleanExpression booleanBinary booleanExpression
   | '(' booleanExpression ')'
   | NOT booleanExpression
   | true
   | false
   | VARNAME
   ;
   
// lowercase needs to be specified separately because json uses lower case specifically, which
// leads to creation of a separate token for it
true : (TRUE | 'true'); 
false : (FALSE | 'false');


arithmeticExpression
   :  arithmeticExpression  power arithmeticExpression
   |  arithmeticExpression  (multplication | division)  arithmeticExpression
   |  arithmeticExpression  (plus| minus) arithmeticExpression
   |  '(' arithmeticExpression ')'
   |  (plus| minus) arithmeticExpression
   |  arithmeticAtom
   ;

arithmeticAtom
   : NUMBER
   | VARNAME
   | QUOTED_STRING
   | true
   | false
   ;

arithmeticComparator
   : equal
   | notEqual
   | gt // defined only for numeric?
   | lt // defined only for numeric?
   ;

booleanBinary
    : AND
    | OR
    |  equal
    | notEqual;

// Explicit operator naming to differentiate in the code
plus: '+';
minus: '-';
multplication: '*';
division: '/';
power: '^';
equal: '=';
notEqual: '!=';
gt: '>';
gte: '>=';
lt: '<';
lte: '<=';

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
fragment M          : ('M'|'m') ;
fragment V          : ('V'|'v') ;
fragment W          : ('W'|'w') ;
fragment H          : ('H'|'h') ;
fragment LOWERCASE  : [a-z] ;
fragment UPPERCASE  : [A-Z] ;
fragment SIGN
   : ('+' | '-')
   ;
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
fragment DIGIT                : [0-9];
   
RETRIEVE                 : R E T R I E V E;
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
REMOVE              : R E M O V E;
OR              : O R;
AND              : A N D;
NOT              : N O T;
WHERE              : W H E R E;
TRUE              : T R U E;
FALSE             : F A L S E;

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