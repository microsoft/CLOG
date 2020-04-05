/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    Build heirarchy of encoding strings and types,  for example %foo vs %f  

--*/

using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using clog2text_lttng;
using clogutils.MacroDefinations;
using Newtonsoft.Json;
using static clogutils.CLogConsoleTrace;

namespace clogutils.ConfigFile
{
    [JsonObject(MemberSerialization.OptIn)]
    public class CLogTypeEncoder
    {
        private CLogTypeSearchNode _parent = new CLogTypeSearchNode();

        private CLogCustomTraceEmittorFactory _traceEmittorX;

        [JsonProperty] public int Version { get; set; }

        [JsonProperty] public List<CLogEncodingCLogTypeSearch> TypeEncoder { get; set; } = new List<CLogEncodingCLogTypeSearch>();    

        public List<CLogEncodingCLogTypeSearch> _savedTypesX { get; set; } = new List<CLogEncodingCLogTypeSearch>();

        public string CustomTypeDecoder
        {
            get { return _traceEmittorX.CustomTypeDecoder; }
        }

        public string ToJson()
        {
            string me = JsonConvert.SerializeObject(this);
            return me;
        }

        public void Lint()
        {
            List<CLogEncodingCLogTypeSearch> newEncoders = new List<CLogEncodingCLogTypeSearch>();
            foreach(var encoder in TypeEncoder)
            {
                if(encoder.UsedBySourceFile.Count > 0)
                {
                    newEncoders.Add(encoder);
                    encoder.MarkPhase = false;
                }
            }
            TypeEncoder = newEncoders;
        }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            Init(TypeEncoder);
        }

        public void Merge(CLogTypeEncoder x)
        {
            foreach (var type in x._savedTypesX)
            {
                var v = TypeEncoder.FirstOrDefault(yx => yx.DefinationEncoding.Equals(type.DefinationEncoding));

                if (null != v)
                {
                    continue;
                }

                AddType(type);
            }
        }

        public void InitCustomDecoder(string code)
        {
            _traceEmittorX.SetSourceCode(code);
        }

        public string DecodeUsingCustomDecoder(CLogEncodingCLogTypeSearch node, IClogEventArg value)
        {
            return _traceEmittorX.Decode(node, value);
        }

        public void Init(IEnumerable<CLogEncodingCLogTypeSearch> savedTypes)
        {
            _traceEmittorX = new CLogCustomTraceEmittorFactory();

            foreach (var e in savedTypes.ToArray())
            {
                AddType(e);

                if (string.IsNullOrEmpty(e.CustomDecoder))
                {
                }
            }
        }

        private void AddType(CLogTypeSearchNode searchNode, CLogTypeSearchNode parentNode, CLogEncodingCLogTypeSearch fileNode, ref bool isNew, int index = 0)
        {
            if (index == fileNode.DefinationEncoding.Length)
            {
                searchNode.UserNode = fileNode;
                return;
            }

            CLogTypeSearchNode n;

            if (!searchNode.Nodes.TryGetValue(fileNode.DefinationEncoding[index], out n))
            {
                searchNode.Nodes[fileNode.DefinationEncoding[index]] = n = new CLogTypeSearchNode();
                isNew = true;
            }

            AddType(n, searchNode, fileNode, ref isNew, index + 1);
        }
        public void AddType(CLogEncodingCLogTypeSearch cLogTypeSearch)
        {
            bool isNew = false;
            AddType(_parent, null, cLogTypeSearch, ref isNew);

            if (isNew)
            {
                if (!TypeEncoder.Contains(cLogTypeSearch))
                {
                    TypeEncoder.Add(cLogTypeSearch);
                }

                if (!_savedTypesX.Contains(cLogTypeSearch))
                {
                    _savedTypesX.Add(cLogTypeSearch);
                }
            }
        }

        public CLogEncodingCLogTypeSearch FindTypeAndAdvance(string encoded, CLogLineMatch traceLineMatch, ref int index)
        {
            CLogTypeSearchNode start = _parent;
            string type = "";

            CLogTypeSearchNode prev = null;
            int? prevIdx = null;

            for (;;)
            {
                type += encoded[index];

                if (!start.Nodes.TryGetValue(encoded[index], out start))
                {
                    if (null != prev && null != prev.UserNode)
                    {
                        if(null != traceLineMatch)
                            prev.UserNode.UsedBySourceFile.Add(traceLineMatch.SourceFile);

                        index = prevIdx.Value;
                        return prev.UserNode;
                    }

                    throw new CLogTypeNotFoundException("InvalidType:" + type, type, traceLineMatch, true);
                }

                if (index == encoded.Length - 1)
                {
                    if (null != traceLineMatch)
                        start.UserNode.UsedBySourceFile.Add(traceLineMatch.SourceFile);

                    return start.UserNode;
                }

                prev = start;
                prevIdx = index;
                ++index;
            }
        }

        public void LoadCustomCSharp(string customTypeClogCSharpFile, CLogConfigurationFile configFile)
        {
            if (!File.Exists(customTypeClogCSharpFile))
            {
                CLogConsoleTrace.TraceLine(TraceType.Err, $"Custom C# file for custom decoder is missing.  Please create the file, or remove its reference from the config file");
                CLogConsoleTrace.TraceLine(TraceType.Err, $"                Missing File: {customTypeClogCSharpFile}");
                CLogConsoleTrace.TraceLine(TraceType.Err, $"      Defined In Config File: {configFile.FilePath}");
                throw new CLogEnterReadOnlyModeException("CustomCSharpFileMissing", CLogHandledException.ExceptionType.UnableToOpenCustomDecoder, null);
            }

            string sourceCode = File.ReadAllText(customTypeClogCSharpFile);
            string sourceHash = Path.GetFileName(configFile.FilePath);

            _traceEmittorX.SetSourceCode(sourceCode);
        }
    }
}
