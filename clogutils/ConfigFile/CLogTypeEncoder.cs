﻿/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    Build heirarchy of encoding strings and types,  for example %foo vs %f  

--*/

using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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

        public IEnumerable<CLogEncodingCLogTypeSearch> FlattendTypeEncoder
        {
            get
            {
                return _parent.Flatten();
            }
        }
        /*
        {
            get
            {
              //  List<CLogEncodingCLogTypeSearch> ret = new List<CLogEncodingCLogTypeSearch>();

                return _parent.Flatten();

               // return ret;
            }
        }*/

        //  public List<CLogEncodingCLogTypeSearch> hotTypes { get; set; } = new List<CLogEncodingCLogTypeSearch>();



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
            foreach(var encoder in FlattendTypeEncoder)
            {
                if(encoder.UsedBySourceFile.Count > 0)
                {
                    newEncoders.Add(encoder);
                    encoder.MarkPhase = false;
                }
            }

            Init(newEncoders);
            //TypeEncoder = newEncoders;
        }
        
        [OnSerializing]
        private void OnSerialized(StreamingContext context)
        {
            TypeEncoder = new List<CLogEncodingCLogTypeSearch>(FlattendTypeEncoder);
        }
               
        [OnSerialized()]
        internal void OnSerializedMethod(StreamingContext context)
        {
            TypeEncoder = null;
        }


        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            Init(TypeEncoder);
            TypeEncoder = null;
        }
/*
        public void MergeHotTypes(CLogTypeEncoder x)
        {
            foreach (var type in x.hotTypes)
            {
                var v = TypeEncoder.FirstOrDefault(yx => yx.DefinationEncoding.Equals(type.DefinationEncoding));

                if(type.Hash != v.Hash)
                {
                    Debugger.Break();
                }
              
                if (null != v)
                {
                    continue;
                }

                AddType(type);
            }
        }*/

        public void InitCustomDecoder(string code)
        {
            _traceEmittorX.SetSourceCode(code);
        }

        public string DecodeUsingCustomDecoder(CLogEncodingCLogTypeSearch node, IClogEventArg value, CLogLineMatch traceLine)
        {
            return _traceEmittorX.Decode(node, value, traceLine);
        }

        public void Init(IEnumerable<CLogEncodingCLogTypeSearch> savedTypes)
        {
            _traceEmittorX = new CLogCustomTraceEmittorFactory();
            _parent = new CLogTypeSearchNode();

            foreach (var e in savedTypes.ToArray())
            {
                AddType(e);
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
#if false
            if (isNew)
            {
                if (!TypeEncoder.Contains(cLogTypeSearch))
                {
                    TypeEncoder.Add(cLogTypeSearch);
                }
      /*          
                if (!hotTypes.Contains(cLogTypeSearch))
                {
                    hotTypes.Add(cLogTypeSearch);
                }*/
            }
#endif
        }

        public CLogEncodingCLogTypeSearch FindTypeX(CLogFileProcessor.CLogVariableBundle bundle)
        {
            return FindTypeX(bundle.DefinationEncoding);
        }

        public CLogEncodingCLogTypeSearch FindTypeX(string encoded)
        {
            int idx = 0;
            return FindTypeAndAdvance(encoded, null,ref idx);
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

                    return null;
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

        public string CustomCSharp
        {
            get
            {
                return _traceEmittorX.CustomTypeDecoder;
            }
        }

        public void SetSourceCode(string sourceCode)
        {         
            _traceEmittorX.SetSourceCode(sourceCode);
        }
    }
}
