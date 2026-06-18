using System;
using System.Globalization;
using System.Numerics;

namespace Ebnf_lib.Examples
{
    /// <summary>
    /// Demonstrates parsing hexadecimal numbers using EBNF grammar.
    ///
    /// This example shows how to:
    /// - Define a simple grammar for hexadecimal numbers (0x prefix + hex digits)
    /// - Parse input and convert it to decimal
    /// - Handle parsing errors with meaningful messages
    /// </summary>
    public static class HexNumberExample
    {
        public static void Run()
        {
            Ebnf.Parser parser = new("main");

            // Define the grammar for hexadecimal numbers
            // digit ::= [0-9A-Fa-f]    - matches single hex digit
            // hex ::= digit+            - matches one or more hex digits (not zero!)
            // main ::= "0x" hex         - matches "0x" followed by hex digits
            parser.AddEbnfCode("digit ::= [0-9A-Fa-f]", "hex ::= digit+", "main ::= \"0x\" hex");

            Console.WriteLine("=== Hexadecimal Number Parser ===");
            Console.WriteLine("Enter hexadecimal numbers (with '0x' prefix) or 'exit' to quit.\n");

            while (true)
            {
                Console.Write("Enter a hexadecimal number: /> ");
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
                        // Extract the hex digits part (skip "0x" prefix)
                        string hexDigits = ((Ebnf.Result.Sequence)result)
                            .Results[1]
                            .StringSection
                            .Section;

                        if (
                            BigInteger.TryParse(
                                hexDigits,
                                NumberStyles.HexNumber,
                                CultureInfo.InvariantCulture,
                                out BigInteger number
                            )
                        )
                        {
                            Console.WriteLine($"✓ Parsed successfully!");
                            Console.WriteLine($"  Hexadecimal: 0x{hexDigits}");
                            Console.WriteLine($"  Decimal:     {number}");
                        }
                        else
                        {
                            Console.WriteLine(
                                $"✗ Failed to convert hexadecimal number: {hexDigits}"
                            );
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"✗ Error processing result: {ex.Message}");
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
