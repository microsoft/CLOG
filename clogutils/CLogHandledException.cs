/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    A stream manages the send and receive queues for application data. This file
    contains the initialization and cleanup functionality for the stream.

--*/

using System;
using System.Text.RegularExpressions;

namespace clogutils
{
    public class CLogHandledException : Exception
    {
        public CLogHandledException(string msg, Match traceLine, bool silent = false, Exception e = null) : base(msg)
        {
            Exception = e;
            TraceLine = traceLine;

            if (!silent)
            {
                PrintDiagnostics();
            }
        }

        public Match TraceLine { get; set; }

        public Exception Exception { get; set; }

        public void PrintDiagnostics()
        {
            CLogErrors.PrintMatchDiagnostic(TraceLine);
            CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, $"ERROR HELP MESSAGE: {Message}");
        }
    }
}
