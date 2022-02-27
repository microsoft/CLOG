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
    public class ClogSidecar_V1
    {
        [JsonProperty] public int Version { get; set; }

        [JsonProperty]
        public SortedDictionary<string, CLogDecodedTraceLine> EventBundlesV2 { get; set; } = new SortedDictionary<string, CLogDecodedTraceLine>();

        [JsonProperty] public CLogConfigurationFile ConfigFile { get; set; }

        [JsonProperty]
        public CLogModuleUsageInformation_V1 ModuleUniqueness
        {
            get;
            set;
        }

        private ClogSidecar_V1()
        {
            Version = 1;
            ModuleUniqueness = new CLogModuleUsageInformation_V1();
        }

        public string ToJson()
        {
            JsonSerializerSettings s = new JsonSerializerSettings();
            s.Formatting = Formatting.Indented;
            this.Version = 1;
            string me = JsonConvert.SerializeObject(this, Formatting.Indented);
            return me;
        }

        public static ClogSidecar_V1 FromJson(string json)
        {
            JsonSerializerSettings s = new JsonSerializerSettings();
            s.Context = new StreamingContext(StreamingContextStates.Other, json);

            ClogSidecar_V1 ret = JsonConvert.DeserializeObject<ClogSidecar_V1>(json, s);
            return ret;
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class ClogSidecar_V2
    {
        [JsonProperty] public int Version { get; set; }

        [JsonProperty]
        public SortedDictionary<string, CLogDecodedTraceLine> EventBundlesV2 { get; set; } = new SortedDictionary<string, CLogDecodedTraceLine>();

        [JsonProperty] public CLogConfigurationFile ConfigFile { get; set; }

        [JsonProperty]
        public CLogModuleUsageInformation_V2 ModuleUniqueness
        {
            get;
            set;
        }
        public ClogSidecar_V2()
        {
            Version = 2;
            ModuleUniqueness = new CLogModuleUsageInformation_V2();
        }

        public string ToJson()
        {
            ModuleUniqueness.Sort();

            JsonSerializerSettings s = new JsonSerializerSettings();
            s.Formatting = Formatting.Indented;
            this.Version = 2;
            string me = JsonConvert.SerializeObject(this, Formatting.Indented);
            return me;
        }

        public static ClogSidecar_V2 FromJson(string json)
        {
            try
            {
                JsonSerializerSettings s = new JsonSerializerSettings();
                s.Context = new StreamingContext(StreamingContextStates.Other, json);

                ClogSidecar_V2 ret = JsonConvert.DeserializeObject<ClogSidecar_V2>(json, s);

                if (1 == ret.Version)
                {
                    ClogSidecar_V1 v1 = ClogSidecar_V1.FromJson(json);
                    ret = new ClogSidecar_V2();
                    ret.Version = 2;
                    ret.EventBundlesV2 = v1.EventBundlesV2;
                    ret.ConfigFile = v1.ConfigFile;
                    ret.ModuleUniqueness = CLogModuleUsageInformation_V2.ConvertFromV1(v1.ModuleUniqueness);
                }

                if (2 != ret.Version)
                    throw new CLogEnterReadOnlyModeException($"Invalid sidecar file version {ret.Version}, our max version is 2 - consider updating CLOG", CLogHandledException.ExceptionType.SidecarFileVersionMismatch, null);
                return ret;
            }
            catch(System.Exception e)
            {
                throw new CLogEnterReadOnlyModeException($"Invalid sidecar file version - it looks corrupted", CLogHandledException.ExceptionType.SidecarCorrupted, null, e);
            }
        }
    }

    public class CLogSidecar : ICLogOutputModule
    {
        private string _sidecarFileName;

        private SortedDictionary<string, Dictionary<string, Dictionary<string, string>>> ModuleTraceData = new SortedDictionary<string, Dictionary<string, Dictionary<string, string>>>();

        private ClogSidecar_V2 _sideCarFile;

        private CLogModuleUsageInformation _myUsageModuleInfo;

        private void SetSideCar(ClogSidecar_V2 file)
        {
            _sideCarFile = file;
            _myUsageModuleInfo = new CLogModuleUsageInformation(file.ModuleUniqueness);
        }

        public CLogSidecar()
        {
            SetSideCar(new ClogSidecar_V2());
        }

        private CLogSidecar(ClogSidecar_V2 me)
        {
            SetSideCar(me);
        }
        private CLogSidecar(string sidecarFileName)
        {
            SetSideCar(new ClogSidecar_V2());
            _sidecarFileName = sidecarFileName;
        }
        public void SetConfigFile(CLogConfigurationFile newConfig)
        {
            _sideCarFile.ConfigFile = newConfig;
        }

        public CLogModuleUsageInformation ModuleUniqueness
        {
            get { return _myUsageModuleInfo; }
        }
        public CLogConfigurationFile ConfigFile
        {
            get { return _sideCarFile.ConfigFile; }
        }

        public IEnumerable<KeyValuePair<string, CLogDecodedTraceLine>> EventBundlesV2
        {
            get
            {
                return _sideCarFile.EventBundlesV2;
            }
        }

        private Dictionary<string, CLogDecodedTraceLine> HotEventBundles { get; set; } = new Dictionary<string, CLogDecodedTraceLine>();

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
            CLogTraceLineInformation_V2 output;
            if (_myUsageModuleInfo.IsUnique(module, traceLine, out output) && null != output)
                return;

            AreDirty = true;
            ChangesList.Add("Inserting : " + traceLine.UniqueId);
            _myUsageModuleInfo.Insert(module, traceLine);
        }
        public void RemoveTraceLine(CLogTraceLineInformation_V2 traceLine)
        {
            AreDirty = true;
            ChangesList.Add("Removed : " + traceLine.TraceID);
            _myUsageModuleInfo.Remove(traceLine);

            _sideCarFile.EventBundlesV2.Remove(traceLine.TraceID);
        }

        public string ModuleName
        {
            get { return "SIDECAR_EMITTER"; }
        }
        public bool ManditoryModule
        {
            get { return true; }
        }

        public void TraceLineDiscovered(string sourceFile, CLogOutputInfo outputInfo, CLogDecodedTraceLine traceLine, CLogSidecar sidecar, StringBuilder macroPrefix, StringBuilder inline, StringBuilder function)
        {
            int hashUInt;
            string hash;

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

        public void FinishedProcessing(CLogOutputInfo outputInfo, StringBuilder header, StringBuilder sourceFile)
        {
            Save(_sidecarFileName);
        }

        public CLogEncodingCLogTypeSearch FindTypeX(CLogFileProcessor.CLogVariableBundle bundle, CLogLineMatch traceLineMatch)
        {
            int idx = 0;
            return _sideCarFile.ConfigFile.FindTypeAndAdvance(bundle.DefinationEncoding, traceLineMatch, ref idx);
        }

        public void SetTracelineMetadata(CLogDecodedTraceLine traceline, string module, Dictionary<string, string> values)
        {
            if (!ModuleTraceData.ContainsKey(module))
            {
                ModuleTraceData[module] = new Dictionary<string, Dictionary<string, string>>();
            }

            ModuleTraceData[module][traceline.UniqueId] = values;
        }

        public Dictionary<string, string> GetTracelineMetadata(CLogDecodedTraceLine traceline, string module)
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
                if (_sideCarFile.EventBundlesV2.TryGetValue(hot.Key, out old))
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
                    _sideCarFile.EventBundlesV2[hot.Key] = hot.Value;
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

        public CLogDecodedTraceLine FindBundle(string name)
        {
            if (!_sideCarFile.EventBundlesV2.ContainsKey(name))
            {
                return null;
            }

            return _sideCarFile.EventBundlesV2[name];
        }

        public string ToJson()
        {
            bool old = _sideCarFile.ConfigFile.SerializeChainedConfigurations;
            _sideCarFile.ConfigFile.SerializeChainedConfigurations = true;

            string me = _sideCarFile.ToJson();

            _sideCarFile.ConfigFile.SerializeChainedConfigurations = old;
            return me;
        }

        public static CLogSidecar FromJson(string json)
        {
            ClogSidecar_V2 me = ClogSidecar_V2.FromJson(json);
            CLogSidecar ret = new CLogSidecar(me);
            return ret;
        }
    }
}
