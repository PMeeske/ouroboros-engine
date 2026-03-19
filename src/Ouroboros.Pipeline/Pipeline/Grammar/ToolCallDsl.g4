// ToolCallDsl.g4 — Reference grammar for LLM tool call intent parsing.
//
// This grammar documents the formats recognized by McpToolCallParser.
// It is NOT compiled via ANTLR code generation; instead, McpToolCallParser
// implements an equivalent hand-written parser for zero build-tool overhead.
//
// Supported formats:
//   1. XML tag:    <tool_call>{"name":"x","arguments":{...}}</tool_call>
//   2. JSON array: {"tool_calls":[{"function":{"name":"x","arguments":{...}}}]}
//   3. Bracket:    [TOOL:name args]
//   4. Markdown function block: ```tool_call\n{"name":"x",...}\n```
//
// The parser extracts ToolCallIntent records from mixed LLM text output.

grammar ToolCallDsl;

// Entry: mixed text with zero or more embedded tool calls
toolOutput
    : (textSegment | toolCall)* EOF
    ;

toolCall
    : xmlToolCall
    | jsonToolCallArray
    | bracketToolCall
    | markdownToolCall
    ;

// Format 1: <tool_call>JSON</tool_call>  (Mistral, Qwen, Hermes)
xmlToolCall
    : XML_TOOL_CALL_OPEN jsonObject XML_TOOL_CALL_CLOSE
    ;

// Format 2: {"tool_calls":[...]}  (OpenAI-compatible)
jsonToolCallArray
    : '{' '"tool_calls"' ':' '[' jsonToolCallItem (',' jsonToolCallItem)* ']' '}'
    ;

jsonToolCallItem
    : '{' '"function"' ':' '{' jsonPair (',' jsonPair)* '}' (',' jsonPair)* '}'
    | '{' jsonPair (',' jsonPair)* '}'
    ;

// Format 3: [TOOL:name args]  (Ouroboros legacy)
bracketToolCall
    : '[' 'TOOL:' IDENTIFIER WS? bracketArgs? ']'
    ;

bracketArgs
    : (~']')+
    ;

// Format 4: ```tool_call\nJSON\n```  (Markdown code block)
markdownToolCall
    : '```tool_call' NEWLINE jsonObject NEWLINE '```'
    ;

// Standard JSON subset (sufficient for tool call payloads)
jsonObject
    : '{' jsonPair (',' jsonPair)* '}'
    | '{' '}'
    ;

jsonPair
    : STRING ':' jsonValue
    ;

jsonArray
    : '[' jsonValue (',' jsonValue)* ']'
    | '[' ']'
    ;

jsonValue
    : STRING
    | NUMBER
    | jsonObject
    | jsonArray
    | 'true'
    | 'false'
    | 'null'
    ;

// Everything that is not a tool call
textSegment
    : TEXT_CHAR+
    ;

// Lexer rules
XML_TOOL_CALL_OPEN  : '<tool_call>' ;
XML_TOOL_CALL_CLOSE : '</tool_call>' ;

STRING
    : '"' (~["\\\r\n] | '\\' .)* '"'
    ;

NUMBER
    : '-'? [0-9]+ ('.' [0-9]+)? ([eE] [+-]? [0-9]+)?
    ;

IDENTIFIER
    : [a-zA-Z_] [a-zA-Z0-9_.-]*
    ;

NEWLINE : '\r'? '\n' ;
WS      : [ \t]+ -> skip ;

TEXT_CHAR
    : ~[<[{]
    ;
