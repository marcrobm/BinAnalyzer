using System;
using System.IO;
namespace BinAnalyzer
{
    class Program
    {
        static string helpInformation = "----------------------------HELP-------------------------------------------------------\n" +
            "help          -show this help page\n" +
            "showStrings   -find all string in the binary and display them\n" +
            "exit          -exit programm\n" +
            "findOF [max]  -find the offset and number format used in the binary to reference strings\n" +
            "---------------------------------------------------------------------------------------\n";
        static void Main(string[] args)
        {
            string romPath = "";
            if (args.Length >= 1)
            {
                romPath = args[0];
                if (!File.Exists(romPath))
                {
                    Console.WriteLine("the specified file does not exist, exiting\n" + helpInformation);
                    return;
                }
            }
            else
            {
                while (!File.Exists(romPath))
                {
                    Console.WriteLine("Specify the file to analyze");
                    romPath = Console.ReadLine();
                }
            }
            Console.WriteLine("File selected");
            Console.Write("Started Binary Analyzer\n" + helpInformation);
            var analyzer = new Analyzer(args[0]);

            while (true)
            {
                string input = Console.ReadLine();
                string cmd = input.Split(' ')[0];
                string[] cargs = input.Split(' ');
                switch (cmd)
                {
                    case "help":
                        Console.Write(helpInformation);
                        break;
                    case "exit":
                        return;
                    case "showStrings":
                        var strings = analyzer.FindStrings();
                        Console.WriteLine("Found "+strings.Count+" strings");
                        Console.WriteLine("Position | Text");
                        foreach (var s in strings)
                        {
                            Console.WriteLine("{0,8:X4} | {1}",s.Key,s.Value);
                        }
                        break;
                    case "findOF":
                        var res = analyzer.FindNumberFormatAndOffset(0,int.Parse(cargs[1]), 1,8);
                        Console.WriteLine("The most likeley format and offset is "+res.Item1+" with a offset of "+res.Item2);
                        break;
                    default:
                        Console.WriteLine("Unknown command, type help and press enter for help");
                        break;

                }
            }
        }
    }
}
