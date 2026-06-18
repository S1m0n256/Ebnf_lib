static void HexNumberExample()
{
    Ebnf.Parser parser = new("main");
    parser.AddEbnfCode("digit ::= [0-9A-F] hex ::= digit+ main ::= \"0x\" hex");
    while (true)
    {
        Console.Write("Enter a hexadecimal number (or 'exit' to quit): \n/> ");
        string? input = Console.ReadLine();
        if (input == null || input.Trim().Equals("exit", StringComparison.CurrentCultureIgnoreCase))
            break;
        var result = parser.Parse(input);
        if (!result.HasError)
        {
            string hexDigits = ((Ebnf.Result.Sequence)result).Results[1].StringSection.Section;
            if (
                BigInteger.TryParse(
                    hexDigits,
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture,
                    out BigInteger number
                )
            )
            {
                Console.WriteLine($"Parsed successfully! Decimal value: {number}");
            }
            else
            {
                Console.WriteLine($"Failed to parse hexadecimal number: {hexDigits}");
            }
        }
        else
        {
            Console.WriteLine($"Error: {result.ErrorMessage ?? "unknown error"}");
            Console.WriteLine($"Context: {result.GetContext()}");
        }
        Console.WriteLine();
    }
}
static void CalculatorExample()
{
    CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    Ebnf.Parser parser = new("expr");
    parser.AddExpression("s", Ebnf.Expression.WhiteSpace);
    parser.AddExpression("number", Ebnf.Expression.Float_double);
    parser.AddEbnfCode(
        "expr1 ::= expr2 ( s* [+-] s* expr2 )*",
        "expr2 ::= expr3 ( s* [*/] s* expr3 )*",
        "expr3 ::= ( '(' s* expr s* ')' )/bracket | number",
        "expr ::= s* expr1 s*"
    );
    parser.AddSuccessProcess(
        "expr1",
        new Ebnf.SuccessProcess(
            (result, cursor) =>
            {
                // expr2 ( s* [+-] s* expr2 )*
                var seq = ((Ebnf.Result.Sequence)result).Results;
                double value = (double)seq[0].Data!;
                // ( s* [+-] s* expr2 )*
                seq = ((Ebnf.Result.Sequence)seq[1]).Results;
                foreach (var item in seq)
                {
                    // s* [+-] s* expr2
                    var itemSeq = ((Ebnf.Result.Sequence)item).Results;
                    char op = itemSeq[1].StringSection.Section[0];
                    double nextValue = (double)itemSeq[3].Data!;
                    value = op switch
                    {
                        '+' => value + nextValue,
                        '-' => value - nextValue,
                        _ => throw new InvalidOperationException($"Unsupported operator: {op}"),
                    };
                }
                result.Data = value;
            }
        )
    );
    parser.AddSuccessProcess(
        "expr2",
        new Ebnf.SuccessProcess(
            (result, cursor) =>
            {
                // expr3 ( s* [*/] s* expr3 )*
                var seq = ((Ebnf.Result.Sequence)result).Results;
                double value = (double)seq[0].Data!;
                // ( s* [*/] s* expr3 )*
                seq = ((Ebnf.Result.Sequence)seq[1]).Results;
                foreach (var item in seq)
                {
                    // s* [*/] s* expr3
                    var itemSeq = ((Ebnf.Result.Sequence)item).Results;
                    char op = itemSeq[1].StringSection.Section[0];
                    double nextValue = (double)itemSeq[3].Data!;
                    value = op switch
                    {
                        '*' => value * nextValue,
                        '/' => value / nextValue,
                        _ => throw new InvalidOperationException($"Unsupported operator: {op}"),
                    };
                }
                result.Data = value;
            }
        )
    );
    parser.AddSuccessProcess(
        "bracket",
        new Ebnf.SuccessProcess(
            (result, cursor) =>
            {
                // '(' s* expr s* ')'
                var seq = ((Ebnf.Result.Sequence)result).Results;
                result.Data = seq[2].Data!;
            }
        )
    );
    parser.AddSuccessProcess(
        "expr",
        new Ebnf.SuccessProcess(
            (result, cursor) =>
            {
                // s* expr1 s*
                var seq = ((Ebnf.Result.Sequence)result).Results;
                result.Data = seq[1].Data!;
            }
        )
    );

    while (true)
    {
        Console.Write("Enter an expression to evaluate (or 'exit' to quit): \n/> ");
        string? input = Console.ReadLine();
        if (input == null || input.Trim().Equals("exit", StringComparison.CurrentCultureIgnoreCase))
            break;
        var result = parser.Parse(input);
        if (!result.HasError)
        {
            Console.WriteLine($"Result: {result.Data}");
        }
        else
        {
            Console.WriteLine($"Error: {result.ErrorMessage ?? "unknown error"}");
            Console.WriteLine($"Context: {result.GetContext()}");
        }
        Console.WriteLine();
    }
}
static void GeneralTest()
{
    Ebnf.Expression expr = new Ebnf.Expression.Terminal("");
    Ebnf.Parser parser = new("main");
    parser.AddExpression("string", Ebnf.Expression.String);
    parser.AddExpression("s", Ebnf.Expression.WhiteSpace);
    parser.AddExpression("e", Ebnf.Expression.EndOfString);
    parser.AddEbnfCode("test ::= string e", "newExpr ::= '-' ~[]*", "main ::= test | newExpr");
    parser.AddSuccessProcess(
        "test",
        new Ebnf.SuccessProcess(
            (result, cursor) =>
            {
                // string e
                var seq = ((Ebnf.Result.Sequence)result).Results;
                string s = (string)seq[0].Data!;
                Action exe = () =>
                {
                    var result = expr.Parse(s);
                    if (result.HasError)
                    {
                        Console.WriteLine(
                            $"Parsing failed: {result.ErrorMessage ?? "unknown error"}"
                        );
                        Console.WriteLine($"Context: {result.GetContext()}");
                    }
                    else
                    {
                        Console.WriteLine($"Parsed Successfully: {result}");
                    }
                };
                result.Data = exe;
            }
        )
    );
    parser.AddSuccessProcess(
        "newExpr",
        new Ebnf.SuccessProcess(
            (result, cursor) =>
            {
                // '-' ~[]*
                var seq = ((Ebnf.Result.Sequence)result).Results;
                // ~[]*
                string code = seq[^1].StringSection.Section.Trim();
                try
                {
                    var newExpr = Ebnf.Expression.FromEbnf(code);
                    expr = newExpr;
                }
                catch (Exception ex)
                {
                    result.ErrorMessage = $"Failed to parse EBNF expression: {ex.Message}";
                    return;
                }
            }
        )
    );
    while (true)
    {
        Console.WriteLine($"main ::= {expr}");
        Console.Write("/> ");
        string? input = Console.ReadLine();
        if (input is null)
            continue;
        var result = parser.Parse(input);
        if (result.HasError)
        {
            Console.WriteLine($"Error: {result.ErrorMessage ?? "unknown"}");
            Console.WriteLine($"Context: {result.GetContext()}");
        }
        else
        {
            var exe = (Action?)result.Data!;
            exe?.Invoke();
        }
        Console.WriteLine();
    }
}
static void FuncTesting()
{
    Ebnf.Parser parser = new("main");
    //parser.AddEbnfCode("main ::= ((p = [-+], d = [0-9]) --> p? d+)([*/])");
    //parser.AddEbnfCode("main ::= (f --> nat = [0-9]+; f(nat))(n --> s = ' '; n ( s* ',' s* n )*)");
    //parser.AddEbnfCode("main ::= ((f = x --> x \"..\" x) --> nat = [0-9]+; f(nat))()");
    //parser.AddEbnfCode("main ::= (x --> (y --> (x | y)+)('b'))('a')");
    //parser.AddEbnfCode("main ::= (f --> f('1'))(t --> ((x, y) --> (x | y)+)('0', t))");
    //parser.AddEbnfCode("main ::= ( op --> (f --> f('1'))(t --> op('0', t)) )((x, y) --> (x | y)+)");
    //parser.AddEbnfCode("main ::= op = (x, y) --> (x | y)+; (f --> f('1'))(t --> op('0', t))");

    //parser.AddEbnfCode("main ::= f = () --> '(' f()* ')'; f()");
    //parser.AddEbnfCode("main ::= x = '(' x* ')'; x");
    //This is not possible: A local variable cannot refer to itself in its own definition.
    //To achieve recursion, you need to define the variable globally.
    //parser.AddEbnfCode("f ::= () --> '(' f()* ')'");
    //parser.AddEbnfCode("main ::= f()");
    //parser.AddEbnfCode("x ::= '(' x* ')'");
    //parser.AddEbnfCode("main ::= x");
    //parser.AddEbnfCode("main ::= a = '0', b = '1'; (a | b)+");
    //parser.AddEbnfCode("main ::= a = '0'; b = '1'; (a | b)+");

    //This is a special case: Since local variables are only connected at the start,
    //when the lambda-expression is build, the folowing problem arises:
    //The variable a refers to the parameter a of the lambda-expression.
    //When the function f is called insed its own definition, the parameter a gets the expression "a b" assigned,
    //which refers to itself, thus an endless-loop is created.
    //That's the reason why arguments should not be assigned to themself in the function body.
    parser.AddExpression("e", Ebnf.Expression.EndOfString);
    parser.AddEbnfCode("f(a, b) ::= a b e | checkLength(a,b) f(a b, b a)");
    parser.AddEbnfCode("main ::= f('0', '1')");
    static int getLength(Ebnf.Expression expr) =>
        expr switch
        {
            Ebnf.Expression.Terminal t => t.Value.Length,
            Ebnf.Expression.TerminalChar _ => 1,
            Ebnf.Expression.CharacterClass _ => 1,
            Ebnf.Expression.Sequence seq => seq.Expressions.Sum(getLength),
            Ebnf.Expression.VariableReference varRef => getLength(varRef.Variable.Expression!),
            _ => throw new InvalidOperationException(
                $"Unsupported expression type: {expr.GetType()}"
            ),
        };
    Ebnf.Expression checkLength = null!;
    parser.AddExpression(
        "checkLength",
        checkLength = new Ebnf.Expression(
            (cursor, args) =>
            {
                // checkLength(a, b)
                var a = args![0];
                var b = args![1];
                return new Ebnf.Result(checkLength, cursor.CreateEmptySection())
                {
                    Success = (getLength(a) + getLength(b)) * 2 <= cursor.Source.Length,
                };
            }
        )
    );

    while (true)
    {
        Console.Write("Enter a string to parse (or 'exit' to quit): \n/> ");
        string? input = Console.ReadLine();
        if (input == null || input.Trim().Equals("exit", StringComparison.CurrentCultureIgnoreCase))
            break;
        var result = parser.Parse(input);
        if (!result.HasError)
        {
            Console.WriteLine("Parsed successfully!");
            Console.WriteLine($"Result: {result}");
        }
        else
        {
            Console.WriteLine("Parsing failed.");
            Console.WriteLine($"Error: {result.ErrorMessage ?? "unknown"}");
        }
        Console.WriteLine();
    }
}
static void JsonExample()
{
    Ebnf.Parser parser = new();
    parser.AddExpression("s", Ebnf.Expression.WhiteSpace);
    parser.AddExpression("nat", Ebnf.Expression.Nat_int);
    parser.AddExpression("int", Ebnf.Expression.Int_int);
    parser.AddExpression("float", Ebnf.Expression.Float);
    parser.AddExpression("openChar", Ebnf.Expression.OpenChar);
    parser.AddExpression("char", Ebnf.Expression.Char);
    parser.AddExpression(
        "string",
        new Ebnf.Expression.Choice(Ebnf.Expression.String, Ebnf.Expression.SingleQuotedString)
    );
    parser.AddEbnfCode(
        "null ::= \"null\"",
        "bool ::= \"true\"/true | \"false\"/false",
        "simple ::= string | float | null | bool",
        "csl(arg) ::= arg ( s* ',' s* arg )*",
        "array ::= '[' s* csl( obj )?/opt s* ']'",
        "dict ::= '{' s* csl( ( string s* ':' s* obj )/pair )?/opt s* '}'",
        "obj ::= simple | array | dict",
        "main ::= s* obj s*"
    );
    parser.MainVariable = "main";
    parser.AddSuccessProcess(
        "main",
        new Ebnf.SuccessProcess(
            (result, cursor) =>
            {
                if (!cursor.EndOfString)
                {
                    result.Success = false;
                    result.ErrorMessage =
                        $"Unexpected character '{cursor.CurrentChar}' at position {cursor.Position}.\n"
                        + $"Context: {result.GetContext()}";
                }
                else
                {
                    // s* obj s*
                    var seq = ((Ebnf.Result.Sequence)result).Results;
                    result.Data = seq[1].Data!;
                }
            }
        )
    );
    parser.AddSuccessProcess(
        "int",
        new Ebnf.SuccessProcess(
            (result, cursor) =>
            {
                if (!cursor.EndOfString && cursor.CurrentChar == '.')
                    result.Success = false;
            }
        )
    );
    parser.AddSuccessProcess(
        "float",
        new Ebnf.SuccessProcess(
            (result, cursor) =>
            {
                string s = (string)result.Data!;
                int i = s[0] is '-' or '+' ? 1 : 0;
                bool b;
                if (b = s[i..].All(char.IsAsciiDigit))
                {
                    b = result.Success = int.TryParse(s, out int num);
                    result.Data = num;
                }
                if (!b)
                {
                    result.Success = double.TryParse(
                        s,
                        CultureInfo.InvariantCulture,
                        out double num
                    );
                    result.Data = num;
                }
            }
        )
    );
    //data = List<object?>
    parser.AddSuccessProcess(
        "opt",
        new Ebnf.SuccessProcess(
            (result, cursor) =>
            {
                // csl(...)?
                var seq = ((Ebnf.Result.Sequence)result).Results;
                result.Data = seq.Count == 0 ? new List<object?>() : seq[0].Data!;
            }
        )
    );
    //data = true
    parser.AddSuccessProcess(
        "true",
        new Ebnf.SuccessProcess((result, cursor) => result.Data = true)
    );
    //data = false
    parser.AddSuccessProcess(
        "false",
        new Ebnf.SuccessProcess((result, cursor) => result.Data = false)
    );
    //data = (string key, object value)
    parser.AddSuccessProcess(
        "pair",
        new Ebnf.SuccessProcess(
            (result, cursor) =>
            {
                // string s* ':' s* obj
                var seq = ((Ebnf.Result.Sequence)result).Results;
                string key = (string)seq[0].Data!;
                object value = seq[4].Data!;
                result.Data = (key, value);
            }
        )
    );
    //data = List<object?>
    parser.AddSuccessProcess(
        "csl",
        new Ebnf.SuccessProcess(
            (result, cursor) =>
            {
                // arg ( s* ',' s* arg )*
                var seq = ((Ebnf.Result.Sequence)result).Results;
                List<object?> args = [seq[0].Data];
                // ( s* ',' s* arg )*
                seq = ((Ebnf.Result.Sequence)seq[1]).Results;
                foreach (var item in seq)
                {
                    // s* ',' s* arg
                    var itemSeq = ((Ebnf.Result.Sequence)item).Results;
                    args.Add(itemSeq[3].Data);
                }
                result.Data = args;
            }
        )
    );
    //data = object[]
    parser.AddSuccessProcess(
        "array",
        new Ebnf.SuccessProcess(
            (result, cursor) =>
            {
                // '[' s* csl( obj ) s* ']'
                var seq = ((Ebnf.Result.Sequence)result).Results;
                List<object?> items = (List<object?>)seq[2].Data!;
                result.Data = items.ToArray();
            }
        )
    );
    //data = Dictionary<string, object>
    parser.AddSuccessProcess(
        "dict",
        new Ebnf.SuccessProcess(
            (result, cursor) =>
            {
                // '{' s* csl( ( string s* ':' s* obj )/pair ) s* '}'
                // '{' s* object[] s* '}'
                var seq = ((Ebnf.Result.Sequence)result).Results;
                IEnumerable<(string key, object value)> pairs = ((List<object?>)seq[2].Data!).Cast<(
                    string key,
                    object value
                )>();
                Dictionary<string, object> dict = [];
                foreach (var (key, value) in pairs)
                {
                    if (dict.ContainsKey(key))
                    {
                        result.ErrorMessage = $"Duplicate key \"{key}\" in dictionary.";
                        return;
                    }
                    dict[key] = value;
                }
                result.Data = dict;
            }
        )
    );

    static string stringify(object? obj) =>
        obj switch
        {
            null => "null",
            bool b => b ? "true" : "false",
            int i => i.ToString(),
            double x => x.ToString(CultureInfo.InvariantCulture),
            string s => $"\"{StrHelper.StringAsString(s)}\"",
            object[] arr => "[" + string.Join(", ", arr.Select(stringify)) + "]",
            Dictionary<string, object> dict => dict.Count == 0
                ? "{}"
                : "{ "
                    + string.Join(
                        ", ",
                        dict.Select(kv => $"{stringify(kv.Key)}: {stringify(kv.Value)}")
                    )
                    + " }",
            _ => throw new InvalidOperationException($"Unsupported type: {obj.GetType()}"),
        };

    while (true)
    {
        Console.Write("Enter a JSON string (or 'exit' to quit): \n/> ");
        string? input = Console.ReadLine();
        if (input == null || input.Trim().Equals("exit", StringComparison.CurrentCultureIgnoreCase))
            break;
        var result = parser.Parse(input);
        if (!result.HasError)
        {
            Console.WriteLine("Parsed successfully!");
            Console.WriteLine(
                $"Result({result.Data?.GetType().Name ?? "Null"}): {stringify(result.Data)}"
            );
        }
        else
        {
            Console.WriteLine("Parsing failed.");
            Console.WriteLine($"Error: {result.ErrorMessage ?? "unknown"}");
        }
        Console.WriteLine();
    }
}
