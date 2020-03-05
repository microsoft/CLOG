/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    Interface for a CLOG output module {lttng, manifested, tracelogging, etc]

--*/

using System.Text;

namespace clogutils
{
    public interface ICLogOutputModule
    {
        string ModuleName { get; }

        bool ManditoryModule { get; }

        void InitHeader(StringBuilder header);

        void TraceLineDiscovered(string sourceFile, CLogDecodedTraceLine traceLine, CLogSidecar sidecar,
            StringBuilder macroPrefix, StringBuilder inline, StringBuilder function);

        void FinishedProcessing(StringBuilder header, StringBuilder sourceFile);
    }
}
