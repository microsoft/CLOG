/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    A stream manages the send and receive queues for application data. This file
    contains the initialization and cleanup functionality for the stream.

--*/

using System;
using System.Collections.Generic;
using System.IO;
using clogutils;
using clogutils.ConfigFile;
using CommandLine;
using static clogutils.CLogConsoleTrace;

namespace clog2text_lttng
{
    internal class Program
    {
        private static Dictionary<string, IClogEvent> SplitBabelTraceLine(string traceLine)
        {
            int bracketCount = 0;
            Dictionary<string, IClogEvent> ret = new Dictionary<string, IClogEvent>();
            string piece = "";
            int lastEqual = -1;
            int startIndex = 0;

            for(int i = 0;
                    i < traceLine.Length + 1;
                    ++i) //<-- go one beyond the array, we catch this in the if block below
            {
                if((i >= traceLine.Length || traceLine[i] == ',') && 0 == bracketCount)
                {
                    string key = traceLine.Substring(startIndex, lastEqual - startIndex).Trim();
                    string value = traceLine.Substring(lastEqual + 1, i - lastEqual - 1).Trim();
                    ret[key] = new LTTNGClogEvent(value);
                    piece = "";
                    startIndex = i + 1;
                    lastEqual = -1;
                    continue;
                }

                if(traceLine[i] == '{' || i >= 1 && traceLine[i] == '"' && traceLine[i - 1] == '/')
                {
                    ++bracketCount;
                }
                else if(bracketCount >= 1 && (traceLine[i] == '}' || i >= 1 && traceLine[i] == '"' && traceLine[i - 1] == '/'))
                {
                    --bracketCount;
                }
                else if(traceLine[i] == '=' && 0 == bracketCount)
                {
                    lastEqual = i;
                }

                piece += traceLine[i];
            }

            return ret;
        }

        private static int Main(string[] args)
        {
            ParserResult<CommandLineArguments> o = Parser.Default.ParseArguments<CommandLineArguments>(args);

            return o.MapResult(
                       options =>
            {
                string sidecarJson = File.ReadAllText(options.SideCarFile);
                CLogSidecar textManifest = CLogSidecar.FromJson(sidecarJson);
                CLogConfigurationFile config = new CLogConfigurationFile();
                config.TypeEncoders = textManifest.TypeEncoder;


                TextReader file = Console.In;

                if(!string.IsNullOrEmpty(options.BabelTrace))
                {
                    file = new StreamReader(options.BabelTrace);
                }


                string line;
                LTTNGEventDecoder lttngDecoder = new LTTNGEventDecoder(textManifest);

                while(!string.IsNullOrEmpty(line = file.ReadLine()))
                {
                    Dictionary<string, IClogEvent> valueBag;
                    CLogDecodedTraceLine bundle = lttngDecoder.DecodedTraceLine(line, out valueBag);
                    DecodeAndTraceToConsole(bundle, line, config, valueBag);
                }

                return 0;
            }, err =>
            {
                Console.WriteLine("Bad Args : " + err);
                return -1;
            });
        }

        public interface ICLogEventDecoder
        {
            public string GetValue(string value);
        }

        public class LTTNGEventDecoder
        {
            private readonly CLogSidecar _sidecar;

            public LTTNGEventDecoder(CLogSidecar sidecar)
            {
                _sidecar = sidecar;
            }

            public CLogDecodedTraceLine DecodedTraceLine(string babbleTraceLine, out Dictionary<string, IClogEvent> args)
            {
                args = SplitBabelTraceLine(babbleTraceLine);

                if(!args.ContainsKey("name") || !args.ContainsKey("event.fields"))
                {
                    Console.WriteLine("TraceHasNoArgs");
                }

                CLogDecodedTraceLine bundle = _sidecar.FindBundle(args["name"].AsString);

                string fields = args["event.fields"].AsString.Substring(1, args["event.fields"].AsString.Length - 2).Trim();

                if(0 == fields.Length)
                {
                    args = new Dictionary<string, IClogEvent>();
                }
                else
                {
                    args = SplitBabelTraceLine(fields);
                }

                return bundle;
            }
        }

        public class LTTNGClogEvent : IClogEvent
        {
            public LTTNGClogEvent(string value)
            {
                AsString = value;
            }

            public string AsString
            {
                get;
            }

            public int AsInt32
            {
                get
                {
                    return Convert.ToInt32(AsString);
                }
            }

            public uint AsUInt32
            {
                get
                {
                    return Convert.ToUInt32(AsString);
                }
            }

            public byte[] AsBinary
            {
                get
                {
                    throw new NotImplementedException("Binary Encoding Not Supported");
                }
            }
        }
    }
}
