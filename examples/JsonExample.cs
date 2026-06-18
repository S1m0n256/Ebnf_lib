using System;
using System.Collections.Generic;
using System.Globalization;

namespace Ebnf_lib.Examples
{
    /// <summary>
    /// Demonstrates parsing JSON-like structures using EBNF grammar.
    ///
    /// This example shows how to:
    /// - Define complex, nested grammars (arrays, objects, primitives)
    /// - Use lambdas to parameterize rules (csl = comma-separated list)
    /// - Process parsed results into .NET objects (Dictionary, Array, etc.)
    /// - Implement recursive data structures (nested objects/arrays)
    /// - Validate parsed data (no duplicate keys)
    ///
    /// Note: Supports both double and single-quoted strings (JSON-like, not strict JSON).
    /// </summary>
    public static class JsonExample
    {
        public static void Run()
        {
            Ebnf.Parser parser = new();

            // Setup basic building blocks
            parser.AddExpression("s", Ebnf.Expression.WhiteSpace);
            parser.AddExpression(
                "string",
                new Ebnf.Expression.Choice(
                    Ebnf.Expression.String,
                    Ebnf.Expression.SingleQuotedString
                )
            );

            // Define JSON-like grammar
            parser.AddEbnfCode(
                "null ::= \"null\"",
                "bool ::= \"true\"/true | \"false\"/false",
                "simple ::= string | null | bool",
                "csl(arg) ::= arg ( s* ',' s* arg )*",
                "array ::= '[' s* csl( obj )?/opt s* ']'",
                "dict ::= '{' s* csl( ( string s* ':' s* obj )/pair )?/opt s* '}'",
                "obj ::= simple | array | dict",
                "main ::= s* obj s*"
            );

            parser.MainVariable = "main";

            // Post-process main to verify end-of-string
            parser.AddSuccessProcess(
                "main",
                new Ebnf.SuccessProcess(
                    (result, cursor) =>
                    {
                        if (!cursor.EndOfString)
                        {
                            result.Success = false;
                            result.ErrorMessage =
                                $"Unexpected character '{cursor.CurrentChar}' at position {cursor.Position}.";
                        }
                        else
                        {
                            // s* obj s*
                            var seq = ((Ebnf.Result.Sequence)result).Results;
                            result.Data = seq[1].Data;
                        }
                    }
                )
            );

            // Post-process bool values
            parser.AddSuccessProcess(
                "true",
                new Ebnf.SuccessProcess((result, cursor) => result.Data = true)
            );
            parser.AddSuccessProcess(
                "false",
                new Ebnf.SuccessProcess((result, cursor) => result.Data = false)
            );

            // Post-process optional lists
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

            // Post-process key-value pairs
            parser.AddSuccessProcess(
                "pair",
                new Ebnf.SuccessProcess(
                    (result, cursor) =>
                    {
                        // string s* ':' s* obj
                        var seq = ((Ebnf.Result.Sequence)result).Results;
                        string key = (string)seq[0].Data!;
                        object value = seq[4].Data!;
                        // data = (string key, object value)
                        result.Data = (key, value);
                    }
                )
            );

            // Post-process comma-separated lists
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
                        // data = List<object?>
                        result.Data = args;
                    }
                )
            );

            // Post-process arrays
            parser.AddSuccessProcess(
                "array",
                new Ebnf.SuccessProcess(
                    (result, cursor) =>
                    {
                        // '[' s* csl( obj )?/opt s* ']'
                        var seq = ((Ebnf.Result.Sequence)result).Results;
                        List<object?> items = (List<object?>)seq[2].Data!;
                        // data = object[]
                        result.Data = items.ToArray();
                    }
                )
            );

            // Post-process objects (dictionaries)
            parser.AddSuccessProcess(
                "dict",
                new Ebnf.SuccessProcess(
                    (result, cursor) =>
                    {
                        // '{' s* csl( ( string s* ':' s* obj )/pair )?/opt s* '}'
                        var seq = ((Ebnf.Result.Sequence)result).Results;
                        // data = List<object?> where each item is (string key, object value)
                        IEnumerable<(string key, object value)> pairs = (
                            (List<object?>)seq[2].Data!
                        ).Cast<(string, object)>();
                        Dictionary<string, object> dict = [];
                        foreach (var (key, value) in pairs)
                        {
                            if (dict.ContainsKey(key))
                            {
                                result.ErrorMessage = $"Duplicate key \"{key}\" in object.";
                                return;
                            }
                            dict[key] = value;
                        }
                        // data = Dictionary<string, object>
                        result.Data = dict;
                    }
                )
            );

            Console.WriteLine("=== JSON-Like Parser ===");
            Console.WriteLine("Supports objects, arrays, strings, booleans, null");
            Console.WriteLine("Enter JSON or 'exit' to quit.\n");

            while (true)
            {
                Console.Write("Enter JSON: /> ");
                string? input = Console.ReadLine();

                if (
                    input == null
                    || input.Trim().Equals("exit", StringComparison.CurrentCultureIgnoreCase)
                )
                    break;

                var result = parser.Parse(input);

                if (!result.HasError)
                {
                    Console.WriteLine($"✓ Parsed successfully!");
                    Console.WriteLine($"  Type: {result.Data?.GetType().Name ?? "Null"}");
                    Console.WriteLine($"  Value: {FormatValue(result.Data)}");
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

        private static string FormatValue(object? obj) =>
            obj switch
            {
                null => "null",
                bool b => b ? "true" : "false",
                string s => $"\"{s}\"",
                object[] arr => $"Array[{arr.Length}]",
                Dictionary<string, object> dict => $"Object{{{dict.Count} properties}}",
                _ => obj.ToString() ?? "?",
            };
    }
}
