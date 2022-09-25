﻿using System;
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
        public static byte[] LittleEndian24Rev(long num)
        {
            byte[] ret = new byte[3];
            if (BitConverter.IsLittleEndian)
            {
                ret[0] = reverseBitOrder(BitConverter.GetBytes(num)[0]);
                ret[1] = reverseBitOrder(BitConverter.GetBytes(num)[1]);
                ret[2] = reverseBitOrder(BitConverter.GetBytes(num)[2]);
            }
            else
            {
                ret[0] = reverseBitOrder(BitConverter.GetBytes(num)[2]);
                ret[1] = reverseBitOrder(BitConverter.GetBytes(num)[1]);
                ret[2] = reverseBitOrder(BitConverter.GetBytes(num)[0]);
            }
            return ret;
        }
        public static byte[] LittleEndian24(long num)
        {
            byte[] ret = new byte[3];
            if (BitConverter.IsLittleEndian)
            {
                ret[0] = BitConverter.GetBytes(num)[0];
                ret[1] = BitConverter.GetBytes(num)[1];
                ret[2] = BitConverter.GetBytes(num)[2];
            }
            else
            {
                ret[0] = BitConverter.GetBytes(num)[2];
                ret[1] = BitConverter.GetBytes(num)[1];
                ret[2] = BitConverter.GetBytes(num)[0];
            }
            return ret;
        }

        public static byte[] BigEndian24Rev(long num)
        {
            byte[] ret = new byte[3];
            if (!BitConverter.IsLittleEndian)
            {
                ret[0] = reverseBitOrder(BitConverter.GetBytes(num)[0]);
                ret[1] = reverseBitOrder(BitConverter.GetBytes(num)[1]);
                ret[2] = reverseBitOrder(BitConverter.GetBytes(num)[2]);
            }
            else
            {
                ret[0] = reverseBitOrder(BitConverter.GetBytes(num)[2]);
                ret[1] = reverseBitOrder(BitConverter.GetBytes(num)[1]);
                ret[2] = reverseBitOrder(BitConverter.GetBytes(num)[0]);
            }
            return ret;
        }
        public static byte[] BigEndian24(long num)
        {
            byte[] ret = new byte[3];
            if (!BitConverter.IsLittleEndian)
            {
                ret[0] = BitConverter.GetBytes(num)[0];
                ret[1] = BitConverter.GetBytes(num)[1];
                ret[2] = BitConverter.GetBytes(num)[2];
            }
            else
            {
                ret[0] = BitConverter.GetBytes(num)[2];
                ret[1] = BitConverter.GetBytes(num)[1];
                ret[2] = BitConverter.GetBytes(num)[0];
            }
            return ret;
        }
        public static byte[] Universal(long num, bool littleEndian, bool bitsReversed, byte byteCount)
        {
            byte[] ret = new byte[byteCount];
            byte[] numberBytes = BitConverter.GetBytes(num);
            for(int i = 0; i < ret.Count(); i++)
            {
                ret[i] = bitsReversed ? reverseBitOrder(numberBytes[i]) : numberBytes[i];
            }
            return (BitConverter.IsLittleEndian==littleEndian)?ret: reverseByteOrder(ret);
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
