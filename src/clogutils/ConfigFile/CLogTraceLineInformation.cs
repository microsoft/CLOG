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

        [JsonProperty]
        public string EncodingString { get; set; }

        public bool Unsaved { get; set; }

        public CLogDecodedTraceLine PreviousFileMatch { get; set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class CLogTraceLineInformation_V2
    {
        [JsonProperty] public Guid UniquenessHash { get; set; }

        [JsonProperty] public string TraceID { get; set; }

        [JsonProperty]
        public string EncodingString { get; set; }

        public bool Unsaved { get; set; }

        public CLogDecodedTraceLine PreviousFileMatch { get; set; }

        public static CLogTraceLineInformation_V2 ConvertFromV1(CLogTraceLineInformation v1)
        {
            CLogTraceLineInformation_V2 ret = new CLogTraceLineInformation_V2();
            ret.UniquenessHash = v1.UniquenessHash;
            ret.TraceID = v1.TraceID;
            ret.EncodingString = "Unknown: in back compat mode, please rebuild sidecar using newer tools";
            return ret;
        }
    }
}
