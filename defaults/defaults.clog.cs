/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

--*/

namespace defaults.clog_config
{
    public class Types
    {
        public static string DecodePointer(ulong pointer)
        {
            return "0x" + pointer.ToString("x");
        }

        public static string DecodeChar(byte value)
        {
            return ((char)value).ToString();
        }

        public static string DecodeUInt32(uint value)
        {
            return value.ToString();
        }

        public static string DecodeInt32(int value)
        {
            return value.ToString();
        }

        public static string DecodeInt8(byte value)
        {
            return value.ToString();
        }
    }
}
