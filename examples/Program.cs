using System;

namespace Ebnf_lib.Examples
{
    public static class Program
    {
        public static void Main()
        {
            while (true)
            {
                Console.WriteLine("=== Ebnf_lib Examples ===");
                Console.WriteLine("1) Hex Number Example");
                Console.WriteLine("2) Calculator Example");
                Console.WriteLine("3) JSON Example");
                Console.WriteLine("q) Quit");
                Console.Write("Select an option: ");

                string? choice = Console.ReadLine()?.Trim().ToLowerInvariant();
                Console.WriteLine();

                if (choice == "q")
                {
                    Console.WriteLine("Goodbye.");
                    return;
                }

                try
                {
                    switch (choice)
                    {
                        case "1":
                            HexNumberExample.Run();
                            break;
                        case "2":
                            CalculatorExample.Run();
                            break;
                        case "3":
                            JsonExample.Run();
                            break;
                        default:
                            Console.WriteLine("Unknown option. Please use 1, 2, 3, or q.");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Example failed: {ex.Message}");
                }

                Console.WriteLine();
            }
        }
    }
}
