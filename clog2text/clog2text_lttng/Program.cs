/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    Reads from LTTNG (via babletrace output) and converts into CLOG property bag.  This bag is then sent into a generic CLOG function for output to STDOUT

    LTTTNG -> babletrace -> clog2text_lttng -> generic clog

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
        private static Dictionary<string, IClogEventArg> SplitBabelTraceLine(string traceLine)
        {
            int bracketCount = 0;
            Dictionary<string, IClogEventArg> ret = new Dictionary<string, IClogEventArg>();
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
                int lines = 0;
                StreamWriter outputfile = null;
                if (!String.IsNullOrEmpty(options.OutputFile))
                    outputfile = new StreamWriter(new FileStream(options.OutputFile, FileMode.Create));

                DateTimeOffset startTime = DateTimeOffset.Now;
                while(!string.IsNullOrEmpty(line = file.ReadLine()))
                {
                    ++lines;
                    if(0 == lines % 10000)
                    {
                        Console.WriteLine($"Line : {lines}");
                    }
                    Dictionary<string, IClogEventArg> valueBag;
                    CLogDecodedTraceLine bundle = lttngDecoder.DecodedTraceLine(line, out valueBag);
                    DecodeAndTraceToConsole(outputfile, bundle, line, config, valueBag);
                }

                outputfile.Flush();
                outputfile.Close();

                Console.WriteLine($"Decoded {lines} in {DateTimeOffset.Now - startTime}");
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

            public CLogDecodedTraceLine DecodedTraceLine(string babbleTraceLine, out Dictionary<string, IClogEventArg> args)
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
                    args = new Dictionary<string, IClogEventArg>();
                }
                else
                {
                    args = SplitBabelTraceLine(fields);
                }

                return bundle;
            }
        }

        public class LTTNGClogEvent : IClogEventArg
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
                    //CLogConsoleTrace.TraceLine(TraceType.Err, "Binary Encoding Not Yet Supported with LTTNG");
                    return new byte[0];
                    //throw new NotImplementedException("Binary Encoding Not Supported");
                }
            }
        }
    }
}
