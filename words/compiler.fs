(create) :
smudge
!
dp latest ! ]
  (create) smudge ! dp latest ! ] ;


: widf 
    wid
    @e
    dup
    @i
    rot and
    swap
    !i
;

: immediate
    $7FFF widf
; immediate

: \
    source
    nip
    >in
    !
; immediate

\ ( "ccc<paren>" -- )
\ Compiler
\ skip everything up to the closing bracket on the same line
: (
    $29
    parse
    ddrop
; immediate


( -- )
\ make most current word compile only
: :c
    $F7FF widf
; immediate

( -- )
\ make most current word inlinned
: inlinned
    $FEFF widf
; immediate

( -- )
\ make most current word immediate and compile only
: :ic
    $77FF
    widf
; immediate

\ force compile any word including immediate words
: [compile]
  'f cxt
; :ic

( -- ) ( C: "<space>name" -- )
\ Compiler
\ what ' does in the interpreter mode, do in colon definitions
\ compiles xt as literal
: [']
    '
    [compile] lit
; :ic


( -- ) ( C: "<space>name" -- )
\ Compiler
\ what 'f does in the interpreter mode, do in colon definitions
\ and xt and flag are compiled as two literals
: ['f]
    'f
    swap
    [compile] lit
    [compile] lit
; :ic

( C:"<spaces>name" -- 0 | nfa )
\ Dictionary
\ search dictionary for name, returns nfa if found or 0 if not found
: find
    pname wid findnfa
;


: compile
  ['f] (compile) cxt
  find ,
; :ic

( -- ) ( C: x "<spaces>name" -- )
\ compiler
\ create a dictionary entry and register in word list
: rword
    (create)      ( voc-link )
    wid           ( voc-link wid )
    !e            ( )
;

( x -- ) ( C: x "<spaces>name" -- )
\ Compiler
\ create a constant in the dictionary
: con
    rword
    [compile] lit  
    [compile] ret,
;

( cchar -- ) 
\ Compiler
\ create a dictionary entry for a variable and allocate 1 cell RAM
: var
    here
    con
    2
    allot
;

( cchar -- ) 
\ Compiler
\ create a dictionary entry for a character variable and allocate 1 byte RAM
: cvar
    here
    con
    1
    allot
;

( -- a-addr ) ( C: "<spaces>name" -- )
\ Dictionary
\ create a dictionary header. XT is (constant),
\ with the address of the data field of name
: create
    rword
    \ leave address after call on tos
    compile popret
;

( n -- )  ( C: x "<spaces>name" -- )
\ Compiler
\ create a dictionary entry for a value and allocate 1 cell in EEPROM.
: val
    rword
    compile (value)
    edp                ( n edp )
    dup                ( n edp edp )
    ,                  ( n edp )
    dup                ( n edp edp )
    2+                 ( n edp edp+2)
    to edp             ( n edp )
    !e                 ( )
    ['] @e ,
    ['] !e ,
;

( -- )
\ Compiler
\ replace the XT written by CREATE to call the code that follows does>
\ does not return to caller
: (does>)
    \ change call at XT to code after (does>)
    \ get current word and then get its XT being compiled
    \ code at XT is 'call POPRET'
    \ want to change POPRET address to return address
    r>
    wid
    @e
    nfa>lfa
    2+         \ lfa>xt+1
    !i
;

( i*x -- j*y ) ( R: nest-sys1 -- ) ( C: colon-sys1 -- colon-sys2 )
\ Compiler
\ organize the XT replacement to call other colon code
: does>
    \ compile pop return to tos which is used as 'THIS' pointer
    compile (does>)
    compile r>
; :ic

( -- xt )
\ Compiler
\ create an unnamed entry in the dictionary
: :noname
    dp
    dup
    latest
    ! ]
;

( -- start ) 
\ Compiler
\ places current dictionary position for forward
\ branch resolve on TOS and advances DP
: >mark
    dp
    dp+1           \ advance DP
;

( start -- ) 
\ Compiler
\ resolve forward jump
: >resolve
    ?stack           ( start ) \ check stack integrety
    dp               ( start dest )
    rjmpc            ( )
;

( -- dest ) 
\ Compiler
\ place destination for backward branch
: <mark
    dp            ( dest )
;

( dest -- ) 
\ Compiler
\ resolve backward branch
: <resolve
    ?stack         \ make sure there is something on the stack
    \ compile a rjmp at current DP that jumps back to mark
    dp             \ ( dest start )
    swap           \ ( start dest )
    rjmpc
    dp+1           \ advance DP
;


\ Compiler
\ compile zerosense and conditional branch forward
: ?brc
    
    compile 0?       \ inline zerosense
    [compile] brnz1,
;

\ compile dupzerosense and conditional branch forward
: ??brc
    [compile] ?0?,
    [compile] brnz1,
;

( f -- ) ( C: -- orig )
\ Compiler
\ start conditional branch
\ part of: if...[else]...then
: if
   ?brc
   >mark 
; :ic

( f -- f ) ( C: -- orig )
\ Compiler
\ start conditional branch, don't consume flag
: ?if
    ??brc
    >mark 
; :ic

( C: orig1 -- orig2 ) 
\ Compiler
\ resolve the forward reference and place
\ a new unresolved forward reference
\ part of: if...else...then
: else
    >mark         \ mark forward rjmp at end of true code
    swap          \ swap new mark with previouse mark
    >resolve      \ rjmp from previous mark to false code starting here
; :ic

( -- ) ( C: orig -- ) 
\ Compiler
\ finish if
\ part of: if...[else]...then
: then
    >resolve
; :ic


( -- ) ( C: -- dest ) 
\ Compiler
\ put the destination address for the backward branch:
\ part of: begin...while...repeat, begin...until, begin...again 
: begin
    <mark
; :ic


( -- ) ( C: dest -- ) 
\ Compiler
\ compile a jump back to dest
\ part of: begin...again

: again
    <resolve
; :ic

( f -- ) ( C: dest -- orig dest ) 
\ Compiler
\ at runtime skip until repeat if non-true
\ part of: begin...while...repeat
: while
    [compile] if
    swap
; :ic

( f -- f) ( C: dest -- orig dest ) 
\ Compiler
\ at runtime skip until repeat if non-true, does not consume flag
\ part of: begin...?while...repeat
: ?while
    [compile] ?if
    swap
; :ic

( --  ) ( C: orig dest -- )
\ Compiler
\ continue execution at dest, resolve orig
: repeat
  [compile] again
  >resolve
; :ic


( f -- ) ( C: dest -- ) 
\ Compiler
\ finish begin with conditional branch,
\ leaves the loop if true flag at runtime
\ part of: begin...until
: until
    ?brc
    <resolve
; :ic

( f -- ) ( C: dest -- ) 
\ Compiler
\ finish begin with conditional branch,
\ leaves the loop if true flag at runtime
\ part of: begin...?until
: ?until
    ??brc
    <resolve
; :ic

( -- ) 
\ Compiler
\ compile the XT of the word currently
\ being defined into the dictionary
: recurse
    latest  \ ;****FIXME******
    @ $0400 cxt
; :ic

( n cchar -- ) 
\ Compiler
\ create a dictionary entry for a user variable at offset n
: user
    rword
    compile douser
    ,
;

( C: addr len -- ) 
\ String
\ compiles a string to flash
: sliteral
    compile (sliteral)     ( -- addr n)
    s,
; immediate


( -- addr len) ( C: <cchar> -- ) 
\ Compiler
\ compiles a string to flash,
\ at runtime leaves ( -- flash-addr count) on stack
: s"
    $22
    parse        ( -- addr n)
    state
    @
    if  \ skip if not in compile mode
      compile (sliteral)    ( -- addr n)
      s,
    then 
; immediate

( -- ) ( C: "ccc<quote>" -- )
\ Compiler
\ compiles string into dictionary to be printed at runtime
: ."
     s"              \ "
     compile itype
; immediate

( c<name> -- ) 
\ Compiler
\ creates a defer vector which is kept in eeprom.
: edefer
    (create)
    wid
    !e
    compile (defer)

    edp             ( -- EDP )
    dup             ( -- EDP EDP )
    ,               ( -- EDP )
    ['] @e ,
    ['] !e ,
    \ increment EDP one cell then save it
    2+              ( -- EDP+2 )
    to edp
;

( c<name> -- ) 
\ Compiler
\ creates a RAM based defer vector
: rdefer
    (create)
    wid
    !e

    compile (defer)

    here ,
    2 allot

    ['] @ ,
    ['] ! ,
;

( xt1 c<char> -- ) 
\ System
\ stores xt into defer or compiles code to do so at runtime
: is
    state @
    if
      [compile] [']
      compile defer!
      
    else
      '
      defer!
    then
; immediate

( n c<name> -- )
\ Compiler
\ add an Interrupt Service Routine to the ISR vector table
\ n is the address of the table entry
\ only need to write the address 
\ jmp instruction is already in vector table
: isr 1+ ' swap !i ;

( C: name -- )
\ Compiler
\ start defining an Interrupt Service Routine
: :isr : compile (i:) ; immediate

( -- )
\ Compiler
\ finish defining an Interrupt Service Routine
: ;isr compile (i;) [compile] ; ; :ic
