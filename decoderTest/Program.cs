/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

--*/

using System;

namespace decoderTest
{
    class Program
    {
        private static void VerifyConversion(string expected, string actual)
        {
            if(!expected.Equals(actual))
            {
                Console.WriteLine($"Conversion failed : {expected} != {actual}");
            }
        }
        static void Main(string[] args)
        {
            //
            // Test some simple config types


            //DecodeInt32
            VerifyConversion("-10000", defaults.clog_config.Types.DecodeInt32(-10000));
            VerifyConversion("10000", defaults.clog_config.Types.DecodeInt32(10000));

            //DecodeUInt32
            VerifyConversion("10000", defaults.clog_config.Types.DecodeUInt32(10000));
            VerifyConversion("4294957296", defaults.clog_config.Types.DecodeUInt32(unchecked((uint)-10000)));
        }
    }
}