/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    Describes a CLOG search macro within a JSON configuration file

--*/

using System.Collections.Generic;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace clogutils
{
    [JsonObject(MemberSerialization.OptIn)]
    public class CLogEncodingCLogTypeSearch
    {
        public CLogEncodingCLogTypeSearch(string d, bool synthesized = false)
        {
            DefinationEncoding = d;
            Synthesized = synthesized;
            UsedBySourceFile = new HashSet<string>();
        }

        [JsonProperty]
        public CLogEncodingType EncodingType { get; set; }

        [JsonProperty]
        public string CType { get; set; }

        [JsonProperty]
        public string DefinationEncoding { get; set; }

        [JsonProperty]
        public string CustomDecoder { get; set; }

        [JsonProperty]
        public bool Synthesized { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public HashSet<string> UsedBySourceFile { get; set; } = new HashSet<string>();

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            if(null == UsedBySourceFile)
                UsedBySourceFile = new HashSet<string>();
        }
    }
}
