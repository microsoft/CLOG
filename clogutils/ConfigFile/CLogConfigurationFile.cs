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
using clogutils.MacroDefinations;
using Newtonsoft.Json;
using static clogutils.CLogConsoleTrace;

namespace clogutils.ConfigFile
{
    [JsonObject(MemberSerialization.OptIn)]
    public class CLogConfigurationFile
    {
        public static int _version = 1;

        private static HashSet<string> _loadedConfigFiles = new HashSet<string>();

        public string ScopePrefix
        {
            get;
            set;
        }

        public string ProfileName
        {
            get; set;
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

        [JsonProperty] public Dictionary<string, CLogConfigurationProfile> MacroConfigurations = new Dictionary<string, CLogConfigurationProfile>();


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
        public List<CLogConfigurationFile> _chainedConfigFiles
        {
            get;
            set;
        } = new List<CLogConfigurationFile>();

        [JsonProperty]
        public bool MarkPhase
        {
            get;
            set;
        } = false;

        /// <summary>
        /// If the MarkPhase bit isnt set, dont clutter up the config file with it - it's a rare feature and we'd like to reduce confusion/complexity
        /// </summary>
        /// <returns>true/false if the MarkPhase should be serialized into json</returns>
        public bool ShouldSerializeMarkPhase()
        {
            return MarkPhase;
        }

        public CLogTypeEncoder InUseTypeEncoders
        {
            get;
            set;
        } = new CLogTypeEncoder();

        public bool AmIDirty
        {
            get;
            set;
        }

        public bool AreWeDirty()
        {
            bool dirty = AmIDirty;
            foreach (var config in _chainedConfigFiles)
            {
                dirty |= config.AreWeDirty();
            }
            return dirty;
        }

        public bool AreWeInMarkPhase()
        {
            bool dirty = MarkPhase;
            foreach (var config in _chainedConfigFiles)
            {
                dirty |= config.AreWeInMarkPhase();
            }
            return dirty;
        }

        public CLogEncodingCLogTypeSearch FindType(CLogFileProcessor.CLogVariableBundle bundle)
        {
            return FindType(bundle, (CLogLineMatch)null);
        }

        public CLogEncodingCLogTypeSearch FindType(CLogFileProcessor.CLogVariableBundle bundle, CLogDecodedTraceLine traceLineMatch)
        {
            return FindType(bundle, traceLineMatch.match);
        }

        public CLogEncodingCLogTypeSearch FindType(CLogFileProcessor.CLogVariableBundle bundle, CLogLineMatch traceLineMatch)
        {
            int idx = 0;
            return FindTypeAndAdvance(bundle.DefinationEncoding, traceLineMatch, ref idx);
        }

        public string DecodeUsingCustomDecoder(CLogEncodingCLogTypeSearch node, IClogEventArg value, CLogLineMatch traceLine)
        {
            CLogCustomDecoderNotFoundException oldException = null;

            try
            {
                return TypeEncoders.DecodeUsingCustomDecoder(node, value, traceLine);
            }
            catch (CLogCustomDecoderNotFoundException e)
            {
                oldException = e;
            }

            foreach (var config in _chainedConfigFiles)
            {
                try
                {
                    return TypeEncoders.DecodeUsingCustomDecoder(node, value, traceLine);
                }
                catch (CLogCustomDecoderNotFoundException e)
                {
                    oldException = e;
                }
            }

            return "ERROR:CustomDecoderNotFound:" + node.CustomDecoder + ":" + node.DefinationEncoding;
        }


        public CLogEncodingCLogTypeSearch FindTypeAndAdvance(string encoded, CLogLineMatch traceLineMatch, ref int index)
        {
            int tempIndex = index;
            CLogEncodingCLogTypeSearch ret = null;

            if (null != (ret = TypeEncoders.FindTypeAndAdvance(encoded, traceLineMatch, ref tempIndex)))
            {
                InUseTypeEncoders.AddType(ret);
                index = tempIndex;
                return ret;
            }

            foreach (var config in _chainedConfigFiles)
            {
                tempIndex = index;
                if (null != (ret = config.TypeEncoders.FindTypeAndAdvance(encoded, traceLineMatch, ref tempIndex)))
                {
                    InUseTypeEncoders.AddType(ret);
                    index = tempIndex;
                    return ret;
                }
            }

            throw new CLogTypeNotFoundException("InvalidType:" + encoded, encoded, traceLineMatch);
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
                        throw new CLogEnterReadOnlyModeException("DuplicateMacro", CLogHandledException.ExceptionType.DuplicateMacro, null);
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
                    throw new CLogEnterReadOnlyModeException("DuplicateMacro", CLogHandledException.ExceptionType.DuplicateMacro, null);
                }

                ret[def.MacroName] = def;
                def.ConfigFileWithMacroDefination = this.FilePath;
            }

            return ret.Values.ToArray();
        }

        public static CLogConfigurationFile FromFile(string fileName)
        {
            if (_loadedConfigFiles.Contains(fileName))
            {
                Console.WriteLine($"Circular config file detected {fileName}");
                throw new CLogEnterReadOnlyModeException("CircularConfigFilesNotAllowed", CLogHandledException.ExceptionType.CircularConfigFilesNotAllowed, null);
            }

            _loadedConfigFiles.Add(fileName);
            string json = File.ReadAllText(fileName);

            JsonSerializerSettings s = new JsonSerializerSettings();
            s.Context = new StreamingContext(StreamingContextStates.Other, json);

            CLogConfigurationFile ret = JsonConvert.DeserializeObject<CLogConfigurationFile>(json, s);
            ret.FilePath = fileName;
            ret._chainedConfigFiles = new List<CLogConfigurationFile>();

            if (!string.IsNullOrEmpty(ret.CustomTypeClogCSharpFile))
            {
                string cSharp = Path.GetDirectoryName(fileName);
                cSharp = Path.Combine(cSharp, ret.CustomTypeClogCSharpFile);
                ret.TypeEncoders.LoadCustomCSharp(cSharp, ret);
            }

            //
            // Do sanity checks on the input configuration file - look for common (and easy) to make mistakes
            //
            HashSet<string> macros = new HashSet<string>();

            foreach (var m in ret.SourceCodeMacros)
            {
                if (macros.Contains(m.MacroName))
                {
                    Console.WriteLine(
                        $"Macro {m.MacroName} specified multiple times - each macro may only be specified once in the config file");
                    throw new CLogEnterReadOnlyModeException("MultipleMacrosWithSameName", CLogHandledException.ExceptionType.MultipleMacrosWithSameName, null);
                }

                macros.Add(m.MacroName);
            }

            foreach (string downstream in ret.ChainedConfigFiles)
            {
                string root = Path.GetDirectoryName(fileName);
                string toOpen = Path.Combine(root, downstream);

                if (!File.Exists(toOpen))
                {
                    Console.WriteLine($"Chained config file {toOpen} not found");
                    throw new CLogEnterReadOnlyModeException("ChainedConfigFileNotFound", CLogHandledException.ExceptionType.UnableToOpenChainedConfigFile, null);
                }

                var configFile = FromFile(toOpen);
                ret._chainedConfigFiles.Add(configFile);
            }

            ret.InUseTypeEncoders = new CLogTypeEncoder();
                        
            RefreshTypeEncodersMarkBit(ret, ret.MarkPhase);

            return ret;
        }

        /// <summary>
        /// Based on the config files 'mark' bit, update type encoders to persist their usage metrics on save
        /// </summary>
        /// <param name="ret"></param>
        private static void RefreshTypeEncodersMarkBit(CLogConfigurationFile ret, bool markBit)
        {
            ret.MarkPhase = markBit;
            foreach (var t in ret.TypeEncoders.FlattendTypeEncoder)
            {
                t.MarkPhase = markBit;
            }

            foreach(var config in ret._chainedConfigFiles)
            {
                RefreshTypeEncodersMarkBit(config, markBit);
            }
        }

        private string ToJson()
        {
            JsonSerializerSettings s = new JsonSerializerSettings();
            s.Formatting = Formatting.Indented;
            string me = JsonConvert.SerializeObject(this, Formatting.Indented);
            return me;
        }

        public void Lint()
        {
            this.MarkPhase = false;

            TypeEncoders.Lint();

            foreach (var child in this._chainedConfigFiles)
            {
                child.Lint();
            }

            RefreshTypeEncodersMarkBit(this, false);
        }

        public void UpdateVersion()
        {
#if false
            if (0 == this.Version)
            {
                string config = Path.GetFileNameWithoutExtension(this.FilePath);

                foreach (var mod in this.SourceCodeMacros)
                {
                    if (mod.CLogConfigurationProfiles.ContainsKey(config))
                        continue;

                    CLogConfigurationProfile profile = new CLogConfigurationProfile();

                    foreach (var module in mod.CLogExportModules)
                    {
                        CLogExportModuleDefination newModule = new CLogExportModuleDefination();
                        newModule.ExportModule = module;
                        foreach (var setting in mod.CustomSettings)
                            newModule.CustomSettings[setting.Key] = setting.Value;

                        profile.Modules.Add(newModule);
                    }

                    mod.CLogConfigurationProfiles.Add(config, profile);
                }
                this.Version = 1;

                foreach (var child in this._chainedConfigFiles)
                {
                    child.UpdateVersion();
                }
            }
#endif
        }

        public void Save()
        {
            File.WriteAllText(this.FilePath, ToJson());

            foreach(var child in this._chainedConfigFiles)
            {
                child.Save();
            }
        }
    }
}
