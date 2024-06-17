/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    This class both serves to contain one configuration file.   Becuase config files may be chained, this class may contain
    other instances of configuration files.

--*/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using clogutils.MacroDefinations;
using Newtonsoft.Json;

namespace clogutils.ConfigFile
{
    [JsonObject(MemberSerialization.OptIn)]
    public class CLogConfigurationFile
    {
        public static int _version = 1;

        public const string EmbeddedDefaultName = "DEFAULTS";

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

        public bool DeveloperMode
        {
            get;
            set;
        } = false;

        [JsonProperty]
        public int Version
        {
            get;
            set;
        }

        [JsonProperty]
        public string CustomTypeClogCSharpFile
        {
            get;
            set;
        }

        public bool ShouldSerializeCustomTypeClogCSharpFileContents()
        {
            return !String.IsNullOrEmpty(CustomTypeClogCSharpFileContents);
        }

        [JsonProperty]
        public string CustomTypeClogCSharpFileContents
        {
            get;
            set;
        }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(20)]
        public int MaximumVariableLength
        {
            get;
            set;
        }

        [JsonProperty]
        public CLogTypeEncoder TypeEncoders
        {
            get;
            set;
        }

        [JsonProperty] public Dictionary<string, CLogConfigurationProfile> MacroConfigurations = new Dictionary<string, CLogConfigurationProfile>();


        [JsonProperty]
        public List<CLogTraceMacroDefination> SourceCodeMacros
        {
            get;
            set;
        } = new List<CLogTraceMacroDefination>();

        [JsonProperty]
        private List<string> ChainedConfigFiles
        {
            get;
            set;
        }


        [JsonProperty]
        public List<CLogConfigurationFile> ChainedConfigurations
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

        [JsonProperty]
        public bool EmitCFiles
        {
            get;
            set;
        } = true;

        public bool ShouldSerializeEmitCFiles()
        {
            return EmitCFiles == false;
        }


        private bool _SerializeChainConfiguration = false;
        public bool SerializeChainedConfigurations
        {
            get
            {
                return _SerializeChainConfiguration;
            }
            set
            {
                _SerializeChainConfiguration = value;

                if (value == true)
                {
                    CustomTypeClogCSharpFileContents = TypeEncoders.CustomCSharp;
                }
                else
                {
                    CustomTypeClogCSharpFileContents = null;
                }

                foreach (var c in ChainedConfigurations)
                {
                    c.SerializeChainedConfigurations = value;
                }
            }
        }

        public bool ShouldSerializeChainedConfigurations()
        {
            return SerializeChainedConfigurations;
        }


        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            if (!String.IsNullOrEmpty(this.CustomTypeClogCSharpFileContents))
                this.TypeEncoders.InitCustomDecoder(this.CustomTypeClogCSharpFileContents);
        }

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

        // TODO Actually make this set, including when the decoder files change
        public bool AmIDirty
        {
            get;
            private set;
        }

        public bool AreWeDirty()
        {
            bool dirty = AmIDirty;
            foreach (var config in ChainedConfigurations)
            {
                dirty |= config.AreWeDirty();
            }
            return dirty;
        }

        public bool AreWeInMarkPhase()
        {
            bool dirty = MarkPhase;
            foreach (var config in ChainedConfigurations)
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

        public bool DecodeUsingCustomDecoder(CLogEncodingCLogTypeSearch node, IClogEventArg value, CLogLineMatch traceLine, out string decodedValue)
        {
            if (!TypeEncoders.DecodeUsingCustomDecoder(node, value, traceLine, out decodedValue))
                return false;


            foreach (var config in ChainedConfigurations)
            {
                if (!config.DecodeUsingCustomDecoder(node, value, traceLine, out decodedValue))
                    return false;
            }

            decodedValue = "ERROR:CustomDecoderNotFound:" + node.CustomDecoder + ":" + node.DefinationEncoding;
            return false;
        }

        public CLogEncodingCLogTypeSearch FindTypeAndAdvance(string encoded, CLogLineMatch traceLineMatch, ref int index)
        {
            int tempIndex = index;
            CLogEncodingCLogTypeSearch ret = null;

            //
            // First scan the type encoders in our main config file
            //
            if (null != (ret = TypeEncoders.FindTypeAndAdvance(encoded, traceLineMatch, ref tempIndex)))
            {
                InUseTypeEncoders.AddType(ret);
                index = tempIndex;
                return ret;
            }

            //
            //  If we're unable to locate the type, iterate across each of our chained configs - note this is
            //     recursive;  such allowing for heirarchies
            //
            foreach (var config in ChainedConfigurations)
            {
                tempIndex = index;
                if (null != (ret = config.FindTypeAndAdvance(encoded, traceLineMatch, ref tempIndex)))
                {
                    InUseTypeEncoders.AddType(ret);
                    index = tempIndex;
                    return ret;
                }
            }

            //
            // Attempt to get something for the user about the actual arg;  note this may be error prone as we are now confused
            //   by their inputs
            //
            string surroundingArray = encoded.Substring(index, 15);
            int end = surroundingArray.IndexOf("}");
            if (-1 != end)
                surroundingArray = surroundingArray.Substring(0, end);

            throw new CLogTypeNotFoundException(encoded, "InvalidType:" + surroundingArray, traceLineMatch);
        }

        public CLogTraceMacroDefination[] AllKnownMacros()
        {
            Dictionary<string, CLogTraceMacroDefination> ret = new Dictionary<string, CLogTraceMacroDefination>();

            foreach (var config in ChainedConfigurations)
            {
                foreach (var def in config.AllKnownMacros())
                {
                    if (ret.ContainsKey(def.MacroName))
                    {
                        Console.WriteLine($"Macro defined twice in chained config files {def.MacroName}");
                        throw new CLogEnterReadOnlyModeException("DuplicateMacro", CLogHandledException.ExceptionType.DuplicateMacro, null);
                    }

                    ret[def.MacroName] = def;
                    def.ConfigFileWithMacroDefination = config.FilePath;
                }
            }

            foreach (var def in SourceCodeMacros)
            {
                if (ret.ContainsKey(def.MacroName))
                {
                    Console.WriteLine($"Macro defined twice in chained config files {def.MacroName}");
                    throw new CLogEnterReadOnlyModeException("DuplicateMacro", CLogHandledException.ExceptionType.DuplicateMacro, null);
                }

                ret[def.MacroName] = def;
                def.ConfigFileWithMacroDefination = this.FilePath;
            }

            return ret.Values.ToArray();
        }

        [OnSerializing]
        internal void OnSerializingMethod(StreamingContext context)
        {
            if (CustomTypeClogCSharpFileContents != null)
            {
                CustomTypeClogCSharpFileContents = CustomTypeClogCSharpFileContents.Replace("\r\n", "\n");
            }
        }

        private static CLogConfigurationFile FromLoadedFile(string fileName, string json)
        {
            JsonSerializerSettings s = new JsonSerializerSettings();
            s.Context = new StreamingContext(StreamingContextStates.Other, json);

            CLogConfigurationFile ret = JsonConvert.DeserializeObject<CLogConfigurationFile>(json, s);
            ret.FilePath = fileName;
            ret.ChainedConfigurations = new List<CLogConfigurationFile>();

            if (!string.IsNullOrEmpty(ret.CustomTypeClogCSharpFile))
            {
                if (ret.CustomTypeClogCSharpFile == EmbeddedDefaultName)
                {
                    ret.TypeEncoders.LoadDefaultCSharpFromEmbedded(ret);
                }
                else
                {
                    string cSharp = Path.GetDirectoryName(fileName);
                    cSharp = Path.Combine(cSharp, ret.CustomTypeClogCSharpFile);
                    ret.TypeEncoders.LoadCustomCSharp(cSharp, ret);
                }
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
                if (downstream == EmbeddedDefaultName)
                {
                    Assembly clogUtilsAssembly = typeof(CLogConfigurationFile).Assembly;
                    string assemblyName = clogUtilsAssembly.GetName().Name;
                    using (Stream embeddedStream = clogUtilsAssembly.GetManifestResourceStream($"{assemblyName}.defaults.clog_config"))
                    using (StreamReader reader = new StreamReader(embeddedStream))
                    {
                        string contents = reader.ReadToEnd();
                        var configFile = FromFileText(downstream, contents);
                        ret.ChainedConfigurations.Add(configFile);
                    }
                }
                else
                {
                    string root = Path.GetDirectoryName(fileName);
                    string toOpen = Path.Combine(root, downstream);

                    if (!File.Exists(toOpen))
                    {
                        Console.WriteLine($"Chained config file {toOpen} not found");
                        throw new CLogEnterReadOnlyModeException("ChainedConfigFileNotFound", CLogHandledException.ExceptionType.UnableToOpenChainedConfigFile, null);
                    }

                    var configFile = FromFile(toOpen);
                    ret.ChainedConfigurations.Add(configFile);
                }
            }

            ret.InUseTypeEncoders = new CLogTypeEncoder();

            RefreshTypeEncodersMarkBit(ret, ret.MarkPhase);

            return ret;
        }

        public static CLogConfigurationFile FromFileText(string fileName, string fileContents)
        {
            if (_loadedConfigFiles.Contains(fileName))
            {
                Console.WriteLine($"Circular config file detected {fileName}");
                throw new CLogEnterReadOnlyModeException("CircularConfigFilesNotAllowed", CLogHandledException.ExceptionType.CircularConfigFilesNotAllowed, null);
            }

            _loadedConfigFiles.Add(fileName);

            return FromLoadedFile(fileName, fileContents);
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

            return FromLoadedFile(fileName, json);
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

            foreach (var config in ret.ChainedConfigurations)
            {
                RefreshTypeEncodersMarkBit(config, markBit);
            }
        }

        private string ToJson(bool persistChainedFiles)
        {
            SerializeChainedConfigurations = persistChainedFiles;

            JsonSerializerSettings s = new JsonSerializerSettings();
            s.Formatting = Formatting.Indented;
            string me = JsonConvert.SerializeObject(this, Formatting.Indented);
            return me;
        }

        public void Lint()
        {
            this.MarkPhase = false;

            TypeEncoders.Lint();

            foreach (var child in this.ChainedConfigurations)
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

        public void ForceDecoderCompile()
        {
            if (TypeEncoders.CustomTypeDecoder != null)
            {
                this.TypeEncoders.ForceDecoderCompile(TypeEncoders.CustomTypeDecoder);
            }
            foreach (var config in ChainedConfigurations)
            {
                config.ForceDecoderCompile();
            }
        }

        public void Save(bool persistChainedFiles)
        {
            if (persistChainedFiles)
                this.CustomTypeClogCSharpFileContents = this.TypeEncoders.CustomCSharp;

            File.WriteAllText(this.FilePath, ToJson(persistChainedFiles));

            foreach (var child in this.ChainedConfigurations)
            {
                child.Save(persistChainedFiles);
            }
        }
    }
}
