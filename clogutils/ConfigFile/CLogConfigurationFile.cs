/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    This class both serves to contain one configuration file.   Becuase config files may be chained, this class may contain 
    other instances of configuration files.

--*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using clogutils.MacroDefinations;
using Newtonsoft.Json;

namespace clogutils.ConfigFile
{
    [JsonObject(MemberSerialization.OptIn)]
    public class CLogConfigurationFile
    {
        public static int _version = 1;

        private static HashSet<string> _loadedConfigFiles = new HashSet<string>();

        public string BUGBUG_String
        {
            get;
            set;
        }
        public string FilePath
        {
            get;
            set;
        }

        public bool OverwriteHashCollisions
        {
            get;
            set;
        }

        [JsonProperty] public int Version
        {
            get;
            set;
        }

        [JsonProperty] public string CustomTypeClogCSharpFile
        {
            get;
            set;
        }

        [JsonProperty] public CLogTypeEncoder TypeEncoders
        {
            get;
            set;
        }

        [JsonProperty] public List<CLogTraceMacroDefination> SourceCodeMacros
        {
            get;
            set;
        } = new List<CLogTraceMacroDefination>();

        [JsonProperty] private List<string> ChainedConfigFiles
        {
            get;
            set;
        }

        [JsonProperty] public CLogModuleUsageInformation ModuleUniqueness
        {
            get;
            set;
        } = new CLogModuleUsageInformation();

        public List<CLogConfigurationFile> _chainedConfigFiles
        {
            get;
            set;
        }

        public CLogTypeEncoder InUseTypeEncoders
        {
            get;
            set;
        } = new CLogTypeEncoder();

        public bool IsDirty
        {
            get;
            set;
        }

        public CLogEncodingCLogTypeSearch FindType(CLogFileProcessor.CLogVariableBundle bundle, CLogDecodedTraceLine traceLine)
        {
            int idx = 0;
            return FindTypeAndAdvance(bundle.DefinationEncoding, traceLine, null, ref idx);
        }

        public CLogEncodingCLogTypeSearch FindTypeAndAdvance(string encoded, CLogDecodedTraceLine traceLine, CLogLineMatch traceLineMatch,
                ref int index)
        {
            int tempIndex = index;
            CLogEncodingCLogTypeSearch ret = null;
            CLogTypeNotFoundException previousException = null;

            try
            {
                if(null != (ret = TypeEncoders.FindTypeAndAdvance(encoded, traceLine, traceLineMatch, ref tempIndex)))
                {
                    InUseTypeEncoders.AddType(ret);
                    index = tempIndex;
                    return ret;
                }
            }
            catch(CLogTypeNotFoundException e)
            {
                previousException = e;
            }

            foreach(var config in _chainedConfigFiles)
            {
                try
                {
                    if(null != (ret = config.TypeEncoders.FindTypeAndAdvance(encoded, traceLine, traceLineMatch, ref tempIndex)))
                    {
                        InUseTypeEncoders.AddType(ret);
                        index = tempIndex;
                        return ret;
                    }
                }
                catch(CLogTypeNotFoundException e)
                {
                    previousException = e;
                }
            }

            CLogErrors.ReportUnspecifiedCLogType(previousException.PartialType, traceLineMatch);
            throw previousException;
        }

        public CLogTraceMacroDefination[] AllKnownMacros()
        {
            Dictionary<string, CLogTraceMacroDefination> ret = new Dictionary<string, CLogTraceMacroDefination>();

            foreach(var config in _chainedConfigFiles)
            {
                foreach(var def in config.AllKnownMacros())
                {
                    if(ret.ContainsKey(def.MacroName))
                    {
                        Console.WriteLine($"Macro defined twice in chained config files {def.MacroName}");
                        throw new CLogEnterReadOnlyModeException("DuplicateMacro", null);
                    }

                    ret[def.MacroName] = def;
                    def.ConfigFileWithMacroDefination = config.FilePath;
                }
            }

            foreach(var def in SourceCodeMacros)
            {
                if(ret.ContainsKey(def.MacroName))
                {
                    Console.WriteLine($"Macro defined twice in chained config files {def.MacroName}");
                    throw new CLogEnterReadOnlyModeException("DuplicateMacro", null);
                }

                ret[def.MacroName] = def;
                def.ConfigFileWithMacroDefination = this.FilePath;
            }

            return ret.Values.ToArray();
        }

        public static CLogConfigurationFile FromFile(string fileName)
        {
            if(_loadedConfigFiles.Contains(fileName))
            {
                Console.WriteLine($"Circular config file detected {fileName}");
                throw new CLogEnterReadOnlyModeException("CircularConfigFilesNotAllowed", null);
            }

            _loadedConfigFiles.Add(fileName);
            string json = File.ReadAllText(fileName);

            JsonSerializerSettings s = new JsonSerializerSettings();
            s.Context = new StreamingContext(StreamingContextStates.Other, json);

            CLogConfigurationFile ret = JsonConvert.DeserializeObject<CLogConfigurationFile>(json, s);
            ret.FilePath = fileName;
            ret._chainedConfigFiles = new List<CLogConfigurationFile>();

            if(!string.IsNullOrEmpty(ret.CustomTypeClogCSharpFile))
            {
                string cSharp = Path.GetDirectoryName(fileName);
                cSharp = Path.Combine(cSharp, ret.CustomTypeClogCSharpFile);
                ret.TypeEncoders.LoadCustomCSharp(cSharp, ret);
            }

            //
            // Do sanity checks on the input configuration file - look for common (and easy) to make mistakes
            //
            HashSet<string> macros = new HashSet<string>();

            foreach(var m in ret.SourceCodeMacros)
            {
                if(macros.Contains(m.MacroName))
                {
                    Console.WriteLine(
                        $"Macro {m.MacroName} specified multiple times - each macro may only be specified once in the config file");
                    throw new CLogEnterReadOnlyModeException("MultipleMacrosWithSameName", null);
                }

                macros.Add(m.MacroName);
            }

            foreach(string downstream in ret.ChainedConfigFiles)
            {
                string root = Path.GetDirectoryName(fileName);
                string toOpen = Path.Combine(root, downstream);

                if(!File.Exists(toOpen))
                {
                    Console.WriteLine($"Chained config file {toOpen} not found");
                    throw new CLogEnterReadOnlyModeException("ChainedConfigFileNotFound", null);
                }

                var configFile = FromFile(toOpen);
                ret._chainedConfigFiles.Add(configFile);
            }

            ret.InUseTypeEncoders = new CLogTypeEncoder();

            return ret;
        }

        private string ToJson()
        {
            JsonSerializerSettings s = new JsonSerializerSettings();
            s.Formatting = Formatting.Indented;
            string me = JsonConvert.SerializeObject(this, Formatting.Indented);
            return me;
        }

        public void UpdateAndSave()
        {
            File.WriteAllText(this.FilePath, ToJson());

            foreach(var child in this._chainedConfigFiles)
            {
                child.UpdateAndSave();
            }
        }
    }
}
