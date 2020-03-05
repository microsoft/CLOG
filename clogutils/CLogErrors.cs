/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    A stream manages the send and receive queues for application data. This file
    contains the initialization and cleanup functionality for the stream.

--*/

using System.Text.RegularExpressions;

namespace clogutils
{
    public class CLogErrors
    {
        public static void PrintMatchDiagnostic(Match traceLineMatch)
        {
            if (null == traceLineMatch)
            {
                return;
            }

            CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, $"Failing Line : {traceLineMatch.Value}");
        }

        public static void ReportUnspecifiedCLogType(string leadupString, Match traceLineMatch)
        {
            CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, $"CERROR:0001 - Unspecified CLog Type {leadupString}");
            PrintMatchDiagnostic(traceLineMatch);
        }
    }
}
