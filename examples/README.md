# EBNF Library Examples

This directory contains practical examples demonstrating the capabilities of the Ebnf_lib library. Each example shows how to define EBNF grammars and parse real-world input.

## Examples

### 1. Hexadecimal Number Parser
**File:** `HexNumberExample.cs`

Demonstrates basic EBNF grammar definition and parsing. This example parses hexadecimal numbers in the format `0xABCD`.

**What it shows:**
- Defining simple EBNF rules (digit, hex, main)
- Parsing user input against a grammar
- Error handling and meaningful error messages
- Converting parsed results to useful data (BigInteger)

**Usage:**
```csharp
HexNumberExample.Run();
```

**Example:**
```
Enter a hexadecimal number: /> 0xFF
✓ Parsed successfully!
  Hexadecimal: 0xFF
  Decimal:     255

Enter a hexadecimal number: /> 0x10A
✓ Parsed successfully!
  Hexadecimal: 0x10A
  Decimal:     266

Enter a hexadecimal number: /> 0x
✗ Parsing failed!
  Error: Parsing-process failed at...
```

### Key Concepts Demonstrated
- **Character Classes:** `[0-9A-Fa-f]` matches hexadecimal digits
- **Repetition:** `digit+` means one or more digits (required)
- **Sequences:** `"0x" hex` matches literal "0x" followed by hex digits
- **Error Handling:** Graceful error reporting with context information

---

### 2. Mathematical Expression Calculator
**File:** `CalculatorExample.cs`

Demonstrates operator precedence, recursive parsing, and result computation. Evaluates expressions like `2 + 3 * (4 - 1)`.

**What it shows:**
- Operator precedence: multiplication/division before addition/subtraction
- Recursive expression parsing
- Using post-processing to compute results during parsing
- Handling nested parentheses

**Usage:**
```csharp
CalculatorExample.Run();
```

**Example:**
```
Enter an expression: /> 2 + 3 * 4
✓ Result: 14

Enter an expression: /> (2 + 3) * 4
✓ Result: 20

Enter an expression: /> 10 / 2 - 3
✓ Result: 2
```

### Key Concepts Demonstrated
- **Operator Precedence:** expr1 (+ -) calls expr2 (* /) calls expr3 (parens/numbers)
- **Post-processing:** Each level computes its results before returning to the parent
- **Choice:** Multiple alternatives (`expr3 ::= bracket | number`)
- **Recursion:** Expressions can contain other expressions via parentheses

---

### 3. JSON-Like Object Parser
**File:** `JsonExample.cs`

Demonstrates complex nested structures, lambda expressions for parameterized rules, and data validation. Parses objects, arrays, strings, booleans, and null values.

**What it shows:**
- Defining complex recursive grammars (objects containing objects, arrays, etc.)
- Lambda expressions (csl for comma-separated lists)
- Converting parsed results to .NET types (Dictionary, Array, etc.)
- Data validation (detecting duplicate keys)
- Handling optional elements

**Usage:**
```csharp
JsonExample.Run();
```

**Example:**
```
Enter JSON: /> {"name": "Simon", "age": 21}
✓ Parsed successfully!
  Type: Dictionary`2
  Value: Object{2 properties}

Enter JSON: /> [1, 2, 3]
✓ Parsed successfully!
  Type: Object[]
  Value: Array[3]

Enter JSON: /> {"key": "value", "key": "duplicate"}
✗ Parsing failed!
  Error: Duplicate key "key" in object.
```

### Key Concepts Demonstrated
- **Lambda Expressions:** `csl(arg) ::= arg ( s* ',' s* arg )*` defines reusable pattern
- **Nested Rules:** Objects can contain arrays, which can contain objects, etc.
- **Choice (Alternatives):** `simple ::= string | null | bool`
- **Optional Elements:** `csl(...)?` makes the list optional
- **Post-processing:** Data transformation from parse tree to .NET objects

---

## Running All Examples

Each example can be run independently:
```csharp
HexNumberExample.Run();
CalculatorExample.Run();
JsonExample.Run();
```

You can also use the menu entry point in `Program.cs`:
```csharp
Program.Main();
```

Menu options:
- `1` Hex Number Example
- `2` Calculator Example
- `3` JSON Example
- `q` Quit

## Related Files

- **Library Documentation:** See `Ebnf_lib/EbnfSyntax-v1.md` for complete EBNF syntax reference
- **Main README:** See `../README.md` for library overview and installation
