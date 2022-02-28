/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    Represents one trace line,  containing one event.  It's created during compilation and stored within a side car for future presentation

--*/

using System.Collections.Generic;
using System.Runtime.Serialization;
using clogutils.ConfigFile;
using clogutils.MacroDefinations;
using Newtonsoft.Json;

namespace clogutils
{
    [JsonObject(MemberSerialization.OptIn)]
    public class CLogDecodedTraceLine
    {
        [JsonProperty] public SortedDictionary<string, SortedDictionary<string, string>> ModuleProperites = new SortedDictionary<string, SortedDictionary<string, string>>();

        public CLogDecodedTraceLine(string uniqueId, string sourceFile, string userString, string userStringNoPrefix, CLogLineMatch m, CLogConfigurationFile c,
            CLogTraceMacroDefination mac, CLogFileProcessor.CLogVariableBundle[] args, CLogFileProcessor.DecomposedString decompString)
        {
            SourceFile = sourceFile;
            macro = mac;
            UniqueId = uniqueId;
            match = m;
            configFile = c;
            TraceString = userString;
            splitArgs = args;
            TraceStringNoPrefix = userStringNoPrefix;

            DecomposedString = decompString;
        }

        [JsonProperty]
        public string TraceString { get; private set; }

        public CLogFileProcessor.DecomposedString DecomposedString;

        public string CleanedString { get { return DecomposedString.AsManifestedETWEncoding; } }

        public string TraceStringNoPrefix { get; private set; }


        [JsonProperty]
        public string UniqueId { get; private set; }

        public CLogConfigurationFile configFile { get; private set; }

        [JsonProperty]
        public CLogFileProcessor.CLogVariableBundle[] splitArgs { get; private set; }

        private CLogFileProcessor.CLogVariableBundle[] _tempArgs;
        [OnSerializing]
        internal void OnSerializingMethod(StreamingContext context)
        {
            List<CLogFileProcessor.CLogVariableBundle> tArgs = new List<CLogFileProcessor.CLogVariableBundle>();
            foreach (var a in splitArgs)
            {
                if (!string.IsNullOrEmpty(a.DefinationEncoding))
                    tArgs.Add(a);
            }
            _tempArgs = splitArgs;
            splitArgs = tArgs.ToArray();
        }

        [OnSerialized]
        internal void OnSerializedMethod(StreamingContext context)
        {
            splitArgs = _tempArgs;
        }

        //[JsonProperty]
        public CLogTraceMacroDefination macro { get; private set; }


        private string _macroName;
        [JsonProperty]
        public string macroName
        {
            get
            {
                if (null != macro && !string.IsNullOrEmpty(macro.MacroName))
                    return macro.MacroName;

                return _macroName;
            }

            set
            {
                _macroName = value;
            }
        }

        public CLogConfigurationProfile GetMacroConfigurationProfile()
        {
            if(!macro.MacroConfiguration.ContainsKey(configFile.ProfileName))
                throw new CLogEnterReadOnlyModeException("MissingProfile:" + configFile.ProfileName + " used by macro=" + macro.MacroName, CLogHandledException.ExceptionType.RequiredConfigParameterUnspecified, match);

            if (!configFile.MacroConfigurations.ContainsKey(macro.MacroConfiguration[configFile.ProfileName]))
                throw new CLogEnterReadOnlyModeException("MissingConfiguration:" + macro.MacroConfiguration[configFile.ProfileName] + " in profile=" + configFile.ProfileName + ", used by macro=" + macro.MacroName, CLogHandledException.ExceptionType.RequiredConfigParameterUnspecified, match);

            return configFile.MacroConfigurations[macro.MacroConfiguration[configFile.ProfileName]];
        }

        public string GetConfigurationValue(string moduleName, string key)
        {
            string ret;
            if (null != macro.CustomSettings && macro.CustomSettings.ContainsKey(moduleName))
            {
                if (macro.CustomSettings[moduleName].TryGetValue(key, out ret))
                    return ret;
            }

            CLogExportModuleDefination moduleSettings = GetMacroConfigurationProfile().FindExportModule(moduleName);

            if (null == moduleSettings || !moduleSettings.CustomSettings.ContainsKey("Priority"))
                throw new CLogEnterReadOnlyModeException("Priority", CLogHandledException.ExceptionType.RequiredConfigParameterUnspecified, match);

            return moduleSettings.CustomSettings["Priority"];
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
                ModuleProperites[module] = new SortedDictionary<string, string>();
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
