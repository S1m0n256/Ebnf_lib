using System;
using System.Globalization;

namespace Ebnf_lib.Examples
{
    /// <summary>
    /// Demonstrates parsing and evaluating mathematical expressions using EBNF grammar.
    ///
    /// This example shows how to:
    /// - Define operator precedence (+/- lower priority than */)
    /// - Build an evaluator that processes expressions recursively
    /// - Handle nested expressions with parentheses
    /// - Use post-processing to compute results during parsing
    /// </summary>
    public static class CalculatorExample
    {
        public static void Run()
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            Ebnf.Parser parser = new("expr");

            // Setup basic expressions
            parser.AddExpression("s", Ebnf.Expression.WhiteSpace);
            parser.AddExpression("number", Ebnf.Expression.Float_double);

            // Define the grammar with operator precedence
            // expr1 handles addition/subtraction (lower precedence)
            // expr2 handles multiplication/division (higher precedence)
            // expr3 handles parentheses and numbers
            parser.AddEbnfCode(
                "expr1 ::= expr2 ( s* [+-] s* expr2 )*",
                "expr2 ::= expr3 ( s* [*/] s* expr3 )*",
                "expr3 ::= ( '(' s* expr s* ')' )/bracket | number",
                "expr ::= s* expr1 s*"
            );

            // Post-process expr1 to compute addition/subtraction
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
                                _ => throw new InvalidOperationException(
                                    $"Unsupported operator: {op}"
                                ),
                            };
                        }
                        result.Data = value;
                    }
                )
            );

            // Post-process expr2 to compute multiplication/division
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
                                _ => throw new InvalidOperationException(
                                    $"Unsupported operator: {op}"
                                ),
                            };
                        }
                        result.Data = value;
                    }
                )
            );

            // Post-process bracket to extract the inner value
            parser.AddSuccessProcess(
                "bracket",
                new Ebnf.SuccessProcess(
                    (result, cursor) =>
                    {
                        // '(' s* expr s* ')'
                        var seq = ((Ebnf.Result.Sequence)result).Results;
                        result.Data = seq[2].Data;
                    }
                )
            );

            // Post-process top-level expr to extract expr1 value
            parser.AddSuccessProcess(
                "expr",
                new Ebnf.SuccessProcess(
                    (result, cursor) =>
                    {
                        // s* expr1 s*
                        var seq = ((Ebnf.Result.Sequence)result).Results;
                        result.Data = seq[1].Data;
                    }
                )
            );

            Console.WriteLine("=== Mathematical Expression Calculator ===");
            Console.WriteLine("Supports: +, -, *, / and parentheses");
            Console.WriteLine("Enter expressions or 'exit' to quit.\n");

            while (true)
            {
                Console.Write("Enter an expression: /> ");
                string? input = Console.ReadLine();

                if (
                    input == null
                    || input.Trim().Equals("exit", StringComparison.CurrentCultureIgnoreCase)
                )
                    break;

                var result = parser.Parse(input);

                if (!result.HasError)
                {
                    try
                    {
                        double resultValue = (double)result.Data!;
                        Console.WriteLine($"✓ Result: {resultValue}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"✗ Error computing result: {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine($"✗ Parsing failed!");
                    Console.WriteLine($"  Error: {result.ErrorMessage ?? "Unknown error"}");
                    Console.WriteLine($"  Context: {result.GetContext()}");
                }

                Console.WriteLine();
            }

            Console.WriteLine("Goodbye!");
        }
    }
}
