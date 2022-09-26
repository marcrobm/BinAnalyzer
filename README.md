# BinAnalyzer
A C# Console Programm which provides tools to semi-automatic ly find out the endianness and length in Bytes as well as offset used in a firmware file.
# How to use
The program displays an overview of all commands.
At first findStrings is most useful to see how many strings there are in the Binary.
Before using FindOF load a file with load [numberOfStrings]. The computation time is linear to number OfStrings.
For larger binaries (over 1MB) it tends to be a good idea to use more (at least a quater of the total) strings or at least ~100, which unfortunately results in long runtimes.
(100Strings, 1MB => ~8hours)
The larger addressSpace/fileSize the better the results.
When using FindOF [max] the time taken to process the file is linear to max;
