grammar QueryLanguage;
/*
 * Parser Rules
 */
 
someQuery : entityCreation | entityAddition | entityReplacement | entityRemoval | entitySelection | entityRetrieval | entityDropping;
 
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

entityDropping      : DROP ENTITY entityName;

entityAddition      :  ADD '[' entityName ']' jsonArr;

entityReplacement     :  REPLACE '[' multipleRawPointers ']' jsonArr;
multipleRawPointers   : () | (rawPointer) | (rawPointer ',' multipleRawPointers) ;
rawPointer : VARNAME;  // Maybe number?

entityRemoval    :  REMOVE '[' multipleRawPointers ']';

entitySelection     :   GET '[' entityName ']' (WHERE booleanExpression)?;

entityRetrieval     :   RETRIEVE '[' multipleRawPointers ']';


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
// in arithmeticExpression (can this even happen in a valid query? Give an example)
// VARNAME = (VARNAME = VARNAME) is probably proccessed in a valid manner even now?
// VARNAME = (VARNAME = (VARNAME AND true)) might(?) throw error because of AND/true in arithmetic expression, 
// even though it should be valid.
// UPD: both things above work perfectly fine. Potentlially there could still be some edge-cases that don't work?
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
   // defined only for numeric
   | gt 
   | lt 
   | gte 
   | lte
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
   // integer part forbids leading 0s (e.g. `01`)
   : '0' | [1-9] DIGIT*
   ;
fragment DIGIT                : [0-9];
   
RETRIEVE                 : R E T R I E V E;
GET                 : G E T;
DROP                 : D R O P ;
REPLACE             : R E P L A C E;
BOOL                : B O O L;
ADD                : A D D;
INT                : I N T ;
STRING                : S T R I N G;
FLOAT                : F L O A T;
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