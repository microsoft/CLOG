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

        public bool MarkPhase { get; set; }


        /// <summary>
        /// Only serialize Synthesized if it's in use - just to keep things in the .config file tidy
        /// </summary>
        /// <returns></returns>
        public bool ShouldSerializeCustomDecoder()
        {
            return !string.IsNullOrEmpty(CustomDecoder);
        }

        /// <summary>
        /// Only serialize Synthesized if it's in use - just to keep things in the .config file tidy
        /// </summary>
        /// <returns></returns>
        public bool ShouldSerializeSynthesized()
        {
            return Synthesized;
        }

        public bool ShouldSerializeUsedBySourceFile()
        {
            return MarkPhase;
        }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            if(null == UsedBySourceFile)
                UsedBySourceFile = new HashSet<string>();
        }
    }
}
