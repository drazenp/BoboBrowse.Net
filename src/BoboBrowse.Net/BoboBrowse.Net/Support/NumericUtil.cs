﻿//* Bobo Browse Engine - High performance faceted/parametric search implementation 
//* that handles various types of semi-structured data.  Originally written in Java.
//*
//* Ported and adapted for C# by Shad Storhaug.
//*
//* Copyright (C) 2015  Shad Storhaug
//*
//* Licensed under the Apache License, Version 2.0 (the "License");
//* you may not use this file except in compliance with the License.
//* You may obtain a copy of the License at
//*
//*   http://www.apache.org/licenses/LICENSE-2.0
//*
//* Unless required by applicable law or agreed to in writing, software
//* distributed under the License is distributed on an "AS IS" BASIS,
//* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//* See the License for the specific language governing permissions and
//* limitations under the License.

namespace BoboBrowse.Net.Support
{
    using Lucene.Net.Util;
    using System;

    public class NumericUtil
    {
        public static bool IsPrefixCodedInt(string prefixCoded)
        {
            try
            {
                int shift = prefixCoded[0] - NumericUtils.SHIFT_START_INT;
                if (shift > 31 || shift < 0)
                    return false;
                int sortableBits = 0;
                for (int i = 1, len = prefixCoded.Length; i < len; i++)
                {
                    sortableBits <<= 7;
                    char ch = prefixCoded[i];
                    if (ch > 0x7f)
                    {
                        return false;
                    }
                    sortableBits |= (int)ch;
                }
                int result = (sortableBits << shift) ^ unchecked((int)0x80000000);
            }
            catch
            {
                return false;
            }

            return true;
        }

        public static bool IsPrefixCodedLong(string prefixCoded)
        {
            try
            {
                int shift = prefixCoded[0] - NumericUtils.SHIFT_START_LONG;
                if (shift > 63 || shift < 0)
                    return false;
                ulong sortableBits = 0UL;
                for (int i = 1, len = prefixCoded.Length; i < len; i++)
                {
                    sortableBits <<= 7;
                    char ch = prefixCoded[i];
                    if (ch > 0x7f)
                    {
                        return false;
                    }
                    sortableBits |= (ulong)ch;
                }
                long result = BitConverter.ToInt64(BitConverter.GetBytes((sortableBits << shift) ^ 0x8000000000000000L), 0);
            }
            catch
            {
                return false;
            }

            return true;
        }

        public static bool IsPrefixCodedFloat(string prefixCoded)
        {
            if (IsPrefixCodedInt(prefixCoded))
            {
                try
                {
                    int val = NumericUtils.PrefixCodedToInt(prefixCoded);
                    if (val < 0)
                        val ^= 0x7fffffff;
                    float result = BitConverter.ToSingle(BitConverter.GetBytes(val), 0);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        public static bool IsPrefixCodedDouble(string prefixCoded)
        {
            if (IsPrefixCodedLong(prefixCoded))
            {
                try
                {
                    long val = NumericUtils.PrefixCodedToLong(prefixCoded);
                    if (val < 0)
                        val ^= 0x7fffffffffffffffL;
                    double result = BitConverter.Int64BitsToDouble(val);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }
    }
}
