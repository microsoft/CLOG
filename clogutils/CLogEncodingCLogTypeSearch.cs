/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    A stream manages the send and receive queues for application data. This file
    contains the initialization and cleanup functionality for the stream.

--*/

namespace clogutils
{
    public class CLogEncodingCLogTypeSearch
    {
        public CLogEncodingCLogTypeSearch(string d, bool synthesized = false)
        {
            DefinationEncoding = d;
           // CType = c;
            //  TraceLoggingAPI = api;
            Synthesized = synthesized;
        }

        public CLogEncodingType EncodingType { get; set; }

        public string CType { get; set; }

        // public string TraceLoggingAPI { get; set; }

        public string DefinationEncoding { get; set; }

        public string CustomDecoder { get; set; }

        public bool Synthesized { get; set; }
    }
}
