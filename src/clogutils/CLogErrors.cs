/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    Helpers to present errors to the user

--*/

namespace clogutils
{
    public class CLogErrors
    {
        public static void PrintMatchDiagnostic(CLogLineMatch traceLineMatch)
        {
            if (null == traceLineMatch)
            {
                return;
            }
            CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, $"Failing Line : {traceLineMatch.MatchedRegExX.Value}");
        }
    }
}
