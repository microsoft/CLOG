/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    A stream manages the send and receive queues for application data. This file
    contains the initialization and cleanup functionality for the stream.

--*/

using System.Collections.Generic;
using Newtonsoft.Json;

namespace clogutils.MacroDefinations
{
    [JsonObject(MemberSerialization.OptIn)]
    public class CLogUsageMacros
    {
        private static readonly int _myVersion = 2;

        public CLogUsageMacros()
        {
            Macros = new List<CLogTraceMacroDefination>();
            Version = _myVersion;
        }

        [JsonProperty] public int Version { get; private set; }

        [JsonProperty] public Dictionary<string, int> Levels { get; } = new Dictionary<string, int>();

        [JsonProperty] public Dictionary<string, int> Keywords { get; } = new Dictionary<string, int>();

        [JsonProperty] public List<CLogTraceMacroDefination> Macros { get; } = new List<CLogTraceMacroDefination>();

        [JsonProperty] public int Hidden { get; set; }

        public bool ShouldSerializeHidden()
        {
            return false;
        }
    }
}