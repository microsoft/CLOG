/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    Describes the file that contains decoding information for a trace containing CLOG data.  This file must be presented (by the user) to decode lines of trace

--*/

using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using clogutils.ConfigFile;
using Newtonsoft.Json;
using static clogutils.CLogConsoleTrace;

namespace clogutils
{
    [JsonObject(MemberSerialization.OptIn)]
    public class CLogSidecar : ICLogOutputModule
    {
        private static int _maxVersion = 1;
        private string _sidecarFileName;

        [JsonProperty] private Dictionary<string, Dictionary<string, Dictionary<string, string>>> ModuleTraceData = new Dictionary<string, Dictionary<string, Dictionary<string, string>>>();

        public CLogSidecar()
        {
            Version = _maxVersion;
            EventBundlesV2 = new Dictionary<string, CLogDecodedTraceLine>();
        }

        private CLogSidecar(string sidecarFileName)
        {
            Version = _maxVersion;
            EventBundlesV2 = new Dictionary<string, CLogDecodedTraceLine>();
            _sidecarFileName = sidecarFileName;
        }

        [JsonProperty] public Dictionary<string, CLogDecodedTraceLine> EventBundlesV2 { get; set; } = new Dictionary<string, CLogDecodedTraceLine>();

        private Dictionary<string, CLogDecodedTraceLine> HotEventBundles { get; set; } = new Dictionary<string, CLogDecodedTraceLine>();


        [JsonProperty] public int Version { get; set; }


        [JsonProperty] public CLogConfigurationFile? ConfigFile { get; set; }

        [JsonProperty]
        public CLogModuleUsageInformation ModuleUniqueness
        {
            get;
            set;
        } = new CLogModuleUsageInformation();

        public void UpdateConfigFile(CLogConfigurationFile newConfigFile)
        {
            // Ideally, we'd use CLogConfigurationFile.AreWeDirty, but because of the tricks
            // we do around serialization, we can't without a major refactor

            if (newConfigFile == null || ConfigFile == null)
            {
                AreDirty = true;
                ConfigFile = newConfigFile;
                return;
            }

            // Serialize old config
            bool old = ConfigFile.SerializeChainedConfigurations;
            ConfigFile.SerializeChainedConfigurations = true;

            string serializedOldConfig = JsonConvert.SerializeObject(ConfigFile);

            ConfigFile.SerializeChainedConfigurations = old;

            // Serialize new config
            old = newConfigFile.SerializeChainedConfigurations;
            newConfigFile.SerializeChainedConfigurations = true;

            string serializedNewConfig = JsonConvert.SerializeObject(newConfigFile);

            newConfigFile.SerializeChainedConfigurations = old;

            if (serializedOldConfig != serializedNewConfig)
            {
                AreDirty = true;
                ChangesList.Add("Configuration file or dependencies changed");
            }
            ConfigFile = newConfigFile;
        }

        private List<string> ChangesList = new List<string>();
        public void PrintDirtyReasons()
        {
            foreach (string s in ChangesList)
            {
                CLogConsoleTrace.TraceLine(TraceType.Err, "Config Changed : " + s);
            }
        }

        public void InsertTraceLine(ICLogOutputModule module, CLogDecodedTraceLine traceLine)
        {
            CLogTraceLineInformation output;
            if (ModuleUniqueness.IsUnique(module, traceLine, out output) && null != output)
                return;

            AreDirty = true;
            ChangesList.Add("Inserting : " + traceLine.UniqueId);
            ModuleUniqueness.Insert(module, traceLine);
        }

        public void RemoveTraceLine(CLogTraceLineInformation traceLine)
        {
            AreDirty = true;
            ChangesList.Add("Removed : " + traceLine.TraceID);
            ModuleUniqueness.Remove(traceLine);

            EventBundlesV2.Remove(traceLine.TraceID);
        }

        public string ModuleName
        {
            get { return "SIDECAR_EMITTER"; }
        }

        public bool ManditoryModule
        {
            get { return true; }
        }


        public void TraceLineDiscovered(string sourceFile, CLogDecodedTraceLine traceLine, CLogSidecar sidecar, StringBuilder macroPrefix, StringBuilder inline, StringBuilder function)
        {
            int hashUInt;
            string? hash;

            traceLine.macro.DecodeUniqueId(traceLine.match, traceLine.UniqueId, out hash, out hashUInt);
            HotEventBundles[hash] = traceLine;
        }

        public void InitHeader(StringBuilder header)
        {
        }

        public void Save(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return;
            }

            string s = ToJson();
            File.WriteAllText(fileName, s);
        }

        public void FinishedProcessing(StringBuilder header, StringBuilder sourceFile)
        {
            Save(_sidecarFileName);
        }

        public CLogEncodingCLogTypeSearch FindTypeX(CLogFileProcessor.CLogVariableBundle bundle, CLogLineMatch traceLineMatch)
        {
            int idx = 0;
            return ConfigFile.FindTypeAndAdvance(bundle.DefinationEncoding, traceLineMatch, ref idx);
        }

        public void SetTracelineMetadata(CLogDecodedTraceLine traceline, string module, Dictionary<string, string> values)
        {
            if (!ModuleTraceData.ContainsKey(module))
            {
                ModuleTraceData[module] = new Dictionary<string, Dictionary<string, string>>();
            }

            ModuleTraceData[module][traceline.UniqueId] = values;
        }

        public Dictionary<string, string>? GetTracelineMetadata(CLogDecodedTraceLine traceline, string module)
        {
            if (!ModuleTraceData.ContainsKey(module))
            {
                ModuleTraceData[module] = new Dictionary<string, Dictionary<string, string>>();
            }

            if (!ModuleTraceData[module].ContainsKey(traceline.UniqueId))
            {
                return null;
            }

            return ModuleTraceData[module][traceline.UniqueId];
        }

        private void MergeHot()
        {
            //
            // Merge in hot values
            // 
            foreach (var hot in HotEventBundles)
            {
                CLogDecodedTraceLine old;
                if (EventBundlesV2.TryGetValue(hot.Key, out old))
                {
                    foreach (var x in hot.Value.ModuleProperites)
                    {
                        if (!old.ModuleProperites.ContainsKey(x.Key))
                        {
                            old.ModuleProperites[x.Key] = x.Value;
                            AreDirty = true;
                        }
                        else
                        {
                            foreach (var y in hot.Value.ModuleProperites[x.Key])
                            {
                                if (!old.ModuleProperites[x.Key].ContainsKey(y.Key))
                                {
                                    old.ModuleProperites[x.Key][y.Key] = y.Value;
                                    AreDirty = true;
                                }
                                else if (!old.ModuleProperites[x.Key][y.Key].Equals(y.Value))
                                {
                                    old.ModuleProperites[x.Key][y.Key] = y.Value;
                                    AreDirty = true;
                                }
                            }
                        }
                    }
                }
                else
                {
                    EventBundlesV2[hot.Key] = hot.Value;
                    ChangesList.Add("Added New Event: " + hot.Key);
                    AreDirty = true;
                }
            }
        }

        private bool _areDirty = false;
        public bool AreDirty
        {
            get { MergeHot(); return _areDirty; }
            private set { _areDirty = value; }
        }

        public CLogDecodedTraceLine? FindBundle(string uid)
        {
            string name = uid.Split(':')[1];

            if (!EventBundlesV2.ContainsKey(name))
            {
                return null;
            }

            return EventBundlesV2[name];
        }

        public string ToJson()
        {
            bool old = ConfigFile.SerializeChainedConfigurations;
            ConfigFile.SerializeChainedConfigurations = true;

            JsonSerializerSettings s = new JsonSerializerSettings();
            s.Formatting = Formatting.Indented;
            string me = JsonConvert.SerializeObject(this, Formatting.Indented);

            ConfigFile.SerializeChainedConfigurations = old;
            return me;
        }

        public static CLogSidecar? FromJson(string json)
        {
            JsonSerializerSettings s = new JsonSerializerSettings();
            s.Context = new StreamingContext(StreamingContextStates.Other, json);

            CLogSidecar? ret = JsonConvert.DeserializeObject<CLogSidecar>(json, s);

            return ret;
        }
    }
}
