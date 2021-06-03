/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    Define an export module

--*/

using System.Collections.Generic;
using Newtonsoft.Json;

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
}
