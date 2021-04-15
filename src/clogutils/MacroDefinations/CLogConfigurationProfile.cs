/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    JSON configuration for an export module

--*/

using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace clogutils.MacroDefinations
{
    [JsonObject(MemberSerialization.OptIn)]
    public class CLogConfigurationProfile
    {
        [JsonProperty]
        public List<CLogExportModuleDefination> Modules = new List<CLogExportModuleDefination>();

        [JsonProperty] public bool SkipProcessing { get; set; } = false;

        public CLogExportModuleDefination FindExportModule(string moduleName)
        {
            return Modules.Where(x => { return x.ExportModule.Equals(moduleName); }).FirstOrDefault();
        }

        public List<string> ModuleNames
        {
            get
            {
                List<string> ret = new List<string>();
                foreach (var mod in Modules)
                {
                    ret.Add(mod.ExportModule);
                }
                return ret;
            }
        }

        public bool ShouldSerializeSkipProcessing()
        {
            return SkipProcessing;
        }
    }
}
