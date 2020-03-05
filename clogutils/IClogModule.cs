/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    A stream manages the send and receive queues for application data. This file
    contains the initialization and cleanup functionality for the stream.

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
