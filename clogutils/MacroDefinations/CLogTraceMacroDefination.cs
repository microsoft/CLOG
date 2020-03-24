/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    Describes a CLOG macro,  this class will be converted into JSON and stored within a CLOG configuration file

--*/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;
using System.Linq;

namespace clogutils.MacroDefinations
{

    [JsonObject(MemberSerialization.OptIn)]
    public class CLogExportModuleDefination
    {
        [JsonProperty]
        public string ExportModule;

        [JsonProperty]
        public Dictionary<string, string> CustomSettings { get; set; } = new Dictionary<string, string>();
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class CLogConfigurationProfile
    {
        [JsonProperty]
        public List<CLogExportModuleDefination> Modules = new List<CLogExportModuleDefination>();

        public CLogExportModuleDefination FindExportModule(string moduleName)
        {
            return Modules.Where(x => { return x.ExportModule.Equals(moduleName); } ).FirstOrDefault();
        }

        public List<string> ModuleNames
        {
            get
            {
                List<string> ret = new List<string>();
                foreach(var mod in Modules)
                {
                    ret.Add(mod.ExportModule);
                }
                return ret;
            }
        }
    }


    [JsonObject(MemberSerialization.OptIn)]
    public class CLogTraceMacroDefination
    {
        [JsonProperty] public string MacroName { get; set; }

        public bool SkipProcessing { get; set; }

        [JsonProperty] public string EncodedPrefix { get; set; }

        [JsonProperty] public virtual int EncodedArgNumber { get; set; }
              
        [JsonProperty] public Dictionary<string, string> MacroConfiguration { get; set; }

        public string ConfigFileWithMacroDefination { get; set; }
               
        [DefaultValue(CLogUniqueIDEncoder.Basic)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public CLogUniqueIDEncoder UniqueIdEncoder { get; set; }

        public string CombinePrefixWithEncodedString(string encodedString)
        {
            return EncodedPrefix + encodedString.Substring(1, encodedString.Length - 2);
        }

        public void DecodeUniqueId(CLogLineMatch m, string arg, out string id, out int idHash)
        {
            id = null;

            switch (UniqueIdEncoder)
            {
                case CLogUniqueIDEncoder.Basic:
                    idHash = Math.Abs(arg.GetHashCode());
                    id = arg;
                    break;

                case CLogUniqueIDEncoder.StringAndNumerical:
                {
                    int lastIdx = arg.LastIndexOf('_');

                    if (-1 == lastIdx)
                        throw new CLogEnterReadOnlyModeException(
                            "Unique ID is not in the correct format, please follow the 'StringAndNumerical' format", m);

                    id = arg.Substring(0, lastIdx);

                    if (string.IsNullOrEmpty(id))
                    {
                        throw new CLogEnterReadOnlyModeException(
                            "Unique ID is not in the correct format, please follow the 'StringAndNumerical' format", m);
                    }

                    try
                    {
                        idHash = Convert.ToInt32(arg.Substring(lastIdx + 1));
                    }
                    catch (FormatException)
                    {
                        throw new CLogEnterReadOnlyModeException(
                            "Unique ID was located but is not in the correct format, please follow the 'StringAndNumerical' format",
                            m);
                    }
                }
                    break;

                default:
                    throw new CLogEnterReadOnlyModeException("Invalid ID Encoder", m);
            }
        }

        public bool ShouldSerializeUniqueIdEncoder()
        {
            return UniqueIdEncoder != CLogUniqueIDEncoder.Basic;
        }
    }
}
