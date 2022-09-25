using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TI30XDev;

namespace BinAnalyzer
{
    public class Analyzer
    {
        Dictionary<int, Dictionary<UInt32, List<UInt32>>>[] offsetToReferencesTable;
        byte[] rom;
        Dictionary<UInt32, string> foundStrings;
        Mutex writeLock = new Mutex(); // locks console printing
        public Analyzer(string path)
        {
            rom = File.ReadAllBytes(path);
            foundStrings = FindStrings(30);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="minOffset"></param>
        /// <param name="maxOffset"></param>
        /// <param name="step"></param>
        /// <param name="minStrLength"></param>
        /// <returns>format, offset</returns>
        public Tuple<string, int> FindNumberFormatAndOffset(int minOffset, int maxOffset, int step, int minStrLength)
        {
            int mostMatches = 0;
            int bestOffset = 0;
            string bestformat = "unknown";
            foreach (var format in NumFormats.formats)
            {
                Console.WriteLine("Now testing " + format.Key);
                var result = findOffset(minOffset, maxOffset, step, format.Value, 1);
                if (result.Item2 > mostMatches)
                {
                    mostMatches = result.Item2;
                    bestOffset = result.Item1;
                    bestformat = format.Key;
                }
            }
            return new Tuple<string, int>(bestformat, bestOffset);
        }

        /// <summary>
        /// Finds all the strings in the rom
        /// </summary>
        /// <param name="minStrLength">the minimum length required for a string to be recognized</param>
        /// <returns>A dictionary which maps an address to a found string </returns>
        public Dictionary<UInt32, String> FindStrings(int maxStrings = int.MaxValue, int minStrLength = 5, double minEntropy = 0.3, double maxEntropy = 0.5)
        {
            Dictionary<UInt32, string> found = new Dictionary<UInt32, string>();
            UInt32 pos = 0;
            string currentString = "";
            while (pos < rom.Length)
            {
                if (isAsci(rom[pos]))
                {
                    currentString += (char)rom[pos];
                }
                else
                {
                    if (currentString.Length >= minStrLength && rom[pos] == 0)
                    {
                        double strEntropy = getEntropy(Encoding.ASCII.GetBytes(currentString));
                        if (strEntropy >= minEntropy && strEntropy <= maxEntropy)
                        {
                            found.Add((UInt32)(pos - currentString.Length), currentString);
                        }
                    }
                    currentString = "";
                }
                pos++;
            }
            return found.OrderBy(x => x.Value.Length).Take(maxStrings).ToDictionary(pair => pair.Key, pair => pair.Value);
        }

        /// <summary>
        /// Finds 
        /// </summary>
        /// <param name="minOffset"> The minimum offset applied to numbers</param>
        /// <param name="maxOffset"> The maximum offset applied to numbers</param>
        /// <param name="stepSize"> The granuality at which to calculate the offset (2 => Offset%2=0 etc)</param>
        /// <param name="numbers"> The addresses (string locations) to search for</param>
        /// <param name="debug"> Wheather or not to print debug messages</param>
        /// <param name="numberFormat"> A Method on how to turn the numbers into binary (found in NumberFormats)</param>
        /// <returns> Dict(offset,Dict(address,List(referencedBy)))
        /// </returns>
        Dictionary<int, Dictionary<UInt32, List<UInt32>>> FindAllReferencesForAllOffsets(int minOffset, int maxOffset, int stepSize, List<UInt32> numbers, bool debug, Func<long, byte[]> numberFormat)
        {
            Dictionary<int, Dictionary<UInt32, List<UInt32>>> ReferencesPerOffset = new Dictionary<int, Dictionary<UInt32, List<UInt32>>>();
            var start = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            int currentOffset = minOffset;
            long lastDebugOutputMillis = 0;
            // for every offset
            while (currentOffset <= maxOffset)
            {
                // find the total number of times one of the provided numbers appear in the rom
                ReferencesPerOffset.Add(currentOffset, FindReferences(rom, numbers, numberFormat, currentOffset));
                currentOffset += stepSize;
                if (debug && lastDebugOutputMillis < DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - 500)
                {
                    lastDebugOutputMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    var timeSinceStart = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - start;
                    var processedOffsets = (currentOffset - minOffset) / stepSize;
                    var leftToProcess = (maxOffset - minOffset) / stepSize - processedOffsets;
                    Console.WriteLine("  processing:" + currentOffset +
                     " eta:" + DateTimeOffset.FromUnixTimeMilliseconds((leftToProcess / processedOffsets) * timeSinceStart + start).LocalDateTime);
                    Console.SetCursorPosition(0, Console.CursorTop - 1);
                }
            }
            return ReferencesPerOffset;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <param name="step"></param>
        /// <param name="minStrLength"></param>
        /// <param name="numFormat"></param>
        /// <returns>offset,matches</returns>
        public Tuple<int, int> findOffset(int min, int max, int step, Func<long, byte[]> numFormat, int maxKeyAttribution)
        {
            Mutex offsetToReferencesMTX = new Mutex();
            Dictionary<int, Dictionary<UInt32, List<UInt32>>> offsetToReferences = new Dictionary<int, Dictionary<UInt32, List<UInt32>>>();

            // for each offset, find the locations referencing every string
            int scanSizePerThread = (max - min) / step / Environment.ProcessorCount;
            var stringLocations = new List<UInt32>(foundStrings.Keys);
            Parallel.For(0, Environment.ProcessorCount, delegate (int i)
            {
                var tmp = FindAllReferencesForAllOffsets(min + scanSizePerThread * i, min + scanSizePerThread * (i + 1) - 1, step, stringLocations, i == 0, numFormat);
                offsetToReferencesMTX.WaitOne();
                tmp.ToList().ForEach(x => offsetToReferences.Add(x.Key, x.Value));
                offsetToReferencesMTX.ReleaseMutex();
            });
            // presumably! the correct offset is the one where the most strings actually get referenced
            var maxRefs = offsetToReferences.Aggregate((l, r) => TotalValueItemsCount(l.Value, maxKeyAttribution) > TotalValueItemsCount(r.Value, maxKeyAttribution) ? l : r);
            int bestOffset = maxRefs.Key;
            int maxStringsReferenced = TotalValueItemsCount(maxRefs.Value,1);

            writeLock.WaitOne();
            // print out information about the best fit (10 most referenced strings)
            Console.WriteLine("Observed a maxiumum of " + maxStringsReferenced +"/"+foundStrings.Count() +" strings referenced using offset:" + bestOffset);
            foreach (var str in offsetToReferences[bestOffset].OrderBy(x => -x.Value.Count).Take(10))
            {
                Console.WriteLine("String: {0} at addr {1:X8} got referenced {2} times",foundStrings[str.Key],str.Key,str.Value.Count());
            }
            // print out the next biggest numbers of references made (so that it is visible if there is a sharp
            // decrease compared to the first one indicating that it was not just by chance) 
            var sortedOffsets = offsetToReferences.OrderBy(x => -TotalValueItemsCount(x.Value, maxKeyAttribution));
            Console.WriteLine("offset | references | uniqueStringsReferenced");
            for (int i = 0; i < 15 && i < sortedOffsets.Count(); i++)
            {
                Console.WriteLine(sortedOffsets.ElementAt(i).Key + " | " + TotalValueItemsCount(sortedOffsets.ElementAt(i).Value, int.MaxValue) + " | " + TotalValueItemsCount(sortedOffsets.ElementAt(i).Value, 1));
            }
            writeLock.ReleaseMutex();
            return new Tuple<int, int>(maxRefs.Key, maxStringsReferenced);
        }


        /// <summary>
        /// For each address find the referencing memory locations
        /// </summary>
        /// <param name="addr">the address to be referenced</param>
        /// <param name="addrToBits">a NumberFormat</param>
        /// <param name="offset">the offet to apply to the address</param>
        /// <returns></returns>
        Dictionary<UInt32, List<UInt32>> FindReferences(byte[] rom, List<UInt32> addresses, Func<long, byte[]> addrToBits, int offset)
        {
            //addr,List<referencingPositions>
            Dictionary<UInt32, List<UInt32>> references = new Dictionary<UInt32, List<UInt32>>();
            foreach (UInt32 addr in addresses)
            {
                references.Add(addr, FindReferences(addr, addrToBits, offset));
            }
            return references;
        }

        /// <summary>
        /// Finds all memory locations containing addr
        /// </summary>
        /// <param name="addr">the address to be referenced</param>
        /// <param name="addrToBits">a NumberFormat</param>
        /// <param name="offset">the offet to apply to the address</param>
        /// <returns></returns>
        List<UInt32> FindReferences(UInt32 addr, Func<long, byte[]> addrToBits, int offset)
        {
            List<UInt32> references = new List<UInt32>();
            int pos = 0;

            int matchedBytes = 0;
            byte[] pattern = addrToBits(addr + offset);
            bool nZero = false;
            while (pos < rom.Length)
            {
                nZero |= (rom[pos] != 0);
                if (rom[pos] == pattern[matchedBytes])
                {
                    if (matchedBytes == pattern.Length - 1)
                    {
                        // ignore match if the source is just zeroes#
                        if (nZero || true)
                        {
                            references.Add((UInt32)(pos - matchedBytes));
                        }
                        nZero = false;
                        matchedBytes = 0;
                    }
                    else
                    {
                        matchedBytes++;
                    }
                }
                else
                {
                    matchedBytes = 0;
                }
                pos++;
            }
            return references;
        }

        /// <summary>
        /// Counts the total number of values (of each Key)
        /// </summary>
        /// <param name="refs"></param>
        /// <returns></returns>
        int TotalValueItemsCount(Dictionary<UInt32, List<UInt32>> refs, int maxKeyAttribution)
        {
            int totalrefs = 0;
            foreach (var v in refs)
            {
                totalrefs += (v.Value.Count > maxKeyAttribution) ? maxKeyAttribution : v.Value.Count;
            }
            return totalrefs;
        }

        /// <summary>
        /// Checks if b is a sensible alphanumeric ASCI character
        /// </summary>
        /// <param name="b"> the character to check</param>
        /// <returns>if the character is a regular (alphanumeric, whitespace,...) ASCI character  </returns>
        bool isAsci(byte b)
        {
            return b < 127 && b > 32;
        }
        double getEntropy(byte[] data)
        {
            int[] occurences = new int[256];
            foreach (byte b in data)
            {
                occurences[b] += 1;
            }
            double entropy = 0;
            for (int c = 0; c < 256; c++)
            {
                double prob = ((double)occurences[(byte)c] / (double)data.Length);
                entropy -= (prob > 0) ? prob * Math.Log(prob, 256) : 0;
            }
            return entropy;
        }
    }
}
