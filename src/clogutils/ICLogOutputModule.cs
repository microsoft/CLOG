/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    Interface for a CLOG output module {lttng, manifested, tracelogging, etc]

--*/

using System.Text;

namespace clogutils
{
    public class CLogOutputInfo
    {
        public string OutputFileName { get; set; }
        public string OutputDirectory { get; set; }
        public string InputFileName { get; set; }

    }

    public interface ICLogBatchingModule
    {
        void FinishedBatch(CLogOutputInfo oi);
    }

    public interface ICLogOutputModule
    {
        string ModuleName { get; }

        bool ManditoryModule { get; }

        void InitHeader(StringBuilder header);

        void TraceLineDiscovered(string sourceFile, CLogOutputInfo outputInfo, CLogDecodedTraceLine traceLine, CLogSidecar sidecar,
            StringBuilder macroPrefix, StringBuilder inline, StringBuilder function);

        void FinishedProcessing(CLogOutputInfo outputInfo, StringBuilder header, StringBuilder sourceFile);
    }
}
