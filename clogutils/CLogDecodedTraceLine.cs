/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    Represents one trace line,  containing one event.  It's created during compilation and stored within a side car for future presentation

--*/

using clogutils.ConfigFile;
using clogutils.MacroDefinations;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace clogutils
{
    [JsonObject(MemberSerialization.OptIn)]
    public class CLogDecodedTraceLine
    {
        [JsonProperty] public Dictionary<string, Dictionary<string, string>> ModuleProperites = new Dictionary<string, Dictionary<string, string>>();

        public CLogDecodedTraceLine(string uniqueId, string sourceFile, string userString, string userStringNoPrefix, CLogLineMatch m, CLogConfigurationFile c,
            CLogTraceMacroDefination mac, CLogFileProcessor.CLogVariableBundle[] args)
        {
            SourceFile = sourceFile;
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

        public CLogConfigurationProfile GetMacroConfigurationProfile()
        {
            return configFile.MacroConfigurations[macro.MacroConfiguration[configFile.ProfileName]];
        }

        public CLogLineMatch match { get; private set; }

        public string SourceFile { get; set; }

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
