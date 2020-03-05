/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    Helpers to present errors to the user

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
