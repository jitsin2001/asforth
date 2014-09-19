\ words for timer 
\ GTCCR – General Timer/Counter Control Register
\ Timer/counter 0 : 8 bit
\ TCCR0A  $24 ($44) Timer/Counter Control Register A
\ TCCR0B  $25 ($45) Timer/Counter Control Register B
\ TCNT0   $26 ($46) Timer/Counter 
\ OCR0A   $27 ($47) Output Compare Register A
\ OCR0B   $28 ($48) Output Compare Register B
\ TIFR0   $15 ($35) Timer/Counter 0 Interrupt Flag Register
\ TIMSK0  ($6E) Timer/Counter Interrupt Mask Register

only I/O
vocabulary Timer
also Timer definitions


\ number of milliseconds since last clear
var ms
\ number of microseconds accumulated
var us
\ ISR for counting ms ticks generated by Timer 0
\ each tick is 1.024 ms
\ 42 ticks of 24 us is 1.008 ms
:isr T0ms
  ms 1+! us @ 24 +
  \ check if microsecond accumulator is > 1000
  dup 999 >
  if
    \ add extra millisecond
    ms 1+!
    \ remove 1000 microseconds from accumulator
    1000 -
  then
  us !
;isr

\ Timer 0 clock select
( n -- )
\ n is a value between 0 and 7
\ 0 - no clock source
\ 1 - no prescaling - clkio
\ 2 - clkio/8
\ 3 - clkio/64
\ 4 - clkio/256
\ 5 - clkio/1024
\ 6 - external clock T0 pin, falling edge
\ 7 - external clock T0 pin, rising edge

\ : T0clk

\ ;

\ set T0ms as interrupt routine for timer 0 overflow
OVF0 isr T0ms

\ setup timer 0 for ~1ms timer counter overflow interrupt
( -- )
: T0init
  0 dup ms ! us !
\ use prescaler of 64
\ timer 0 will generate an overflow event 976.5625 times/sec
  %011 TCCR0B c!
\ setup timer in normal count mode and normal port mode
  %0 TCCR0A c!
\ clear overflow flag by setting the flag
  %1 TIFR0 c!
\ enable timer overflow interupt
  %1 TIMSK0 c!
;
