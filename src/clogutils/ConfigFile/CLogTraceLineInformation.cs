/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    Describes one trace event for purposes of uniqueuness

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
