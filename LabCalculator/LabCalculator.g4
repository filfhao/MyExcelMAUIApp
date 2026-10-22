grammar LabCalculator;

/*
 * Граматика виразів лабораторної роботи 1, варіант 44:
 *   операції 1 (+ - * /), 4 (^), 6 (max, min), 8 (= < >), 9 (<= >= <>).
 * Вираз у клітинці записується після знака «=» (сам знак «=» до граматики не входить).
 * Унарних + та - у варіанті немає.
 */

/*
 * Parser Rules
 */

compileUnit : formula EOF ;

// Логічний вираз: порівняння двох арифметичних виразів.
// Частина з порівнянням необов'язкова, щоб повідомити про «вираз не є логічним» зрозумілішим текстом.
formula : left=expression ( op=(EQ | NEQ | LT | GT | LE | GE) right=expression )? ;

// Арифметичний вираз. Пріоритет визначається порядком альтернатив (раніше — вищий).
expression
    : LPAREN expression RPAREN                                  #ParenthesizedExpr
    | <assoc=right> expression POW expression                   #ExponentialExpr
    | expression op=(MUL | DIV) expression                      #MultiplicativeExpr
    | expression op=(ADD | SUB) expression                      #AdditiveExpr
    | func=(MAX | MIN) LPAREN expression COMMA expression RPAREN #FunctionExpr
    | NUMBER                                                    #NumberExpr
    | CELL                                                      #IdentifierExpr
    ;

/*
 * Lexer Rules
 */

MAX : [Mm][Aa][Xx] ;
MIN : [Mm][Ii][Nn] ;

CELL   : [a-zA-Z]+ [0-9]+ ;            // ім'я клітинки: A1, B12, AA3
NAME   : [a-zA-Z_] [a-zA-Z_0-9]* ;     // інші імена (для повідомлення «невідоме ім'я»)
NUMBER : [0-9]+ ;                      // цілі числа довільної довжини

ADD    : '+' ;
SUB    : '-' ;
MUL    : '*' ;
DIV    : '/' ;
POW    : '^' ;
LPAREN : '(' ;
RPAREN : ')' ;
COMMA  : ',' ;

EQ  : '=' ;
NEQ : '<>' ;
LE  : '<=' ;
GE  : '>=' ;
LT  : '<' ;
GT  : '>' ;

WS : [ \t\r\n]+ -> skip ;

// Будь-який інший символ — окрема лексема, щоб помилку виявляв і локалізував парсер
ERROR_CHAR : . ;
