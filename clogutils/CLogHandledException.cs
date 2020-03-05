/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    Describes a handled exception that comes as a result of a traceline (in your C/C++) that is malformed

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
