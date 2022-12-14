using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using TI30XDev;

namespace BinAnalyzer
{
    using ReferencesFromAddr = Dictionary<UInt32, List<UInt32>>; // Key: addr, Value: references

    public class Analyzer
    {
        byte[] rom;
        Dictionary<ulong, string> foundStrings;
        Mutex writeLock = new Mutex(); // locks console printing

        public Analyzer(string path, int maxStringsToConsider = 20)
        {
            rom = File.ReadAllBytes(path);
            foundStrings = FindStrings(maxStringsToConsider);
        }


        public struct RefType
        {
            public string format;
            public long offset;
            public ulong uniqueMatches;
            public double certainty; // estimated percentage of real (not random) references
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="minOffset"></param>
        /// <param name="maxOffset"></param>
        /// <param name="step"></param>
        /// <param name="minStrLength"></param>
        /// <returns>format, offset</returns>
        public RefType FindNumberFormatAndOffset(long minOffset, long maxOffset, int step, int minStrLength)
        {
            RefType bestMatch = new RefType { format = "unknown", offset = 0, uniqueMatches = 0 };
            List<RefType> results = new List<RefType>();
            foreach (var format in NumFormats.formats)
            {
                Console.WriteLine("Now testing " + format.Key);
                var result = findOffset(minOffset, maxOffset, step, format, 1);
                results.Add(result);
                Console.WriteLine("  certainty:"+result.certainty);
            }
            var reasonable = results.Where(x => x.certainty > 0.4);
            if (reasonable.Count() > 0)
            {
                return reasonable.OrderBy(r => -(long)r.uniqueMatches).First();
            }
            else
            {
                return results.OrderBy(r => -r.certainty).First();
            }
        }

        /// <summary>
        /// Finds all the strings in the rom
        /// </summary>
        /// <param name="minStrLength">the minimum length required for a string to be recognized</param>
        /// <returns>A dictionary which maps an address to a found string </returns>
        public Dictionary<ulong, String> FindStrings(int maxStrings = int.MaxValue, int minStrLength = 5, double minEntropy = 0.3, double maxEntropy = 0.5)
        {
            Dictionary<ulong, string> found = new Dictionary<ulong, string>();
            ulong pos = 0;
            string currentString = "";
            while (pos < (ulong)rom.Length)
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
                            found.Add((pos - (ulong)currentString.Length), currentString);
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
        /// <param name="maxOffset"> The maximum (inclusive) offset applied to numbers</param>
        /// <param name="stepSize"> The granuality at which to calculate the offset (2 => Offset%2=0 etc)</param>
        /// <param name="numbers"> The addresses (string locations) to search for</param>
        /// <param name="debug"> Wheather or not to print debug messages</param>
        /// <param name="numberFormat"> A Method on how to turn the numbers into binary (found in NumberFormats)</param>
        /// <returns> Dict(offset,Dict(address,List(referencedBy)))
        /// </returns>
        Dictionary<long, ReferencesFromAddr> FindAllReferencesForAllOffsets(long minOffset, long maxOffset, int stepSize, List<ulong> numbers, bool debug, Func<long, byte[]> numberFormat)
        {
            Dictionary<long, ReferencesFromAddr> ReferencesPerOffset = new Dictionary<long, ReferencesFromAddr>();
            var start = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            long currentOffset = minOffset;
            long lastDebugOutputMillis = 0;
            // for every offset
            while (currentOffset <= maxOffset)
            {
                // find the total number of times one of the provided numbers appear in the rom
                ReferencesPerOffset.Add(currentOffset, FindReferences(rom, numbers, numberFormat, (int)currentOffset));
                if (debug && lastDebugOutputMillis < DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - 500 && currentOffset != minOffset)
                {
                    lastDebugOutputMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    var timeSinceStart = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - start;

                    double processedOffsets = currentOffset - minOffset;
                    double totalOffsets = maxOffset - minOffset;
                    double leftOffsets = totalOffsets - processedOffsets;
                    double percentageDone = processedOffsets / totalOffsets;
                    double percentageLeft = leftOffsets / totalOffsets;
                    double estimatedTotalProcessingTime = (timeSinceStart / percentageDone);
                    double estimatedRemainingProcessingTime = estimatedTotalProcessingTime * percentageLeft;
                    DateTime expectedFinish = DateTimeOffset.FromUnixTimeMilliseconds((long)estimatedRemainingProcessingTime + start + timeSinceStart).LocalDateTime;
                    Console.WriteLine("  progress:{1:0.00}% eta:{2}", currentOffset,percentageDone * 100, expectedFinish);
                    Console.SetCursorPosition(0, Console.CursorTop - 1);
                }
                currentOffset += stepSize;
            }
            return ReferencesPerOffset;
        }

        /// <summary>
        /// Finds the amount of times a string gets referenced for each offset
        /// </summary>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <param name="step"></param>
        /// <param name="minStrLength"></param>
        /// <param name="numFormat"></param>
        /// <returns>offset,matches</returns>
        public RefType findOffset(long min, long max, int step, KeyValuePair<string, Func<long, byte[]>> numFormat, int maxKeyAttribution,bool multiThreaded=true)
        {
            Mutex offsetToReferencesMTX = new Mutex();
            Dictionary<long, ReferencesFromAddr> offsetToReferences = new Dictionary<long, ReferencesFromAddr>();
            var stringLocations = new List<ulong>(foundStrings.Keys);
            if (multiThreaded)
            {
                var ranges = Utils.buildIntervals(min, max, (long)Math.Min((double)Environment.ProcessorCount,max-min+1));
                Parallel.ForEach(ranges, range =>
                {
                    var tmp = FindAllReferencesForAllOffsets((int)range.Item2,(int)range.Item3, step, stringLocations, range.Item1==0, numFormat.Value);
                    offsetToReferencesMTX.WaitOne();
                    tmp.ToList().ForEach(x => offsetToReferences.Add(x.Key, x.Value));
                    offsetToReferencesMTX.ReleaseMutex();
                });
            }
            else
            {
                offsetToReferences = FindAllReferencesForAllOffsets((int)min, (int)max, step, stringLocations, true, numFormat.Value);
            }
            if (offsetToReferences.Count == 0)
            {
                return new RefType() { format = numFormat.Key, offset = 0, uniqueMatches = 0, certainty = 0 };
            }
            // presumably! the correct offset is the one where the most strings actually get referenced
            var maxRefs = offsetToReferences.Aggregate((l, r) => TotalValueItemsCount(l.Value, maxKeyAttribution) > TotalValueItemsCount(r.Value, maxKeyAttribution) ? l : r);
            long bestOffset = maxRefs.Key;
            ulong maxStringsReferenced = (ulong)TotalValueItemsCount(maxRefs.Value, 1);

            writeLock.WaitOne();
            // print out information about the best fit (10 most referenced strings)
                Console.WriteLine("Observed a maxiumum of {0}/{1} strings referenced using offset:{2}(dec)", maxStringsReferenced, foundStrings.Count(), bestOffset);
                foreach (var str in offsetToReferences[bestOffset].Where(x => x.Value.Count>0).OrderBy(x => -x.Value.Count).Take(30))
            {
                Console.WriteLine("String: {0} at addr {1:X8} got referenced {2} times", foundStrings[str.Key], str.Key, str.Value.Count());
            }
            // print out the next biggest numbers of references made (so that it is visible if there is a sharp
            // decrease compared to the first one indicating that it was not just by chance) 
            var sortedOffsets = offsetToReferences.OrderBy(x => -TotalValueItemsCount(x.Value, maxKeyAttribution)).Where(x=> TotalValueItemsCount(x.Value, 1)>0).Take(5);
            Console.WriteLine("offset | references | uniqueStringsReferenced");
            foreach (var reference in sortedOffsets)
            {
                Console.WriteLine(reference.Key + " | " + TotalValueItemsCount(reference.Value, int.MaxValue) + " | " + TotalValueItemsCount(reference.Value, 1));
            }
            writeLock.ReleaseMutex();
            // we count up to 15 references of a string, more are probably not that certain
            double certainty = TotalValueItemsCount(maxRefs.Value, 15) / (NumFormats.expectedNumberOfOccurences(rom.LongLength, numFormat.Key) * foundStrings.Count() + (ulong)TotalValueItemsCount(maxRefs.Value, 15));
            return new RefType() { format = numFormat.Key, offset = bestOffset, uniqueMatches = maxStringsReferenced,certainty=certainty };
        }

       

        public struct Reference
        {
            public long addr;       // the address to which is pointed
            public long reference;  // the location where addr was found (which references addr)
        }
        /// <summary>
        /// For each address find the referencing memory locations
        /// </summary>
        /// <param name="addr">the address to be referenced</param>
        /// <param name="addrToBits">a NumberFormat</param>
        /// <param name="offset">the offet to apply to the address</param>
        /// <returns>key:address,Value:refferences</returns>
        ReferencesFromAddr FindReferences(byte[] rom, List<ulong> addresses, Func<long, byte[]> addrToBits, int offset)
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
        /// <returns>A list of all memory locations where addr is referenced</returns>
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
                        pos -= matchedBytes;
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
