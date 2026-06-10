using System.Diagnostics;
using System.Globalization;
using StringHelper_lib;

namespace Ebnf_lib
{
    /// <summary>
    /// The EBNF-syntax is explained in EbnfSyntax.md which can be found here:<br/>
    /// @"C:\Users\User\source\repos\Ebnf_program\EbnfSyntax.md"
    /// </summary>
    public static class Ebnf
    {
        [Flags]
        public enum ReferenceType
        {
            None = 0,
            Expression = 1,
            SuccessProcess = 2,
            ErrorProcess = 4
        };

        public class Parser(string? mainVariable = null)
        {
            public Dictionary<string, Variable> Variables { get; } = [];
            public string? MainVariable { get; set; } = mainVariable;
            public bool CheckNullReferences { get; set; } = true;
            public Result Parse(string input, string? mainVariable = null)
            {
                mainVariable ??= MainVariable;
                if (mainVariable is null)
                    throw new InvalidOperationException("MainVariable is not set.");
                if (!Variables.TryGetValue(mainVariable, out var mainVar) || mainVar.Expression is null)
                    throw new InvalidOperationException($"Main variable \"{mainVariable}\" is not defined.");
                if (CheckNullReferences)
                {
                    var nullRefs = mainVar.Expression.GetNullReferences();
                    if (nullRefs.Count > 0)
                    {
                        string msg = $"The main variable \"{mainVariable}\" has null references:\n" +
                            string.Join("\n", ReferencesToText(nullRefs)) + "\n";
                        if (PrintConsoleOnError) Console.WriteLine(msg);
                        throw new InvalidOperationException(msg);
                    }
                }
                return mainVar.Parse(input);
            }
            public Result Parse(string input, Expression expression)
            {
                UpdateVariableReferences(expression);
                if (CheckNullReferences)
                {
                    var nullRefs = expression.GetNullReferences();
                    if (nullRefs.Count > 0)
                    {
                        string msg = $"The expression has null references:\n" +
                            string.Join("\n", ReferencesToText(nullRefs)) + "\n";
                        if (PrintConsoleOnError) Console.WriteLine(msg);
                        throw new InvalidOperationException(msg);
                    }
                }
                return expression.Parse(input);
            }

            public IDictionary<string, ReferenceType> GetNullReferences(string variableName)
            {
                if (!Variables.TryGetValue(variableName, out var variable) || variable.Expression is null)
                    throw new InvalidOperationException($"Variable \"{variableName}\" is not defined.");
                return variable.Expression.GetNullReferences();
            }
            public IDictionary<string, ReferenceType> GetNullReferences(Expression expression)
            {
                UpdateVariableReferences(expression);
                return expression.GetNullReferences();
            }
            public static IEnumerable<string> ReferencesToText(IEnumerable<KeyValuePair<string, ReferenceType>> references)
            {
                List<string> exprs = [];
                List<string> succPro = [];
                List<string> errPro = [];
                foreach (var kv in references)
                {
                    string name = kv.Key;
                    ReferenceType type = kv.Value;
                    if (type.HasFlag(ReferenceType.Expression)) exprs.Add(name);
                    if (type.HasFlag(ReferenceType.SuccessProcess)) succPro.Add(name);
                    if (type.HasFlag(ReferenceType.ErrorProcess)) errPro.Add(name);
                }
                bool any = false;
                if (exprs.Count > 0)
                {
                    any = true;
                    yield return "Expressions:";
                    foreach (var name in exprs) yield return $"- {name}";
                }
                if (succPro.Count > 0)
                {
                    any = true;
                    yield return "Success Processes:";
                    foreach (var name in succPro) yield return $"- {name}";
                }
                if (errPro.Count > 0)
                {
                    any = true;
                    yield return "Error Processes:";
                    foreach (var name in errPro) yield return $"- {name}";
                }
                if (!any) yield return "No references.";
            }

            private void UpdateVariableReferences(Expression? expression)
            {
                if (expression is null) return;
                foreach (var reference in expression.GetVariableReferences())
                {
                    if (!Variables.TryGetValue(reference.Name, out var variable))
                        Variables.Add(reference.Name, variable = reference.Name);
                    reference.Variable = variable;
                }
            }
            public void AddVariable(Variable variable)
            {
                if (!Variables.TryAdd(variable.Name, variable))
                    throw new ArgumentException($"Variable with name \"{variable.Name}\" already exists.");
                UpdateVariableReferences(variable.Expression);
            }
            public void AddExpression(string variableName, Expression expression)
            {
                if (!Variables.TryGetValue(variableName, out var variable))
                    Variables[variableName] = variable = new(variableName);
                variable.AddExpression(expression);
                UpdateVariableReferences(expression);
            }
            public void AddSuccessProcess(string variableName, SuccessProcess postProcessingExpression)
            {
                if (!Variables.TryGetValue(variableName, out var variable))
                    Variables[variableName] = variable = new(variableName);
                if (variable.SuccessProcess is not null)
                    throw new ArgumentException($"Variable \"{variableName}\" already has a post-processing expression.");
                variable.SuccessProcess = postProcessingExpression;
            }
            public void AddErrorProcess(string variableName, ErrorProcess errorProcessingExpression)
            {
                if (!Variables.TryGetValue(variableName, out var variable))
                    Variables[variableName] = variable = new(variableName);
                if (variable.ErrorProcess is not null)
                    throw new ArgumentException($"Variable \"{variableName}\" already has an error-processing expression.");
                variable.ErrorProcess = errorProcessingExpression;
            }

            public bool PrintConsoleOnError { get; set; } = true;
            public bool ThrowException { get; set; } = true;
            public string? AddEbnfCode(string code)
            {
                var result = EbnfParser.Parse(code);
                if (!result.Success)
                {
                    string msg = $"Failed to parse EBNF code: \n{result.ErrorMessage}\nContext: {result.GetContext()}";
                    if (PrintConsoleOnError) Console.WriteLine(msg);
                    if (ThrowException) throw new ArgumentException(msg);
                    return msg;
                }
                foreach (var (name, expr) in (IEnumerable<(string name, Expression expr)>)result.Data!)
                    AddExpression(name, expr);
                return null;
            }
            public string? AddEbnfCode(params string[] codeLines) => AddEbnfCode(string.Join("\n", codeLines));
            public string? AddEbnfCodeFromFile(string filePath)
            {
                string code;
                try
                {
                    code = File.ReadAllText(filePath);
                }
                catch (Exception ex)
                {
                    string msg = $"Failed to read EBNF code from file: {ex.Message}";
                    if (PrintConsoleOnError) Console.WriteLine(msg);
                    if (ThrowException) throw new ArgumentException(msg, ex);
                    return msg;
                }
                return AddEbnfCode(code);
            }


            /// <summary>
            /// Variable ::= [a-zA-Z] [a-zA-z0-9_]*
            /// </summary>
            public static Expression Variable { get; } =
                Expression.VariableName.Extend(new SuccessProcess((result, cursor) =>
                {
                    string name = result.StringSection.Section;
                    result.Data = new Expression.VariableReference(name);
                }));
            /// <summary>
            /// Terminal ::= '"' OpenChar* '"'<br/>
            /// </summary>
            public static Expression Terminal { get; } =
                Expression.String.Extend(new SuccessProcess((result, cursor) =>
                {
                    result.Data = new Expression.Terminal((string)result.Data!);
                }));
            /// <summary>
            /// TerminalChar ::= '\'' OpenChar '\''<br/>
            /// </summary>
            public static Expression TerminalChar { get; } =
                Expression.Char.Extend(new SuccessProcess((result, cursor) =>
                {
                    result.Data = new Expression.TerminalChar((char)result.Data!);
                }));
            /// <summary>
            /// CharacterClass ::= '~'? '[' OpenChar* ']'
            /// </summary>
            public static Expression CharacterClass { get; } = new Expression.Sequence(
                Expression.Repetition.Optional(new Expression.TerminalChar('~')),
                new Expression.TerminalChar('['),
                Expression.Repetition.ZeroOrMore(Expression.OpenChar.Extend(new SuccessProcess((result, cursor) =>
                {
                    if (result.StringSection.Section == "]")
                    {
                        result.Success = false;
                        cursor.Position = result.StringSection.Start;
                    }
                })).Extend(new ErrorProcess((result, cursor) =>
                {
                    string tmp = result.StringSection.Section;
                    if (tmp.Length != 2 || tmp[0] != '\\') return;
                    if ("-][".Contains(tmp[1])) result.Success = true;
                }))),
                new Expression.TerminalChar(']')).Extend(new SuccessProcess((result, cursor) =>
                {
                    //'~'? '[' OpenChar* ']'
                    var seq = ((Result.Sequence)result).Results;
                    // '~'?
                    bool negated = ((Result.Sequence)seq[0]).Results.Count == 1;
                    // OpenChar*
                    var gen = ((Result.Sequence)seq[2]).Results.GetEnumerator();
                    char currentChar() => (char)gen.Current.Data!;
                    HashSet<char> chars = [];
                    List<(char low, char hig)> ranges = [];
                    char? lastChar = null;
                    while (gen.MoveNext())
                    {
                        if (lastChar is null)
                        {
                            lastChar = currentChar();
                            continue;
                        }
                        if (gen.Current.StringSection.Section == "-")
                        {
                            if (gen.MoveNext())
                            {
                                char nextChar = currentChar();
                                if (lastChar > nextChar)
                                    ranges.Add((nextChar, lastChar.Value));
                                else ranges.Add((lastChar.Value, nextChar));
                                lastChar = (char)(nextChar + 1);
                            }
                            else
                            {
                                chars.Add(lastChar.Value);
                                lastChar = '-';
                            }
                        }
                        else
                        {
                            chars.Add(lastChar.Value);
                            lastChar = currentChar();
                        }
                    }
                    if (lastChar is not null) chars.Add(lastChar.Value);
                    result.Data = new Expression.CharacterClass(
                        c => negated ^ (chars.Contains(c) || ranges.Any(r => r.low <= c && c <= r.hig)))
                    { Description = result.StringSection.Section };
                }));
            /// <summary>
            /// Postfix ::= [*+?] | '{' s* nat s* ( ',' s* nat? s* )? '}'<br/>
            /// data = (int min, int max)
            /// </summary>
            public static Expression Postfix { get; } = new Expression.Choice(
                new Expression.TerminalChar('*').Extend(new SuccessProcess((r, _) => r.Data = (0, int.MaxValue))),
                new Expression.TerminalChar('+').Extend(new SuccessProcess((r, _) => r.Data = (1, int.MaxValue))),
                new Expression.TerminalChar('?').Extend(new SuccessProcess((r, _) => r.Data = (0, 1))),
                new Expression.Sequence(
                    new Expression.TerminalChar('{'),
                    Expression.WhiteSpaceStar, Expression.Nat_int, Expression.WhiteSpaceStar,
                    Expression.Repetition.Optional(new Expression.Sequence(
                        new Expression.TerminalChar(','), Expression.WhiteSpaceStar,
                        Expression.Repetition.Optional(Expression.Nat_int), Expression.WhiteSpaceStar)),
                    new Expression.TerminalChar('}')).Extend(new SuccessProcess((result, _) =>
                    {
                        // '{' s* nat s* ( ',' s* nat? s* )? '}'
                        var seq = (Result.Sequence)result;
                        int min = (int)seq.Results[2].Data!;
                        // ( ',' s* nat? s* )?
                        seq = (Result.Sequence)seq.Results[4];
                        if (seq.Results.Count == 0)
                        {
                            result.Data = (min, min);
                            return;
                        }
                        // ',' s* nat? s*
                        seq = (Result.Sequence)seq.Results[0];
                        // nat?
                        seq = (Result.Sequence)seq.Results[2];
                        if (seq.Results.Count == 0)
                        {
                            result.Data = (min, int.MaxValue);
                            return;
                        }
                        // nat 
                        seq = (Result.Sequence)seq.Results[0];
                        int max = (int)seq.Results[0].Data!;
                        result.Data = (min, max);
                    }))
            );
            /// <summary>
            /// Extension ::= [/!] name <br/>
            /// data = (char, string name)
            /// </summary>
            public static Expression Extension { get; } = new Expression.Sequence(
                new Expression.CharacterClass('/', '!'), Expression.VariableName).Extend(new SuccessProcess((result, _) =>
                {
                    var seq = ((Result.Sequence)result).Results;
                    StringSection section = seq[0].StringSection;
                    result.Data = (section.Source[section.Start], seq[1].StringSection.Section);
                }));
            /// <summary>
            /// Comment ::= '#' ~[\n]* 
            /// </summary>
            public static Expression Comment { get; } = new Expression.Sequence(new Expression.TerminalChar('#'),
                Expression.Repetition.ZeroOrMore(new Expression.CharacterClass(c => c != '\n')));
            public static Parser EbnfParser
            {
                get
                {
                    field ??= CreateEbnfParser();
                    return field;
                }
            } = null!;
            public static Expression Expression { get; } = new Expression(EbnfParser.Variables["expr"].Parse);
            private static Parser CreateEbnfParser()
            {
                var parser = new Parser("main");

                parser.AddExpression("name", Expression.VariableName);
                parser.AddSuccessProcess("name", new SuccessProcess((result, cursor) =>
                {
                    result.Data = result.StringSection.Section;
                }));

                parser.AddExpression("variable", Variable);
                parser.AddExpression("terminal", Terminal);
                parser.AddExpression("terminalChar", TerminalChar);
                parser.AddExpression("characterClass", CharacterClass);

                parser.AddExpression("postfix", Postfix);
                parser.AddExpression("extension", Extension);
                parser.AddExpression("comment", Comment);


                //- csl(arg) ::= arg ( s* ',' s* arg )*
                parser.AddExpression("csl", new Expression.Lambda(["arg"], new Expression.Sequence(
                    new Expression.VariableReference("arg"),
                    Expression.Repetition.ZeroOrMore(new Expression.Sequence(
                        Expression.WhiteSpaceStar, new Expression.TerminalChar(','), Expression.WhiteSpaceStar,
                        new Expression.VariableReference("arg"))))));
                //- csl(arg) ::= arg ( s* ',' s* arg )*
                parser.AddSuccessProcess("csl", new SuccessProcess((result, cursor) =>
                {
                    // arg ( s* ',' s* arg )*
                    var seq = ((Result.Sequence)result).Results;
                    List<object?> args = [seq[0].Data];
                    // ( s* ',' s* arg )*
                    seq = ((Result.Sequence)seq[1]).Results;
                    foreach (var item in seq)
                    {
                        // s* ',' s* arg
                        var itemSeq = ((Result.Sequence)item).Results;
                        args.Add(itemSeq[3].Data);
                    }
                    result.Data = args;
                }));
                //data = List<object?>

                //arguments ::= '(' ( s* csl(expr) s* )? ')'
                parser.AddExpression("arguments", new Expression.Sequence(
                    new Expression.TerminalChar('('),
                    Expression.Repetition.Optional(new Expression.Sequence(Expression.WhiteSpaceStar,
                        new Expression.Application(new Expression.VariableReference("csl"),
                            new Expression.VariableReference("expr")),
                        Expression.WhiteSpaceStar)), new Expression.TerminalChar(')')));
                //arguments ::= '(' ( s* csl(expr) s* )? ')'
                parser.AddSuccessProcess("arguments", new SuccessProcess((result, cursor) =>
                {
                    // '(' ( s* csl(expr1) s* )? ')'
                    var seq = ((Result.Sequence)result).Results;
                    // ( s* csl(expr1) s* )?
                    seq = ((Result.Sequence)seq[1]).Results;
                    if (seq.Count == 0)
                    {
                        result.Data = Array.Empty<Expression>();
                        return;
                    }
                    // s* csl(expr1) s*
                    seq = ((Result.Sequence)seq[0]).Results;
                    // csl(expr1)
                    result.Data = ((List<object?>)seq[1].Data!).Cast<Expression>().ToArray();
                }));
                //data = Expression[]

                //parameters ::= '(' ( s* csl(name) s* ( '=' s* expr s* ( ',' s* name s* '=' s* expr s* )* )? )? ')'
                parser.AddExpression("parameters", new Expression.Sequence(
                    // '(' ( s* csl(name) s* ( '=' s* expr s* ( ',' s* name s* '=' s* expr s* )* )? )? ')'
                    new Expression.TerminalChar('('),
                    // ( s* csl(name) s* ( '=' s* expr s* ( ',' s* name s* '=' s* expr s* )* )? )?
                    Expression.Repetition.Optional(new Expression.Sequence(Expression.WhiteSpaceStar,
                        new Expression.Application(new Expression.VariableReference("csl"),
                            new Expression.VariableReference("name")), Expression.WhiteSpaceStar,
                        // ( '=' s* expr s* ( ',' s* name s* '=' s* expr s* )* )?
                        Expression.Repetition.Optional(new Expression.Sequence(
                            new Expression.TerminalChar('='), Expression.WhiteSpaceStar,
                            new Expression.VariableReference("expr"), Expression.WhiteSpaceStar,
                            // ( ',' s* name s* '=' s* expr s* )*
                            Expression.Repetition.ZeroOrMore(new Expression.Sequence(
                                new Expression.TerminalChar(','), Expression.WhiteSpaceStar,
                                new Expression.VariableReference("name"), Expression.WhiteSpaceStar,
                                new Expression.TerminalChar('='), Expression.WhiteSpaceStar,
                                new Expression.VariableReference("expr"), Expression.WhiteSpaceStar
                                ).Extend(new SuccessProcess((result, cursor) =>
                                {
                                    // ',' s* name s* '=' s* expr s*
                                    var seq = ((Result.Sequence)result).Results;
                                    string name = (string)seq[2].Data!;
                                    Expression expr = (Expression)seq[6].Data!;
                                    result.Data = (name, expr);
                                }))
                            ).Extend(new SuccessProcess((result, cursor) =>
                            {
                                // ( ',' s* name s* '=' s* expr s* )*
                                // (string name, Expression expr)*
                                var seq = ((Result.Sequence)result).Results;
                                result.Data = seq.Select(x => ((string, Expression))x.Data!);
                            }))
                            ).Extend(new SuccessProcess((result, cursor) =>
                            {
                                // '=' s* expr s* ( ',' s* name s* '=' s* expr s* )*
                                // '=' s* expr s* IEnumerable<(string name, Expression expr)>
                                var seq = ((Result.Sequence)result).Results;
                                Expression expr = (Expression)seq[2].Data!;
                                IEnumerable<(string name, Expression expr)> additionalParams =
                                    (IEnumerable<(string name, Expression expr)>)seq[4].Data!;
                                List<string> paramNames = [];
                                List<Expression> paramExprs = [expr];
                                foreach (var (name, subExpr) in additionalParams)
                                {
                                    paramNames.Add(name);
                                    paramExprs.Add(subExpr);
                                }
                                result.Data = (paramNames, paramExprs);
                            }))
                        //data = (List<string> paramNames, List<Expression> paramExprs)
                        )
                        ).Extend(new SuccessProcess((result, cursor) =>
                        {
                            // s* csl(name) s* ( '=' s* expr s* ( ',' s* name s* '=' s* expr s* )* )?
                            // s* csl(name) s* ( (List<string> paramNames, List<Expression> paramExprs) )?
                            var seq = ((Result.Sequence)result).Results;
                            IEnumerable<string> namesA = ((List<object?>)seq[1].Data!).Cast<string>();
                            // ( (List<string> paramNames, List<Expression> paramExprs) )?
                            seq = ((Result.Sequence)seq[3]).Results;
                            if (seq.Count == 0)
                            {
                                result.Data = (namesA.ToArray(), Array.Empty<Expression>());
                                return;
                            }
                            var (namesB, exprs) = ((List<string>, List<Expression>))seq[0].Data!;
                            result.Data = (namesA.Concat(namesB).ToArray(), exprs.ToArray());
                        }))
                    //data = (string[] paramNames, Expression[] paramExprs)
                    ),
                    new Expression.TerminalChar(')')
                ));
                //parameters ::= '(' ( s* csl(name) s* ( '=' s* expr s* ( ',' s* name s* '=' s* expr s* )* )? )? ')'
                //parameters ::= '(' ( (string[] paramNames, Expression[] paramExprs) )? ')'
                parser.AddSuccessProcess("parameters", new SuccessProcess((result, cursor) =>
                {
                    // '(' ( (string[] paramNames, Expression[] paramExprs) )? ')'
                    var seq = ((Result.Sequence)result).Results;
                    // ( (string[] paramNames, Expression[] paramExprs) )?
                    seq = ((Result.Sequence)seq[1]).Results;
                    if (seq.Count == 0)
                    {
                        result.Data = (Array.Empty<string>(), Array.Empty<Expression>());
                        return;
                    }
                    // (string[] paramNames, Expression[] paramExprs)
                    result.Data = seq[0].Data;
                }));
                //data = (string[] paramNames, Expression[] paramExprs)


                //definition ::= name parameters? extension* s* "::=" s* expr 
                parser.AddExpression("definition", new Expression.Sequence(new Expression.VariableReference("name"),
                    Expression.Repetition.Optional(new Expression.VariableReference("parameters")),
                    Expression.Repetition.ZeroOrMore(new Expression.VariableReference("extension")),
                    Expression.WhiteSpaceStar, new Expression.Terminal("::="),
                    Expression.WhiteSpaceStar, new Expression.VariableReference("expr")));
                //definition ::= name parameters? extension* s* "::=" s* expr 
                parser.AddSuccessProcess("definition", new SuccessProcess((result, cursor) =>
                {
                    // name parameters? extension* s* "::=" s* expr 
                    var seq = ((Result.Sequence)result).Results;
                    string name = (string)seq[0].Data!;
                    // name parameters? extension* s* "::=" s* expr 
                    // expr 
                    Expression expr = (Expression)seq[6].Data!;
                    // name parameters? extension* s* "::=" s* expr 
                    // parameters?
                    var subSeq = ((Result.Sequence)seq[1]).Results;
                    if (subSeq.Count > 0)
                    {
                        var (paramNames, paramExprs) = ((string[] paramNames, Expression[] paramExprs))subSeq[0].Data!;
                        expr = new Expression.Lambda(paramNames, expr);
                        if (paramExprs.Length > 0)
                            expr = new Expression.OptionalParams(expr, paramNames.Length, paramExprs);
                    }
                    // name parameters? extension* s* "::=" s* expr 
                    // extension*
                    subSeq = ((Result.Sequence)seq[2]).Results;
                    foreach (var item in subSeq)
                    {
                        // extension
                        var (type, extName) = ((char type, string extName))item.Data!;
                        expr = type switch
                        {
                            '/' => new Expression.SuccessPostProcessingReference(expr, extName),
                            '!' => new Expression.ErrorPostProcessingReference(expr, extName),
                            _ => throw new InvalidOperationException($"Invalid extension type '{type}'."),
                        };
                    }
                    // name parameters? extension* s* "::=" s* expr 
                    result.Data = (name, expr);
                }));
                //data = (string name, Expression expr)
                parser.AddErrorProcess("definition", new ErrorProcess((result, cursor) =>
                {
                    if (cursor.EndOfString || result.StringSection.Size == 0 || result.Success) return;
                    void check(Func<StringCursor, Expression[]?, Result> parseFnuc)
                    {
                        var res = parseFnuc(cursor, null);
                        cursor.Position = res.StringSection.End;
                        //Since parseFunc ::= start OpenChar* end, and start was already checked,
                        //Success == false should only occur when cursor.EndOfString == true
                        if (!res.Success) Debug.Assert(cursor.EndOfString);
                    }
                    for (; !cursor.EndOfString; cursor++)
                    {
                        char c = cursor.CurrentChar;
                        switch (c)
                        {
                            case '-':
                                result.StringSection = cursor.CreateSection(result.StringSection.Start);
                                return;
                            case '[': check(CharacterClass.Parse); break;
                            case '"': check(Expression.String.Parse); break;
                            case '\'': check(Expression.Char.Parse); break;
                            default: break;
                        }
                    }
                }));

                //main ::= s* ( (definition | comment) s* )*
                parser.AddExpression("main", new Expression.Sequence(
                    Expression.WhiteSpaceStar,
                    Expression.Repetition.ZeroOrMore(new Expression.Sequence(
                        new Expression.Choice(new Expression.VariableReference("definition"),
                            new Expression.VariableReference("comment")),
                        Expression.WhiteSpaceStar
                    ))
                ));
                //main ::= s* ( (definition | comment) s* )*
                parser.AddSuccessProcess("main", new SuccessProcess((result, cursor) =>
                {
                    // s* ( (definition | comment) s* )*
                    var seq = ((Result.Sequence)result).Results;
                    // ( (definition | comment) s* )*
                    seq = ((Result.Sequence)seq[1]).Results;
                    result.Data = seq.Select(x =>
                    {
                        // (definition | comment) s*
                        return ((Result.Sequence)x).Results[0].Data;
                    }).Where(x => x is not null).Cast<(string, Expression)>();
                    if (!cursor.EndOfString) result.Success = false;
                }));
                //data = IEnumerable<(string name, Expression expr)>
                parser.AddErrorProcess("main", new ErrorProcess((result, cursor) =>
                {
                    if (result.ErrorMessage is null)
                    {
                        if (result.Data is not null && ((List<(string, Expression)>)result.Data).Count == 0)
                            //TODO: better error message
                            result.ErrorMessage =
                                $"- Expected a definition starting without '-' or a comment starting with '#'.\n" +
                                $"  (definition ::= name \"::=\" expr)\n" +
                                $"  Context: {result.GetLast().GetContext()}";
                        else
                        {
                            // ( s* (definition | comment) )* s* 
                            var seq = ((Result.Sequence)result).Results;
                            seq = ((Result.Sequence)seq[0]).Results;
                            result.ErrorMessage = $"- Parsing-process failed at {result.GetLast().GetContext()}.";
                        }
                    }
                }));


                //lambda ::= csl(name s* '=' s* expr) s* ';' s* expr | ( parameters | name ) s* "-->" s* expr 
                parser.AddExpression("lambda", new Expression.Choice(
                    // csl(name s* '=' s* expr) s* ';' s* expr 
                    new Expression.Sequence(new Expression.Application(new Expression.VariableReference("csl"),
                            new Expression.Sequence(new Expression.VariableReference("name"),
                                Expression.WhiteSpaceStar, new Expression.TerminalChar('='),
                                Expression.WhiteSpaceStar, new Expression.VariableReference("expr")
                            ).Extend(new SuccessProcess((result, cursor) =>
                            {
                                // name s* '=' s* expr
                                var seq = ((Result.Sequence)result).Results;
                                string name = (string)seq[0].Data!;
                                Expression expr = (Expression)seq[4].Data!;
                                result.Data = (name, expr);
                            }))
                        ).Extend(new SuccessProcess((result, cursor) =>
                        {
                            // csl(name s* '=' s* expr)
                            List<string> names = [];
                            List<Expression> exprs = [];
                            foreach (var (name, expr) in
                                ((List<object?>)result.Data!).Cast<(string name, Expression expr)>())
                            {
                                names.Add(name);
                                exprs.Add(expr);
                            }
                            result.Data = (names.ToArray(), exprs.ToArray());
                        })),
                        Expression.WhiteSpaceStar, new Expression.TerminalChar(';'),
                        Expression.WhiteSpaceStar, new Expression.VariableReference("expr")
                    ),
                    // ( parameters | name ) s* "-->" s* expr 
                    new Expression.Sequence(
                        new Expression.Choice(new Expression.VariableReference("parameters"),
                            new Expression.VariableReference("name").Extend(new SuccessProcess((result, cursor) =>
                            {
                                result.Data = ((string[])[(string)result.Data!], Array.Empty<Expression>());
                            }))
                        ),
                        Expression.WhiteSpaceStar,
                        new Expression.Terminal("-->").Extend(new ErrorProcess((result, cursor) =>
                        {
                            for (; !cursor.EndOfString; cursor++)
                            {
                                char c = cursor.CurrentChar;
                                if ("-><".Contains(c)) continue;
                                break;
                            }
                            //check if section == ""
                            if (result.StringSection.Start == cursor.Position) return;
                            result.Success = true;
                            result.StringSection = cursor.CreateSection(result.StringSection.Start);
                            result.ErrorMessage = $"- Expected \"-->\" at {result.GetContext()}. (got \"{result.StringSection}\")";
                        })),
                        Expression.WhiteSpaceStar, new Expression.VariableReference("expr")
                    )
                ));
                //lambda ::= ( parameters | name ) s* "-->" s* expr | csl(name s* '=' s* expr) s* ';' s* expr 
                //(string[] paramNames, Expression[] paramExprs) s* (';'|"-->") s* expr
                parser.AddSuccessProcess("lambda", new SuccessProcess((result, cursor) =>
                {
                    var seq = ((Result.Sequence)result).Results;
                    var (paramNames, paramExprs) = ((string[] paramNames, Expression[] paramExprs))seq[0].Data!;
                    var expr = (Expression)seq[^1].Data!;
                    Expression tmp = new Expression.Lambda(paramNames, expr);
                    switch (seq[2].StringSection.Section)
                    {
                        case ";": tmp = new Expression.Application(tmp, paramExprs); break;
                        case "-->":
                            if (paramExprs.Length > 0)
                                tmp = new Expression.OptionalParams(tmp, paramNames.Length, paramExprs);
                            break;
                        default: throw new Exception();
                    }
                    result.Data = tmp;
                }));

                //expr ::= expr1 | lambda 
                parser.AddExpression("expr", new Expression.Choice(
                    new Expression.VariableReference("expr1"),
                    new Expression.VariableReference("lambda")));

                //expr1 ::= expr2 ( s* '|' s* expr2 )*
                parser.AddExpression("expr1", new Expression.Sequence(
                    new Expression.VariableReference("expr2"),
                    Expression.Repetition.ZeroOrMore(new Expression.Sequence(
                        Expression.WhiteSpaceStar, new Expression.TerminalChar('|'), Expression.WhiteSpaceStar,
                        new Expression.VariableReference("expr2")
                    ))
                ));
                //expr1 ::= expr2 ( s* '|' s* expr2 )*
                parser.AddSuccessProcess("expr1", new SuccessProcess((result, cursor) =>
                {
                    // expr2 ( s* '|' s* expr2 )*
                    var seq = ((Result.Sequence)result).Results;
                    Expression expr = (Expression)seq[0].Data!;
                    // ( s* '|' s* expr2 )*
                    seq = ((Result.Sequence)seq[1]).Results;
                    if (seq.Count == 0)
                    {
                        result.Data = expr;
                        return;
                    }
                    List<Expression> ls = [expr];
                    foreach (var item in seq)
                    {
                        // s* '|' s* expr2 
                        var itemSeq = ((Result.Sequence)item).Results;
                        Expression nextExpr = (Expression)itemSeq[3].Data!;
                        ls.Add(nextExpr);
                    }
                    result.Data = new Expression.Choice(ls);
                }));

                //expr2 ::= expr3 ( s+ expr3 notDef )*
                parser.AddExpression("expr2", new Expression.Sequence(
                    new Expression.VariableReference("expr3"),
                    Expression.Repetition.ZeroOrMore(new Expression.Sequence(
                        Expression.WhiteSpaceLinePlus, new Expression.VariableReference("expr3"),
                        new Expression((@this, cursor) =>
                        {
                            int cpy = cursor.Position;
                            while (!cursor.EndOfString && char.IsWhiteSpace(cursor.CurrentChar)) cursor++;
                            bool error = cursor.Source.Length - cursor.Position >= 3 && cursor.Source.Substring(cursor.Position, 3) == "::=";
                            cursor.Position = cpy;
                            return new Result(@this, cursor.CreateEmptySection()) { Success = !error };
                        })
                    ))
                ));
                //expr2 ::= expr3 ( s+ expr3 notDef )*
                parser.AddSuccessProcess("expr2", new SuccessProcess((result, cursor) =>
                {
                    // expr3 ( s+ expr3 )*
                    var seq = ((Result.Sequence)result).Results;
                    var expr = (Expression)seq[0].Data!;
                    // ( s+ expr3 )*
                    seq = ((Result.Sequence)seq[1]).Results;
                    if (seq.Count == 0)
                    {
                        result.Data = expr;
                        return;
                    }
                    List<Expression> ls = [expr];
                    foreach (var item in seq)
                    {
                        // s+ expr3 
                        var itemSeq = ((Result.Sequence)item).Results;
                        Expression nextExpr = (Expression)itemSeq[1].Data!;
                        ls.Add(nextExpr);
                    }
                    result.Data = new Expression.Sequence(ls);
                }));

                //expr3 ::= expr4 (postfix | extension)*
                parser.AddExpression("expr3", new Expression.Sequence(
                    new Expression.VariableReference("expr4"),
                    Expression.Repetition.ZeroOrMore(new Expression.Choice(
                        new Expression.VariableReference("postfix"), new Expression.VariableReference("extension")
                    ))
                ));
                //expr3 ::= expr4 (postfix | extension)*
                parser.AddSuccessProcess("expr3", new SuccessProcess((result, cursor) =>
                {
                    // expr4 (postfix | extension)*
                    var seq = ((Result.Sequence)result).Results;
                    Expression expr = (Expression)seq[0].Data!;
                    // (postfix | extension)*
                    seq = ((Result.Sequence)seq[1]).Results;
                    if (seq.Count == 0)
                    {
                        result.Data = expr;
                        return;
                    }
                    foreach (var item in seq)
                    {
                        // postfix | extension 
                        // (int min, int max) | (char type, string extName)
                        expr = item.Data switch
                        {
                            (int min, int max) => new Expression.Repetition(expr, min, max),
                            (char type, string extName) when type == '/' =>
                                new Expression.SuccessPostProcessingReference(expr, extName),
                            (char type, string extName) when type == '!' =>
                                new Expression.ErrorPostProcessingReference(expr, extName),
                            _ => throw new InvalidOperationException($"Invalid postfix/extension data '{item.Data}'."),
                        };
                    }
                    result.Data = expr;
                }));

                //expr4 ::= expr5 arguments?
                parser.AddExpression("expr4", new Expression.Sequence(
                    new Expression.VariableReference("expr5"),
                    Expression.Repetition.Optional(new Expression.VariableReference("arguments"))
                ));
                //expr4 ::= expr5 arguments?
                parser.AddSuccessProcess("expr4", new SuccessProcess((result, cursor) =>
                {
                    // expr5 arguments?
                    var seq = ((Result.Sequence)result).Results;
                    Expression expr = (Expression)seq[0].Data!;
                    // arguments?
                    seq = ((Result.Sequence)seq[1]).Results;
                    if (seq.Count == 0)
                    {
                        result.Data = expr;
                        return;
                    }
                    Expression[] args = (Expression[])seq[0].Data!;
                    result.Data = new Expression.Application(expr, args);
                }));

                //expr5 ::= '(' s* expr s* ')' | atomic 
                parser.AddExpression("expr5", new Expression.Choice(
                    new Expression.Sequence(new Expression.TerminalChar('('), Expression.WhiteSpaceStar,
                        new Expression.VariableReference("expr"),
                        Expression.WhiteSpaceStar, new Expression.TerminalChar(')')
                    ).Extend(new SuccessProcess((result, cursor) =>
                    {
                        // '(' s* expr s* ')'
                        var seq = ((Result.Sequence)result).Results;
                        result.Data = seq[2].Data;
                    })),
                    new Expression.VariableReference("atomic")
                ));

                //atomic ::= Terminal | TerminalChar | CharacterClass | Variable 
                parser.AddExpression("atomic", new Expression.Choice(
                    new Expression.VariableReference("terminal"), new Expression.VariableReference("terminalChar"),
                    new Expression.VariableReference("characterClass"), new Expression.VariableReference("variable")
                ));

                return parser;
            }
            /*
             * postfix ::= [*+?] | '{' s* nat s* ( ',' s* nat? s* )? '}'
             * extension ::= [/!] name 
             * comment ::= '#' ~[\n]* 
             * 
             * csl(arg) ::= arg ( s* ',' s* arg )*
             * arguments ::= '(' ( s* csl(expr) s* )? ')'
             * parameters ::= '(' ( s* csl(name) s* ( '=' s* expr s* ( ',' s* name s* '=' s* expr s* )* )? )? ')'
             * 
             * definition ::= name parameters? extension* s* "::=" s* expr 
             * main ::= s* ( (definition | comment) s* )*
             * 
             * lambda ::= csl(name s* '=' s* expr) s* ';' s* expr | ( parameters | name ) s* "-->" s* expr 
             * expr ::= expr1 | lambda 
             * expr1 ::= expr2 ( s* '|' s* expr2 )*
             * expr2 ::= expr3 ( s+ expr3 notDef )*
             * expr3 ::= expr4 (postfix | extension)*
             * expr4 ::= expr5 arguments?
             * expr5 ::= '(' s* expr s* ')' | atomic 
             * atomic ::= Terminal | TerminalChar | CharacterClass | Variable 
             * 
             * Terminal ::= '"' OpenChar* '"'
             * TerminalChar ::= '\'' OpenChar '\''
             * CharacterClass ::= '~'? '[' OpenChar* ']'
             * Variable ::= name
             * name ::= [a-zA-Z] [a-zA-Z0-9_]*
             */
        }
        public class Variable(string name)
        {
            public static implicit operator Variable(string name) => new(name);
            public static Variable FromEbnf(string code)
            {
                var result = Parser.EbnfParser.Variables["definition"].Parse(code);
                if (result.HasError) throw new FormatException($"Failed to parse EBNF definition: " +
                    result.ErrorMessage ?? "unknown error");
                //data = (string name, Expression expr)
                var (name, expr) = ((string name, Expression expr))result.Data!;
                return new Variable(name) { Expression = expr };
            }

            public string Name { get; } = name;
            public Expression? Expression { get; set; }
            public SuccessProcess? SuccessProcess { get; set; }
            public ErrorProcess? ErrorProcess { get; set; }
            public bool IsEmpty => Expression is null && SuccessProcess is null && ErrorProcess is null;

            public void AddExpression(Expression expression)
            {
                switch (Expression)
                {
                    case null:
                        Expression = expression;
                        break;
                    case Expression.Choice option:
                        option.Expressions.Add(expression);
                        break;
                    default:
                        Expression = new Expression.Choice([Expression, expression]);
                        break;
                }
            }
            public void AddSuccessProcess(SuccessProcess successProcess)
            {
                if (SuccessProcess is not null)
                    throw new InvalidOperationException($"Variable \"{Name}\" already has a success process.");
                SuccessProcess = successProcess;
            }
            public void AddErrorProcess(ErrorProcess errorProcess)
            {
                if (ErrorProcess is not null)
                    throw new InvalidOperationException($"Variable \"{Name}\" already has an error process.");
                ErrorProcess = errorProcess;
            }

            public void CopyFrom(Variable other)
            {
                Expression = other.Expression;
                SuccessProcess = other.SuccessProcess;
                ErrorProcess = other.ErrorProcess;
            }
            public void CopyFrom(Expression expression)
            {
                Expression = expression;
                SuccessProcess = null;
                ErrorProcess = null;
            }

            public Result Parse(StringCursor value, Expression[]? args = null)
            {
                if (Expression is null)
                    throw new InvalidOperationException($"Variable \"{Name}\" does not have an associated expression.");
                var result = Expression.Parse(value, args);
                if (result.Success) ProcessSuccess(result, value);
                if (result.HasError) ProcessError(result, value);
                return result;
            }
            public void ProcessSuccess(Result result, StringCursor value, Expression[]? args = null) =>
                SuccessProcess?.Process(result, value, args);
            public void ProcessError(Result result, StringCursor value, Expression[]? args = null) =>
                ErrorProcess?.Process(result, value, args);

            public override string ToString() => Name;
        }
        public class Expression
        {
            public abstract class ExpressionList : Expression
            {
                public List<Expression> Expressions { get; init; } = [];
                public override IEnumerable<VariableReference> GetVariableReferences()
                {
                    foreach (var expression in Expressions)
                        foreach (var reference in expression.GetVariableReferences())
                            yield return reference;
                }
                protected override void GetNullReferencesInternal(HashSet<Expression> checkedExprs, IDictionary<string, ReferenceType> nullRefs)
                {
                    foreach (var expr in Expressions) expr.GetNullReferences(checkedExprs, nullRefs);
                }
            }
            public class Choice : ExpressionList
            {
                public const int Layer = 1;

                public Choice(IEnumerable<Expression> options)
                {
                    Expressions = [.. options];
                    ParseFunc = ToParseFunc(cursor =>
                    {
                        int initialCursor = cursor.Position;
                        foreach (var option in options)
                        {
                            var result = option.Parse(cursor);
                            if (result.Success)
                            {
                                result.SrcExpr = this;
                                return result;
                            }
                            cursor.Position = initialCursor;
                        }
                        return new Result(this, cursor.CreateEmptySection())
                        {
                            Success = false,
                        };
                    });
                }
                public Choice(params Expression[] options) : this((IEnumerable<Expression>)options) { }

                public override string ToString(int upperLayer) =>
                    upperLayer > Layer ? $"({ToString()})" : ToString();
                public override string ToString() => $"({string.Join(" | ", Expressions.Select(x => x.ToString(Layer)))})";
            }
            public class Sequence : ExpressionList
            {
                public const int Layer = 2;

                public Sequence(IEnumerable<Expression> expressions)
                {
                    Expressions = [.. expressions];
                    ParseFunc = ToParseFunc(cursor =>
                    {
                        int initialCursor = cursor.Position;
                        List<Result> results = [];
                        List<string> errors = [];
                        foreach (var expression in Expressions)
                        {
                            var result = expression.Parse(cursor);
                            results.Add(result);
                            if (!result.Success)
                            {
                                return new Result.Sequence(this, cursor.CreateSection(initialCursor))
                                {
                                    Results = results,
                                    Success = false,
                                };
                            }
                            if (result.HasError) errors.Add(result.ErrorMessage ?? $"- Parsing-process failed! ({result.GetContext()})");
                        }
                        return new Result.Sequence(this, cursor.CreateSection(initialCursor))
                        {
                            Results = results,
                            ErrorMessage = errors.Count == 0 ? null : string.Join('\n', errors),
                        };
                    });
                }
                public Sequence(params Expression[] expressions) : this((IEnumerable<Expression>)expressions) { }

                public override string ToString(int upperLayer) =>
                    upperLayer > Layer ? $"({ToString()})" : ToString();
                public override string ToString() => string.Join(" ", Expressions.Select(x => x.ToString(Layer)));
            }
            public class Repetition : Expression
            {
                public const int Layer = 3;

                public Expression Expression { get; }
                public int Minimum { get; }
                public int Maximum { get; }
                public Repetition(Expression expression, int min, int max)
                {
                    if (min < 0) throw new ArgumentOutOfRangeException(nameof(min), "Minimum must be non-negative.");
                    if (max < min) throw new ArgumentOutOfRangeException(nameof(max), "Maximum must be greater than or equal to minimum.");
                    Expression = expression;
                    Minimum = min;
                    Maximum = max;
                    ParseFunc = ToParseFunc(cursor =>
                    {
                        List<Result> results = [];
                        List<string> errors = [];
                        int count = 0;
                        int initialCursor = cursor.Position;
                        int lastCursor = initialCursor;
                        while (count < Maximum)
                        {
                            var result = Expression.Parse(cursor);
                            if (!result.Success) break;
                            lastCursor = cursor.Position;
                            count++;
                            //Since result.Success == true and result.HasError == true, therefore result.ErrorMessage can't be null.
                            if (result.HasError) errors.Add(result.ErrorMessage!);
                            results.Add(result);
                        }
                        cursor.Position = lastCursor;
                        return new Result.Sequence(this, cursor.CreateSection(initialCursor))
                        {
                            Results = results,
                            Success = count >= Minimum,
                            ErrorMessage = errors.Count == 0 ? null : string.Join('\n', errors),
                        };
                    });
                }

                public override IEnumerable<VariableReference> GetVariableReferences() =>
                    Expression.GetVariableReferences();
                protected override void GetNullReferencesInternal(HashSet<Expression> checkedExprs,
                    IDictionary<string, ReferenceType> nullRefs) => Expression.GetNullReferences(checkedExprs, nullRefs);

                public static Repetition ZeroOrMore(Expression expression) => new(expression, 0, int.MaxValue);
                public static Repetition OneOrMore(Expression expression) => new(expression, 1, int.MaxValue);
                public static Repetition Optional(Expression expression) => new(expression, 0, 1);
                public static Repetition Exact(Expression expression, int count) => new(expression, count, count);
                public static Repetition AtLeast(Expression expression, int min) => new(expression, min, int.MaxValue);

                public override string ToString(int upperLayer) =>
                    upperLayer > Layer ? $"({ToString()})" : ToString();
                public override string ToString() => $"{Expression.ToString(Layer)}{GetRepitionString(Minimum, Maximum)}";
                public static string GetRepitionString(int min, int max)
                {
                    if (min == 0 && max == int.MaxValue) return "*";
                    if (min == 1 && max == int.MaxValue) return "+";
                    if (min == 0 && max == 1) return "?";
                    if (min == max) return $"{{{min}}}";
                    if (max == int.MaxValue) return $"{{{min},}}";
                    return $"{{{min},{max}}}";
                }
            }
            public class Terminal : Expression
            {
                public static implicit operator Terminal(string value) => new(value);
                public string Value { get; }
                public Terminal(string value)
                {
                    Value = value;
                    ParseFunc = ToParseFunc(cursor =>
                    {
                        int initialCursor = cursor.Position;
                        foreach (char c in value)
                        {
                            if (cursor.EndOfString || cursor.CurrentChar != c)
                            {
                                return new Result(this, cursor.CreateSection(initialCursor))
                                {
                                    Success = false,
                                };
                            }
                            cursor++;
                        }
                        return new Result(this, cursor.CreateSection(initialCursor));
                    });
                }
                public static new Terminal FromEbnf(string code)
                {
                    StringCursor cursor = code;
                    var result = Parser.Terminal.Parse(cursor);
                    if (result.HasError) throw new FormatException($"Failed to parse EBNF terminal: " +
                        result.ErrorMessage ?? "unknown error");
                    if (!cursor.EndOfString) throw new FormatException($"Unexpected characters after EBNF terminal: \"{code[cursor.Position..]}\"");
                    return (Terminal)result.Data!;
                }

                public override string ToString() => $"\"{StrHelper.StringAsString(Value)}\"";
            }
            public class TerminalChar : Expression
            {
                public static implicit operator TerminalChar(char value) => new(value);
                public char Value { get; }
                public TerminalChar(char value)
                {
                    Value = value;
                    ParseFunc = ToParseFunc(cursor =>
                    {
                        if (cursor.EndOfString || cursor.CurrentChar != Value)
                        {
                            return new Result(this, cursor.CreateEmptySection())
                            {
                                Success = false,
                            };
                        }
                        cursor++;
                        return new Result(this, cursor.CreateSection(cursor.Position - 1));
                    });
                }
                public static new TerminalChar FromEbnf(string code)
                {
                    StringCursor cursor = code;
                    var result = Parser.TerminalChar.Parse(cursor);
                    if (result.HasError) throw new FormatException($"Failed to parse EBNF terminal character: " +
                        result.ErrorMessage ?? "unknown error");
                    if (!cursor.EndOfString) throw new FormatException($"Unexpected characters after EBNF terminal character: \"{code[cursor.Position..]}\"");
                    return (TerminalChar)result.Data!;
                }

                public override string ToString() => $"'{StrHelper.CharAsString(Value, c => c == '\'')}'";
            }
            public class CharacterClass : Expression
            {
                public string? Description { get; set; }
                public Func<char, bool> Predicate { get; }
                public CharacterClass(Func<char, bool> predicate)
                {
                    Predicate = predicate;
                    ParseFunc = ToParseFunc(cursor =>
                    {
                        if (cursor.EndOfString || !Predicate(cursor.CurrentChar))
                        {
                            return new Result(this, cursor.CreateEmptySection())
                            {
                                Success = false,
                            };
                        }
                        cursor++;
                        return new Result(this, cursor.CreateSection(cursor.Position - 1));
                    });
                }
                public CharacterClass(params char[] chars) : this(ch => chars.Contains(ch)) { }
                public static new CharacterClass FromEbnf(string code)
                {
                    StringCursor cursor = code;
                    var result = Parser.CharacterClass.Parse(cursor);
                    if (result.HasError) throw new FormatException($"Failed to parse EBNF character class: " +
                        result.ErrorMessage ?? "unknown error");
                    if (!cursor.EndOfString) throw new FormatException($"Unexpected characters after EBNF character class: \"{code[cursor.Position..]}\"");
                    return (CharacterClass)result.Data!;
                }

                public override string ToString() => Description ??= ToString(Predicate);
                public static string ToString(Func<char, bool> predicate)
                {
                    List<char> digits = [], lowerLetters = [], upperLetters = [], others = [];
                    List<char> n_digits = [], n_lowerLetters = [], n_upperLetters = [], n_others = [];
                    for (char c = (char)0; c < 128; c++)
                    {
                        if (char.IsDigit(c)) (predicate(c) ? digits : n_digits).Add(c);
                        else if (char.IsLower(c)) (predicate(c) ? lowerLetters : n_lowerLetters).Add(c);
                        else if (char.IsUpper(c)) (predicate(c) ? upperLetters : n_upperLetters).Add(c);
                        else (predicate(c) ? others : n_others).Add(c);
                    }
                    string tmp(List<char> ls)
                    {
                        if (ls.Count == 0) return "";
                        List<string> parts = [];
                        int startIdx = 0;
                        int lastIdx = -1;
                        int i = 0;
                        void add()
                        {
                            if (lastIdx - startIdx > 2) parts.Add($"{ls[startIdx]}-{ls[lastIdx]}");
                            else parts.AddRange(ls[startIdx..(lastIdx + 1)].Select(c => StrHelper.CharAsString(c, x => x == '-' || x == ']')));
                            startIdx = lastIdx = i;
                        }
                        for (; i < ls.Count; i++)
                        {
                            if (lastIdx == -1 || ls[i] == ls[lastIdx] + 1)
                            {
                                lastIdx = i;
                            }
                            else add();
                        }
                        add();
                        return string.Concat(parts);
                    }
                    string str(List<char> digits, List<char> lowerLetters, List<char> upperLetters, List<char> others)
                    {
                        List<string> parts = [tmp(digits), tmp(lowerLetters), tmp(upperLetters)];
                        parts.AddRange(others.Select(c => StrHelper.CharAsString(c, x => x == '-' || x == ']')));
                        return $"[{string.Concat(parts)}]";
                    }
                    string a = str(digits, lowerLetters, upperLetters, others);
                    string b = $"~{str(n_digits, n_lowerLetters, n_upperLetters, n_others)}";
                    return a.Length <= b.Length ? a : b;
                }
            }
            public class Function : Expression
            {
                public Function(Func<StringCursor, Expression[], Result> parseFunc)
                {
                    ParseFunc = (cursor, args) =>
                    {
                        if (args is null) return new Result(this, cursor.CreateEmptySection())
                        {
                            Success = false,
                        };
                        var result = parseFunc(cursor, args);
                        result.SrcExpr = this;
                        return result;
                    };
                }
                public Function(Func<StringCursor, Expression[], Result> parseFunc, int argCnt)
                {
                    if (argCnt < 0) throw new ArgumentOutOfRangeException(nameof(argCnt), "Argument count must be non-negative.");
                    ParseFunc = (cursor, args) =>
                    {
                        if (args is null || args.Length != argCnt) return new Result(this, cursor.CreateEmptySection())
                        {
                            Success = false,
                        };
                        var result = parseFunc(cursor, args);
                        result.SrcExpr = this;
                        return result;
                    };
                }
            }
            public class Lambda : Expression
            {
                public const int Layer = 0;
                public Variable? GetVariable(string name) => Parameters.FirstOrDefault(x => x.Name == name);
                public Variable[] Parameters { get; private set; }
                public Expression Expression { get; }
                public Lambda(IEnumerable<string> parameters, Expression expression)
                {
                    Parameters = [.. parameters.Select(p => new Variable(p) { Expression = Expression.Null })];
                    if (Parameters.Select(p => p.Name).Distinct().Count() != Parameters.Length)
                        throw new ArgumentException("Duplicate was found in parameters!");
                    Expression = expression;
                    foreach (var reference in expression.GetVariableReferences())
                    {
                        var parameter = GetVariable(reference.Name);
                        if (parameter is null) continue;
                        reference.Variable = parameter;
                    }
                    ParseFunc = (cursor, args) =>
                    {
                        if (args is null || args.Length != Parameters.Length) return new Result(this, cursor.CreateEmptySection())
                        {
                            Success = false,
                        };
                        Expression?[] copy = new Expression[Parameters.Length];
                        for (int i = 0; i < Parameters.Length; i++)
                            (copy[i], Parameters[i].Expression) = (Parameters[i].Expression, args[i]);
                        var result = Expression.Parse(cursor);
                        result.SrcExpr = this;
                        for (int i = 0; i < Parameters.Length; i++) Parameters[i].Expression = copy[i];
                        return result;
                    };
                }

                public override IEnumerable<VariableReference> GetVariableReferences() =>
                    Expression.GetVariableReferences().Where(r => GetVariable(r.Name) is null);
                protected override void GetNullReferencesInternal(HashSet<Expression> checkedExprs,
                    IDictionary<string, ReferenceType> nullRefs) => Expression.GetNullReferences(checkedExprs, nullRefs);

                public override string ToString(int upperLayer) =>
                    upperLayer > Layer ? $"({ToString()})" : ToString();
                public override string ToString() =>
                    $"({string.Join(", ", Parameters.Select(p => p.Name))}) --> {Expression.ToString(Layer)}";
            }
            public class Application : Expression
            {
                public const int Layer = 4;
                public new Expression Function { get; }
                public Expression[] Arguments { get; }
                public Application(Expression function, params Expression[] arguments)
                {
                    Function = function;
                    Arguments = arguments;
                    ParseFunc = ToParseFunc(cursor =>
                    {
                        var result = Function.Parse(cursor, Arguments);
                        result.SrcExpr = this;
                        return result;
                    });
                }

                public override IEnumerable<VariableReference> GetVariableReferences()
                {
                    foreach (var reference in Function.GetVariableReferences())
                        yield return reference;
                    foreach (var argument in Arguments)
                        foreach (var reference in argument.GetVariableReferences())
                            yield return reference;
                }
                protected override void GetNullReferencesInternal(
                    HashSet<Expression> checkedExprs, IDictionary<string, ReferenceType> nullRefs)
                {
                    Function.GetNullReferences(checkedExprs, nullRefs);
                    foreach (var argument in Arguments) argument.GetNullReferences(checkedExprs, nullRefs);
                }

                public override string ToString(int upperLayer) =>
                    upperLayer > Layer ? $"({ToString()})" : ToString();
                public override string ToString() =>
                    $"{Function.ToString(Layer)}({string.Join(", ", Arguments.Select(a => a.ToString(Lambda.Layer + 1)))})";
            }
            public class OptionalParams : Expression
            {
                public const int Layer = 0;
                public Expression Expression { get; }
                public Expression[] FullArgList { get; }
                public Expression[] DefaultValues { get; }
                public OptionalParams(Expression expression, int totalParamCount, Expression[] defaultValues)
                {
                    Expression = expression;
                    FullArgList = new Expression[totalParamCount];
                    DefaultValues = defaultValues;
                    if (FullArgList.Length < DefaultValues.Length)
                        throw new ArgumentException("Total parameter count must be greater than or equal to the number of default values.");
                    ParseFunc = (cursor, args) =>
                    {
                        if (args is null || args.Length > FullArgList.Length ||
                            args.Length < FullArgList.Length - DefaultValues.Length)
                            return new Result(this, cursor.CreateEmptySection())
                            {
                                Success = false,
                            };
                        Array.Copy(args, FullArgList, args.Length);
                        int missingCount = FullArgList.Length - args.Length;
                        Array.Copy(DefaultValues, DefaultValues.Length - missingCount, FullArgList, args.Length, missingCount);
                        var result = Expression.Parse(cursor, FullArgList);
                        result.SrcExpr = this;
                        return result;
                    };
                }

                public override IEnumerable<VariableReference> GetVariableReferences()
                {
                    foreach (var reference in Expression.GetVariableReferences())
                        yield return reference;
                    foreach (var defaultValue in DefaultValues)
                        foreach (var reference in defaultValue.GetVariableReferences())
                            yield return reference;
                }
                protected override void GetNullReferencesInternal(
                    HashSet<Expression> checkedExprs, IDictionary<string, ReferenceType> nullRefs)
                {
                    Expression.GetNullReferences(checkedExprs, nullRefs);
                    foreach (var defaultValue in DefaultValues) defaultValue.GetNullReferences(checkedExprs, nullRefs);
                }

                public override string ToString(int upperLayer) =>
                    upperLayer > Layer ? $"({ToString()})" : ToString();
                public override string ToString()
                {
                    if (DefaultValues.Length == 0) return Expression.ToString()!;
                    if (Expression is not Lambda lambda) return nameof(OptionalParams);
                    Debug.Assert(lambda.Parameters.Length == FullArgList.Length);
                    string firstPart = string.Concat(lambda.Parameters.Take(FullArgList.Length - DefaultValues.Length)
                        .Select(p => $"{p.Name}, "));
                    string secondPart = string.Join(", ", lambda.Parameters.Skip(FullArgList.Length - DefaultValues.Length)
                        .Select((p, i) => $"{p.Name} = {DefaultValues[i].ToString(Lambda.Layer + 1)}"));
                    return $"({firstPart}{secondPart}) --> {lambda.Expression.ToString(Lambda.Layer)}";
                }
            }
            public class VariableReference : Expression
            {
                public Variable Variable { get; set; }
                public string Name => Variable.Name;
                public VariableReference(Variable variable)
                {
                    Variable = variable;
                    ParseFunc = (cursor, args) =>
                    {
                        var result = Variable.Parse(cursor, args);
                        result.SrcExpr = this;
                        return result;
                    };
                }

                public override Expression? GetSourceExpression() => Variable.Expression?.GetSourceExpression();
                public override IEnumerable<VariableReference> GetVariableReferences()
                {
                    yield return this;
                }
                protected override void GetNullReferencesInternal(
                    HashSet<Expression> checkedExprs, IDictionary<string, ReferenceType> nullRefs)
                {
                    if (Variable.Expression is null)
                    {
                        if (nullRefs.TryGetValue(Name, out var type))
                            nullRefs[Name] = type | ReferenceType.Expression;
                        else nullRefs[Name] = ReferenceType.Expression;
                    }
                    else Variable.Expression.GetNullReferences(checkedExprs, nullRefs);
                }

                public override string ToString() => Name;
            }
            public class PostProcessingExpression : Expression
            {
                public Expression Expression { get; }
                public PostProcess PostProcess { get; }

                public static Expression GetSourceExpression(Expression expression)
                {
                    while (expression is PostProcessingExpression postProcessingExpression)
                        expression = postProcessingExpression.Expression;
                    return expression;
                }

                public static PostProcessingExpression Create(Expression expression, PostProcess postProcess) =>
                    postProcess switch
                    {
                        SuccessProcess successProcess => new PostProcessingExpression(expression, successProcess),
                        ErrorProcess errorProcess => new PostProcessingExpression(expression, errorProcess),
                        _ => throw new NotImplementedException(),
                    };
                public PostProcessingExpression(Expression expression, SuccessProcess successProcess)
                {
                    Expression = expression;
                    PostProcess = successProcess;
                    ParseFunc = (cursor, args) =>
                    {
                        var result = Expression.Parse(cursor, args);
                        if (result.Success) successProcess.Process(result, cursor, args);
                        result.SrcExpr = this;
                        return result;
                    };
                }
                public PostProcessingExpression(Expression expression, ErrorProcess errorProcess)
                {
                    Expression = expression;
                    PostProcess = errorProcess;
                    ParseFunc = (cursor, args) =>
                    {
                        var result = Expression.Parse(cursor, args);
                        if (result.HasError) errorProcess.Process(result, cursor, args);
                        result.SrcExpr = this;
                        return result;
                    };
                }

                public override Expression? GetSourceExpression() => Expression.GetSourceExpression();
                public override IEnumerable<VariableReference> GetVariableReferences() =>
                    Expression.GetVariableReferences();
                protected override void GetNullReferencesInternal(
                    HashSet<Expression> checkedExprs, IDictionary<string, ReferenceType> nullRefs) =>
                    Expression.GetNullReferences(checkedExprs, nullRefs);
            }
            public abstract class PostProcessingReference(Expression expression, Variable variable) : VariableReference(variable)
            {
                public const int Layer = 3;
                public Expression Expression { get; } = expression;

                public override Expression? GetSourceExpression() => Expression.GetSourceExpression();
                public override IEnumerable<VariableReference> GetVariableReferences()
                {
                    foreach (var reference in Expression.GetVariableReferences())
                        yield return reference;
                    yield return this;
                }
                protected override void GetNullReferencesInternal(
                    HashSet<Expression> checkedExprs, IDictionary<string, ReferenceType> nullRefs)
                {
                    Expression.GetNullReferences(checkedExprs, nullRefs);
                    GetNullReferencesSubInternal(nullRefs);
                }
                protected abstract void GetNullReferencesSubInternal(IDictionary<string, ReferenceType> dict);

                public override string ToString(int upperLayer) =>
                    upperLayer > Layer ? $"({ToString()})" : ToString();
            }
            public class SuccessPostProcessingReference : PostProcessingReference
            {
                public SuccessPostProcessingReference(Expression expression, Variable variable) :
                    base(expression, variable) => ParseFunc = (cursor, args) =>
                    {
                        var result = Expression.Parse(cursor, args);
                        if (result.Success) Variable.ProcessSuccess(result, cursor, args);
                        result.SrcExpr = this;
                        return result;
                    };

                protected override void GetNullReferencesSubInternal(IDictionary<string, ReferenceType> dict)
                {
                    if (Variable.SuccessProcess is not null) return;
                    if (dict.TryGetValue(Name, out var type))
                        dict[Name] = type | ReferenceType.SuccessProcess;
                    else dict[Name] = ReferenceType.SuccessProcess;
                }

                public override string ToString() => $"{Expression.ToString(Layer)}/{Variable}";
            }
            public class ErrorPostProcessingReference : PostProcessingReference
            {
                public ErrorPostProcessingReference(Expression expression, Variable variable) :
                    base(expression, variable) => ParseFunc = (cursor, args) =>
                    {
                        var result = Expression.Parse(cursor, args);
                        if (result.HasError) Variable.ProcessError(result, cursor, args);
                        result.SrcExpr = this;
                        return result;
                    };

                protected override void GetNullReferencesSubInternal(IDictionary<string, ReferenceType> dict)
                {
                    if (Variable.ErrorProcess is not null) return;
                    if (dict.TryGetValue(Name, out var type))
                        dict[Name] = type | ReferenceType.ErrorProcess;
                    else dict[Name] = ReferenceType.ErrorProcess;
                }

                public override string ToString() => $"{Expression.ToString(Layer)}!{Variable}";
            }

            private Func<StringCursor, Expression[]?, Result> ParseFunc { get; set; }
            public Result Parse(StringCursor cursor, Expression[]? args = null) => ParseFunc(cursor, args);

            private Expression() => ParseFunc = null!;
            public Func<StringCursor, Expression[]?, Result> ToParseFunc(Func<StringCursor, Result> parseFunc) =>
                (cursor, args) =>
                {
                    if (args is not null) return new Result(this, cursor.CreateEmptySection())
                    {
                        Success = false
                    };
                    return parseFunc(cursor);
                };
            public Expression(Func<StringCursor, Result> parseFunc) => ParseFunc = ToParseFunc(parseFunc);
            public Expression(Func<StringCursor, Expression[]?, Result> parseFunc) => ParseFunc = parseFunc;
            public Expression(Func<Expression, StringCursor, Result> parseFunc) =>
                ParseFunc = ToParseFunc(cursor => parseFunc(this, cursor));
            public Expression(Func<Expression, StringCursor, Expression[]?, Result> parseFunc) =>
                ParseFunc = (cursor, args) => parseFunc(this, cursor, args);
            public static Expression FromEbnf(string code)
            {
                StringCursor cursor = code;
                var result = Parser.Expression.Parse(cursor);
                if (result.HasError) throw new FormatException($"Failed to parse EBNF expression: " +
                    result.ErrorMessage ?? "unknown error");
                if (!cursor.EndOfString) throw new FormatException($"Unexpected characters after EBNF expression: \"{code[cursor.Position..]}\"");
                return (Expression)result.Data!;
            }

            public Expression Extend(PostProcess postProcess) => postProcess switch
            {
                SuccessProcess successProcess => new PostProcessingExpression(this, successProcess),
                ErrorProcess errorProcess => new PostProcessingExpression(this, errorProcess),
                _ => throw new NotImplementedException(),
            };
            public Expression Extend(SuccessProcess successProcess) => new PostProcessingExpression(this, successProcess);
            public Expression Extend(ErrorProcess errorProcess) => new PostProcessingExpression(this, errorProcess);

            public virtual Expression? GetSourceExpression() => this;
            public virtual IEnumerable<VariableReference> GetVariableReferences() => [];
            public IDictionary<string, ReferenceType> GetNullReferences(
                HashSet<Expression>? checkedExprs = null, IDictionary<string, ReferenceType>? nullRefs = null)
            {
                checkedExprs ??= [];
                nullRefs ??= new Dictionary<string, ReferenceType>();
                if (checkedExprs.Contains(this)) return nullRefs;
                checkedExprs.Add(this);
                GetNullReferencesInternal(checkedExprs, nullRefs);
                return nullRefs;
            }
            protected virtual void GetNullReferencesInternal(
                HashSet<Expression> checkedExprs, IDictionary<string, ReferenceType> nullRefs)
            { }
            public virtual string ToString(int upperLayer) => ToString() ?? "";

            public enum NumberBase
            {
                None,
                Decimal,
                Hexadecimal,
                Binary,
            }
            public static NumberBase GetNumberBase(Expression arg)
            {
                if (arg is Terminal str)
                {
                    return str.Value switch
                    {
                        BaseDecimalStr => NumberBase.Decimal,
                        BaseHexadecimalStr => NumberBase.Hexadecimal,
                        BaseBinaryStr => NumberBase.Binary,
                        _ => NumberBase.None
                    };

                }
                if (arg is TerminalChar chr)
                {
                    return chr.Value switch
                    {
                        BaseDecimalChar => NumberBase.Decimal,
                        BaseHexadecimalChar => NumberBase.Hexadecimal,
                        BaseBinaryChar => NumberBase.Binary,
                        _ => NumberBase.None
                    };
                }
                return NumberBase.None;
            }
            public const string BaseDecimalStr = "dec";
            public const string BaseHexadecimalStr = "hex";
            public const string BaseBinaryStr = "bin";
            public const char BaseDecimalChar = 'd';
            public const char BaseHexadecimalChar = 'x';
            public const char BaseBinaryChar = 'b';
            public static readonly Func<char, bool> IsDecimalDigit = char.IsAsciiDigit;
            public static readonly Func<char, bool> IsHexadecimalDigit = c =>
            {
                char lower = char.ToLower(c);
                return char.IsAsciiDigit(c) || ('a' <= lower && lower <= 'f');
            };
            public static readonly Func<char, bool> IsBinaryDigit = c => c == '0' || c == '1';
            /// <summary>
            /// Nat ::= Nat("dec") <br/>
            /// Nat(type) ::= digit(type)+ where '_' is allowed<br/>
            /// valid types:<br/>
            /// - long version: "dec", "hex", "bin"<br/>
            /// - short version: 'd', 'x', 'b'<br/>
            /// data = (<see cref="NumberBase"/> numberBase, <see cref="string"/> numberString)
            /// </summary>
            public static Expression Nat { get; } = new((cursor, args) =>
            {
                NumberBase numBase = NumberBase.Decimal;
                Func<char, bool> isDigit;
                if (args is null) isDigit = IsDecimalDigit;
                else if (args.Length != 1) return new Result(Nat!, cursor.CreateEmptySection())
                {
                    Success = false,
                    Data = "",
                };
                else switch (numBase = GetNumberBase(args[0]))
                    {
                        case NumberBase.Decimal: isDigit = IsDecimalDigit; break;
                        case NumberBase.Hexadecimal: isDigit = IsHexadecimalDigit; break;
                        case NumberBase.Binary: isDigit = IsBinaryDigit; break;
                        case NumberBase.None:
                            return new Result(Nat!, cursor.CreateEmptySection())
                            {
                                Success = false,
                                Data = "",
                            };
                        default: throw new Exception();
                    }
                int initialCursor = cursor.Position;
                if (cursor.EndOfString || !isDigit(cursor.CurrentChar))
                    return new Result(Nat!, cursor.CreateEmptySection())
                    {
                        Success = false,
                        Data = "",
                    };
                List<char> digits = [];
                for (; !cursor.EndOfString; cursor++)
                {
                    char c = cursor.CurrentChar;
                    if (c == '_') continue;
                    if (!isDigit(c)) break;
                    digits.Add(c);
                }
                while (initialCursor < cursor.Position && cursor.Source[cursor.Position - 1] == '_') cursor--;
                var section = cursor.CreateSection(initialCursor);
                return new Result(Nat!, section)
                {
                    Data = (numBase, new string([.. digits])),
                };
            });
            /// <summary>
            /// CodeNat ::= prefix? Nat(type) where prefix is '0' followed by a type character ('d', 'x', 'b')<br/>
            /// Note: '_' is allowed in the number string.<br/>
            /// data = (<see cref="NumberBase"/> numberBase, <see cref="string"/> numberString)
            /// </summary>
            public static Expression CodeNat { get; } =
                Repetition.Optional(new Sequence(new TerminalChar('0'),
                    new CharacterClass(BaseDecimalChar, BaseHexadecimalChar, BaseBinaryChar)))
                .Extend(new SuccessProcess((result, cursor) =>
                {
                    TerminalChar tc;
                    var seq = ((Result.Sequence)result).Results;
                    if (seq.Count == 0) tc = BaseDecimalChar;
                    else
                    {
                        var section = ((Result.Sequence)seq[0]).Results[1].StringSection;
                        tc = section.Source[section.Start];
                    }
                    var tmp = Nat.Parse(cursor, [tc]);
                    result.Success = tmp.Success;
                    result.Data = tmp.Data;
                }));
            public static SuccessProcess SignExtension { get; } = new((result, cursor) =>
            {
                // [-+]? Nat 
                var seq = ((Result.Sequence)result).Results;
                var (type, numStr) = ((NumberBase, string))seq[1].Data!;
                int sgn = 0;
                // [-+]?
                seq = ((Result.Sequence)seq[0]).Results;
                if (seq.Count > 0)
                {
                    var section = seq[0].StringSection;
                    char signChar = section.Source[section.Start];
                    sgn = signChar == '-' ? -1 : 1;
                }
                result.Data = (type, sgn, numStr);
            });
            /// <summary>
            /// Int ::= [-+]? Nat <br/>
            /// data = (<see cref="NumberBase"/> numberBase, <see cref="int"/> sign, <see cref="string"/> numberString)
            /// Note: The sign corresponds to the number string, not the final integer value. 
            /// - sign = 0 => no sign character was present. (Note: "0" as well as "1" or "123" will result in sign = 0)
            /// - sign = 1 => a '+' character was present, indicating a positive number.
            /// - sign = -1 => a '-' character was present, indicating a negative number.
            /// </summary>
            public static Expression Int { get; } = new Sequence(
                    Repetition.Optional(new CharacterClass('-', '+')), Nat
                ).Extend(SignExtension);
            /// <summary>
            /// Int ::= [-+]? CodeNat <br/>
            /// data = (<see cref="NumberBase"/> numberBase, <see cref="int"/> sign, <see cref="string"/> numberString)
            /// Note: The sign corresponds to the number string, not the final integer value. 
            /// - sign = 0 => no sign character was present. (Note: "0" as well as "1" or "123" will result in sign = 0)
            /// - sign = 1 => a '+' character was present, indicating a positive number.
            /// - sign = -1 => a '-' character was present, indicating a negative number.
            /// </summary>
            public static Expression CodeInt { get; } = new Sequence(
                    Repetition.Optional(new CharacterClass('-', '+')), CodeNat
                ).Extend(SignExtension);
            public static Expression Float { get; } = new Expression(cursor =>
            {
                int initialCursor = cursor.Position;
                List<string> parts = [];
                bool success;
                Result getResult() => new(Float!, cursor.CreateSection(initialCursor))
                {
                    Success = success,
                    Data = string.Concat(parts)
                };
                var result = Int.Parse(cursor);
                if (success = result.Success)
                {
                    var (numBase, sgn, str) = ((NumberBase, int sgn, string str))result.Data!;
                    if (sgn != 0) parts.Add(sgn < 0 ? "-" : "+");
                    parts.Add(str);
                }
                if (cursor.EndOfString) return getResult();
                if (cursor.CurrentChar == '.')
                {
                    cursor++;
                    parts.Add(".");
                    result = Nat.Parse(cursor);
                    if (!result.Success) { if (!success) return getResult(); }
                    else
                    {
                        success = true;
                        parts.Add((((NumberBase, string str))result.Data!).str);
                    }
                }
                if (cursor.EndOfString || !success) return getResult();
                if (cursor.CurrentChar is 'e' or 'E')
                {
                    parts.Add(cursor.ReadChar().ToString());
                    result = Int.Parse(cursor);
                    if (result.Success)
                    {
                        var (_, sgn, str) = ((NumberBase, int sgn, string str))result.Data!;
                        if (sgn != 0) parts.Add(sgn < 0 ? "-" : "+");
                        parts.Add(str);
                    }
                    else
                    {
                        cursor--;
                        parts.RemoveAt(parts.Count - 1);
                    }
                }
                return getResult();
            });
            public static SuccessProcess GetNatParseExtension(
                Func<string, NumberStyles, (bool error, object num)> parseNum) => new((result, cursor) =>
                {
                    var (numBase, s) = ((NumberBase, string))result.Data!;
                    NumberStyles style = numBase switch
                    {
                        NumberBase.Decimal => NumberStyles.None,
                        NumberBase.Hexadecimal => NumberStyles.AllowHexSpecifier,
                        NumberBase.Binary => NumberStyles.AllowBinarySpecifier,
                        _ => throw new Exception(),
                    };
                    (bool error, result.Data) = parseNum(s, style);
                    if (error) result.ErrorMessage =
                        $"- The number {s} is too large to fit in the target type. (Type = {result.Data.GetType()})";
                });
            public static Expression Nat_int { get; } =
                Nat.Extend(GetNatParseExtension(
                    (str, style) => (!int.TryParse(str, style, null, out int num) || num < 0, num)));
            public static Expression CodeNat_int { get; } =
                CodeNat.Extend(GetNatParseExtension(
                    (str, style) => (!int.TryParse(str, style, null, out int num) || num < 0, num)));
            public static SuccessProcess GetIntParseExtension(Func<object, int> getSign,
                Func<string, NumberStyles, (bool error, object num)> parseNum) => new((result, cursor) =>
                {
                    var (numBase, sgn, s) = ((NumberBase, int, string))result.Data!;
                    NumberStyles style = numBase switch
                    {
                        NumberBase.Decimal => NumberStyles.AllowLeadingSign,
                        NumberBase.Hexadecimal => NumberStyles.AllowHexSpecifier,
                        NumberBase.Binary => NumberStyles.AllowBinarySpecifier,
                        _ => throw new Exception(),
                    };
                    if (sgn < 0 && style == NumberStyles.AllowLeadingSign) s = $"-{s}";
                    (bool error, result.Data) = parseNum(s, style);
                    if (sgn < 0 && style != NumberStyles.AllowLeadingSign) result.Data = -(int)result.Data!;
                    int numSgn = getSign(result.Data!);
                    if (error || numSgn != 0 && (sgn < 0) != (numSgn < 0)) result.ErrorMessage =
                        $"- The number {s} is too large to fit in the target type. (Type = {result.Data.GetType()})";
                });
            public static Expression Int_int { get; } =
                Int.Extend(GetIntParseExtension(obj => int.Sign((int)obj),
                    (str, style) => (!int.TryParse(str, style, null, out int num), num)));
            public static Expression CodeInt_int { get; } =
                CodeInt.Extend(GetIntParseExtension(obj => int.Sign((int)obj),
                    (str, style) => (!int.TryParse(str, style, null, out int num), num)));
            public static Expression Float_double { get; } =
                Float.Extend(new SuccessProcess((result, cursor) =>
                {
                    var s = (string)result.Data!;
                    result.Data = double.TryParse(s, CultureInfo.InvariantCulture, out double num) ? num : double.NaN;
                    if (double.IsNaN((double)result.Data!)) result.ErrorMessage =
                        $"- The number {s} is too large to fit in a double.";
                }));

            public static Expression WhiteSpace { get; } = new CharacterClass(char.IsWhiteSpace);
            public static Expression WhiteSpaceStar { get; } = Repetition.ZeroOrMore(WhiteSpace);
            public static Expression WhiteSpacePlus { get; } = Repetition.OneOrMore(WhiteSpace);
            public static Expression WhiteSpaceLine { get; } = new CharacterClass(c => char.IsWhiteSpace(c) && c != '\n');
            public static Expression WhiteSpaceLineStar { get; } = Repetition.ZeroOrMore(WhiteSpaceLine);
            public static Expression WhiteSpaceLinePlus { get; } = Repetition.OneOrMore(WhiteSpaceLine);
            public static Expression EndOfString { get; } = new(cursor =>
                new Result(EndOfString!, cursor.CreateEmptySection()) { Success = cursor.EndOfString }
            );
            public static Expression Null { get; } = new Expression(_ => throw new InvalidOperationException("Null expression cannot parse."));

            /// <summary>
            /// Represents a placeholder character used to indicate an unknown or invalid character value.
            /// </summary>
            /// <remarks>This constant is commonly used as a sentinel value when a character cannot be
            /// determined or is not recognized during parsing or processing operations.</remarks>
            public const char UnknownChar = (char)26;
            public static Expression OpenChar { get; } = new(cursor =>
            {
                Result result;
                int initialCursor = cursor.Position;
                if (cursor.EndOfString)
                {
                    return new Result(OpenChar!, cursor.CreateSection(initialCursor))
                    {
                        Success = false,
                    };
                }
                char c = cursor.ReadChar();
                if (c != '\\') return new Result(OpenChar!, cursor.CreateSection(initialCursor))
                {
                    Data = c,
                };
                if (cursor.EndOfString)
                {
                    return new Result(OpenChar!, cursor.CreateSection(initialCursor))
                    {
                        Success = false,
                    };
                }
                switch (cursor.ReadChar())
                {
                    case '0': c = '\0'; break;
                    case 'a': c = '\a'; break;
                    case 'b': c = '\b'; break;
                    case 'e': c = '\e'; break;
                    case 'f': c = '\f'; break;
                    case 'n': c = '\n'; break;
                    case 'r': c = '\r'; break;
                    case 't': c = '\t'; break;
                    case 'v': c = '\v'; break;
                    case '\\': c = '\\'; break;
                    case '\'': c = '\''; break;
                    case '"': c = '"'; break;
                    case 'u':
                        {
                            if (cursor.Position + 4 > cursor.Source.Length)
                            {
                                cursor.Position = cursor.Source.Length;
                                return new Result(OpenChar!, cursor.CreateSection(initialCursor))
                                {
                                    Success = false,
                                };
                            }
                            string hex = cursor.Source.Substring(cursor.Position, 4);
                            if (!int.TryParse(hex, NumberStyles.AllowHexSpecifier, null, out int codePoint))
                            {
                                result = new Result(OpenChar!, cursor.CreateSection(initialCursor))
                                {
                                    ErrorMessage = $"Invalid Unicode escape sequence '\\u{hex}'.",
                                    Data = UnknownChar
                                };
                                result.ErrorMessage += $" ({result.GetContext()})";
                                return result;
                            }
                            c = (char)codePoint;
                            cursor.Position += 4;
                            break;
                        }
                    default:
                        result = new Result(OpenChar!, cursor.CreateSection(initialCursor))
                        {
                            ErrorMessage = $"Unknown escape sequence '\\{cursor.Source[cursor.Position - 1]}'.",
                            Data = cursor.Source[cursor.Position - 1]
                        };
                        result.ErrorMessage += $" ({result.GetContext()})";
                        return result;
                }
                return new Result(OpenChar!, cursor.CreateSection(initialCursor))
                {
                    Data = c,
                };
            });
            public static Expression GetOpenString(params PostProcess[] postProcesses)
            {
                Expression expr = OpenChar;
                foreach (var postProcess in postProcesses) expr = expr.Extend(postProcess);
                return Repetition.ZeroOrMore(expr).Extend(new SuccessProcess((result, cursor) =>
                {
                    // OpenChar*
                    var seq = ((Result.Sequence)result).Results;
                    result.Data = new string([.. seq.Select(x => (char)x.Data!)]);
                }));
            }
            /// <summary>
            /// Char ::= '\'' OpenChar '\''
            /// </summary>
            public static Expression Char { get; } =
                new Sequence(new TerminalChar('\''), OpenChar, new TerminalChar('\''))
                .Extend(new SuccessProcess((result, cursor) =>
                {
                    var seq = (Result.Sequence)result;
                    if (seq.Results[1].StringSection.Section == "'")
                    {
                        result.Success = false;
                        cursor.Position = result.StringSection.Start;
                    }
                    else result.Data = seq.Results[1].Data;
                }));
            /// <summary>
            /// String ::= '"' OpenChar* '"'
            /// </summary>
            public static Expression String { get; } = new Sequence(new TerminalChar('"'),
                GetOpenString(new SuccessProcess((result, cursor) =>
                {
                    if (result.StringSection.Section == "\"")
                    {
                        result.Success = false;
                        cursor.Position = result.StringSection.Start;
                    }
                })),
                new TerminalChar('"')).Extend(new SuccessProcess((result, cursor) =>
                {
                    // '"' OpenString '"'
                    var seq = ((Result.Sequence)result).Results;
                    result.Data = seq[1].Data!;
                }));
            /// <summary>
            /// String ::= '\'' OpenChar* '\''
            /// </summary>
            public static Expression SingleQuotedString { get; } = new Sequence(new TerminalChar('\''),
                GetOpenString(new SuccessProcess((result, cursor) =>
                {
                    if (result.StringSection.Section == "'")
                    {
                        result.Success = false;
                        cursor.Position = result.StringSection.Start;
                    }
                })),
                new TerminalChar('\'')).Extend(new SuccessProcess((result, cursor) =>
                {
                    // '\'' OpenString '\''
                    var seq = ((Result.Sequence)result).Results;
                    result.Data = seq[1].Data!;
                }));
            /// <summary>
            /// VariableName ::= [a-zA-Z] [a-zA-Z0-9_]*
            /// </summary>
            public static Expression VariableName { get; } = new Expression(cursor =>
            {
                if (cursor.EndOfString) return new Result(VariableName!, cursor.CreateEmptySection())
                {
                    Success = false,
                };
                int initialCursor = cursor.Position;
                if (!char.IsAsciiLetter(cursor.ReadChar()))
                    return new Result(VariableName!, (--cursor).CreateEmptySection())
                    {
                        Success = false,
                    };
                while (!cursor.EndOfString && (char.IsAsciiLetterOrDigit(cursor.CurrentChar) || cursor.CurrentChar == '_'))
                    cursor++;
                var section = cursor.CreateSection(initialCursor);
                return new Result(VariableName!, section);
            });
        }
        public abstract class PostProcess
        {
            public Action<Result, StringCursor, Expression[]?> Process { get; }

            public PostProcess(Action<Result, StringCursor, Expression[]?> processSuccess) =>
                Process = processSuccess;
            public PostProcess(Action<Result, StringCursor> processSuccess) =>
                Process = (result, cursor, _) => processSuccess(result, cursor);
        }
        public class SuccessProcess : PostProcess
        {
            public SuccessProcess(Action<Result, StringCursor, Expression[]?> processSuccess) : base(processSuccess) { }
            public SuccessProcess(Action<Result, StringCursor> processSuccess) : base(processSuccess) { }
        }
        public class ErrorProcess : PostProcess
        {
            public ErrorProcess(Action<Result, StringCursor, Expression[]?> processError) : base(processError) { }
            public ErrorProcess(Action<Result, StringCursor> processError) : base(processError) { }
        }

        public class Result(Expression sourceExpr, StringSection stringSection)
        {
            public class Sequence(Expression expression, StringSection stringSection) : Result(expression, stringSection)
            {
                public List<Result> Results
                {
                    get;
                    init
                    {
                        field = value;
                        foreach (var result in value)
                        {
                            if (result.Parent is not null) throw new ArgumentException("A result cannot be part of multiple sequences.", nameof(value));
                            result.Parent = this;
                        }
                    }
                } = [];

                public override Result GetLast() => Results.Count == 0 ? this : Results.Last().GetLast();
                public override IEnumerable<Result> GetErrors() => Results.Where(r => r.HasError);
            }

            public Result Root => Parent is null ? this : Parent.Root;
            public Result? Parent { get; set; }
            public List<Expression> SourceExpressions { get; } = sourceExpr is null ? [] : [sourceExpr];
            public Expression SrcExpr
            {
                get;
                set
                {
                    field = value;
                    if (!SourceExpressions.Contains(value)) SourceExpressions.Add(value);
                }
            } = sourceExpr!;
            public StringSection StringSection { get; set; } = stringSection;
            /// <summary>
            /// Success is used for standard EBNF-expressions.<br/>
            /// Examples:<br/>
            /// - ``a b c´´ is successful, if a b and c are successful.<br/>
            /// - ``a | b | c´´ is successful, if at least one of a, b or c is successful.<br/>
            /// </summary>
            public bool Success { get; set; } = true;
            /// <summary>
            /// Error is used to indicate, that an error occurred during parsing.<br/>
            /// Note: An error can occur, even if the parsing was successful.
            /// This might happen, when the parsed code contains a syntax error, but the parser is able to recover from it and continue parsing.<br/>
            /// Examples:<br/>
            /// - ``a b c´´ suppose a b and c are successful, but b contains a syntax error. Then the parsing is successful, but an error occurred.<br/>
            /// - ``a | b | c´´ suppose a is successful, but contains a syntax error. Then the search will stop at a.<br/>
            /// That's why it is important to be careful. An error should only be set as successful, if it is guaranteed that no other case can succeed without errors.<br/>
            /// </summary>
            public bool HasError => !Success || ErrorMessage is not null;
            public string? ErrorMessage { get; set; }
            public object? Data { get; set; }
            /// <summary>
            /// Gets the context of this result.
            /// </summary>
            /// <returns>"{section}" at line {lineNumber} "{line}"</returns>
            public string GetContext()
            {
                int i = StringSection.Start == 0 ? 0 :
                    StringSection.Source.LastIndexOf('\n', StringSection.Start - 1) + 1;
                int j = StringSection.Source.IndexOf('\n', StringSection.End);
                if (j < 0) j = StringSection.Source.Length;
                string line = StringSection.Source[i..j];
                int lineNumber = StringSection.Source[..i].Count(c => c == '\n') + 1;
                int cnt = line.Count(c => c == '\n');
                return $"\"{StrHelper.StringAsString(StringSection.Section)}\" at line " +
                    $"{(cnt == 0 ? lineNumber.ToString() : $"{lineNumber}..{lineNumber + cnt}")} " +
                    $"\"{StrHelper.StringAsString(line)}\"";
            }

            public virtual Result GetLast() => this;
            public virtual IEnumerable<Result> GetErrors()
            {
                if (HasError) yield return this;
            }
            public Result? FindParant(Func<Result, bool> predicate) => Parent?.FindThisOrParant(predicate);
            public Result? FindThisOrParant(Func<Result, bool> predicate)
            {
                Result? current = this;
                while (current is not null)
                {
                    if (predicate(current)) return current;
                    current = current.Parent;
                }
                return null;
            }

            public override string ToString() => StringSection.Section;
        }
    }
}
