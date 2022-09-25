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
            "load [path]   -load a different file\n" +
            "clear         -clears screen\n" +
            "---------------------------------------------------------------------------------------\n";
        static void Main(string[] args)
        {
            Console.SetBufferSize(Console.BufferWidth, 32766);
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
            Analyzer analyzer;
            if (args.Length == 2)
            {
                analyzer = new Analyzer(romPath, int.Parse(args[1]));
            }
            else
            {
                analyzer = new Analyzer(romPath, 20);
            }

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
                        Console.WriteLine("Found " + strings.Count + " strings");
                        Console.WriteLine("Position | Text");
                        foreach (var s in strings)
                        {
                            Console.WriteLine("{0,8:X4} | {1}", s.Key, s.Value);
                        }
                        break;
                    case "findOF":
                        var res = analyzer.FindNumberFormatAndOffset(0, int.Parse(cargs[1]), 1, 8);
                        Console.WriteLine("The most likely format is {0} with offset {1:X}(hex)", res.format, res.offset);
                        break;
                    case "load":
                        if (File.Exists(cargs[1]))
                        {
                            romPath = cargs[1];
                            if (cargs.Length == 3)
                            {
                                analyzer = new Analyzer(romPath, int.Parse(cargs[2]));
                                Console.WriteLine("Loaded {0} considering {1} strings", romPath, int.Parse(cargs[2]));
                            }
                            else
                            {
                                analyzer = new Analyzer(romPath);
                                Console.WriteLine("Loaded {0}", romPath);
                            }
                        }
                        else
                        {
                            Console.WriteLine("The specified file does not exist, keeping old file loaded");
                        }
                        break;
                    case "clear":
                        Console.Clear();
                        break;
                    default:
                        Console.WriteLine("Unknown command, type help and press enter for help");
                        break;

                }
            }
        }
    }
}
