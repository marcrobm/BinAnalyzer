using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TI30XDev
{
    public static class NumFormats
    {
        public static Dictionary<string, Func<long, byte[]>> formats = new Dictionary<string, Func<long, byte[]>>();
        static NumFormats()
        {
            formats.Add("LittleEndian16", num => Universal(num, true, false, 2));
            formats.Add("LittleEndian16Rev", num => Universal(num, true, true, 2));
            formats.Add("BigEndian16", num => Universal(num, false, false, 2));
            formats.Add("BigEndian16Rev", num => Universal(num, false, true, 2));

            formats.Add("LittleEndian24", num => Universal(num, true, false, 3));
            formats.Add("LittleEndian24Rev", num => Universal(num, true, true, 3));
            formats.Add("BigEndian24", num => Universal(num, false, false, 3));
            formats.Add("BigEndian24Rev", num => Universal(num, false, true, 3));

            formats.Add("LittleEndian32", num => Universal(num, true, false, 4));
            formats.Add("LittleEndian32Rev", num => Universal(num, true, true, 4));
            formats.Add("BigEndian32", num => Universal(num, false, false, 4));
            formats.Add("BigEndian32Rev", num => Universal(num, false, true, 4));
        }

        static byte[] Universal(long num, bool littleEndian, bool bitsReversed, byte byteCount)
        {
            byte[] ret = new byte[byteCount];
            byte[] numberBytes = BitConverter.GetBytes(num);
            for (int i = 0; i < ret.Count(); i++)
            {
                ret[i] = bitsReversed ? reverseBitOrder(numberBytes[i]) : numberBytes[i];
            }
            return (BitConverter.IsLittleEndian == littleEndian) ? ret : reverseByteOrder(ret);
        }
        /// <summary>
        /// This estimates the probability to see sequence of patternLength bytes in the data
        /// This assumes that the data is independant (entropy=1), but in reality entropy is ~0.6
        /// so this might tend to be on the smaller side
        /// </summary>
        /// <param name="dataLength"></param>
        /// <param name=""></param>
        /// <returns></returns>
        public static double expectedNumberOfOccurences(long dataLength, int patternLength)
        {
            return (dataLength - patternLength) / Math.Pow(256.0, patternLength);
        }
        public static double expectedNumberOfOccurences(long dataLength,string format)
        {
            return expectedNumberOfOccurences(dataLength, formats[format](0).Length);
        }
        static byte[] reverseByteOrder(byte[] inp)
        {
            byte[] ret = new byte[inp.Count()];
            for (int i = 0; i < ret.Length; i++)
            {
                ret[i] = inp[inp.Count() - i - 1];
            }
            return ret;
        }
        static byte reverseBitOrder(byte inp)
        {
            byte ret = 0;
            for (int bit = 0; bit <= 7; bit++)
            {
                byte mask = (byte)(1 << bit);
                byte isSet = (byte)((inp & mask) >> bit);
                ret |= (byte)(isSet << (7 - bit));
            }
            return ret;
        }
    }
}
