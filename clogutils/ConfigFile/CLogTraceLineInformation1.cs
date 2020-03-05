/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    A stream manages the send and receive queues for application data. This file
    contains the initialization and cleanup functionality for the stream.

--*/

using System;
using Newtonsoft.Json;

namespace clogutils.ConfigFile
{
    [JsonObject(MemberSerialization.OptIn)]
    public class CLogTraceLineInformation
    {
        [JsonProperty] public Guid UniquenessHash { get; set; }

        [JsonProperty] public string TraceID { get; set; }

        public bool Unsaved { get; set; }

        public CLogDecodedTraceLine PreviousFileMatch { get; set; }
    }
}
