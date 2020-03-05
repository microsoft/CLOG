/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    Represents one trace line,  containing one event.  It's created during compilation and stored within a side car for future presentation

--*/

using System.Collections.Generic;
using System.Text.RegularExpressions;
using clogutils.ConfigFile;
using clogutils.MacroDefinations;
using Newtonsoft.Json;

namespace clogutils
{
    [JsonObject(MemberSerialization.OptIn)]
    public class CLogDecodedTraceLine
    {
        [JsonProperty] public Dictionary<string, Dictionary<string, string>> ModuleProperites = new Dictionary<string, Dictionary<string, string>>();

        public CLogDecodedTraceLine(string uniqueId, string userString, string userStringNoPrefix, Match m, CLogConfigurationFile c,
            CLogTraceMacroDefination mac, CLogFileProcessor.CLogVariableBundle[] args)
        {
            macro = mac;
            UniqueId = uniqueId;
            match = m;
            configFile = c;
            TraceString = userString;
            splitArgs = args;
            TraceStringNoPrefix = userStringNoPrefix;
        }

        [JsonProperty] public string TraceString { get; private set; }

        public string TraceStringNoPrefix { get; private set; }

        [JsonProperty] public string UniqueId { get; private set; }

        public CLogConfigurationFile configFile { get; private set; }

        [JsonProperty] public CLogFileProcessor.CLogVariableBundle[] splitArgs { get; private set; }

        [JsonProperty] public CLogTraceMacroDefination macro { get; private set; }

        public Match match { get; private set; }

        public void AddConfigFileProperty(string module, string key, string value)
        {
            string oldValue = GetConfigFileProperty(module, key);

            // If we already have this value, dont set it (we dont want to dirty the config file)
            if (value.Equals(oldValue))
            {
                return;
            }

            if (!ModuleProperites.ContainsKey(module))
            {
                ModuleProperites[module] = new Dictionary<string, string>();
            }

            ModuleProperites[module][key] = value;
            configFile.IsDirty = true;
        }

        public string GetConfigFileProperty(string module, string key)
        {
            if (!ModuleProperites.ContainsKey(module))
            {
                return null;
            }

            if (!ModuleProperites[module].ContainsKey(key))
            {
                return null;
            }

            return ModuleProperites[module][key];
        }
    }
}
