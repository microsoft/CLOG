/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    A stream manages the send and receive queues for application data. This file
    contains the initialization and cleanup functionality for the stream.

--*/

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using clog2text_lttng;
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

        public void InitCustomDecoder(string nameSpace, string code)
        {
            _traceEmittorX.CompileSource(nameSpace, code);
        }

        public string DecodeUsingCustomDecoder(CLogEncodingCLogTypeSearch node, IClogEvent value)
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


        public CLogEncodingCLogTypeSearch FindTypeAndAdvance(string encoded, Match traceLineMatch, ref int index)
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
                        index = prevIdx.Value;
                        return prev.UserNode;
                    }

                    throw new CLogTypeNotFoundException("InvalidType:" + type, type, traceLineMatch, true);
                }

                if (index == encoded.Length - 1)
                {
                    return start.UserNode;
                }

                prev = start;
                prevIdx = index;
                ++index;
            }
        }

        public void LoadCustomCSharp(string customTypeClogCSharpFile, CLogConfigurationFile configFile)
        {
            string sourceCode = File.ReadAllText(customTypeClogCSharpFile);
            string sourceHash = Path.GetFileName(configFile.FilePath);

            _traceEmittorX.CompileSource(sourceHash, sourceCode);
        }
    }
}
