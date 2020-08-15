﻿/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    Reads from LTTNG (via babletrace output) and converts into CLOG property bag.  This bag is then sent into a generic CLOG function for output to STDOUT

    LTTTNG -> babletrace -> clog2text_lttng -> generic clog

--*/

using clogutils;
using CommandLine;
using System;
using System.Collections.Generic;
using System.IO;
using static clogutils.CLogConsoleTrace;

namespace clog2text_lttng
{
    internal class Program
    {
        private static Dictionary<string, IClogEventArg> SplitBabelTraceLine(string traceLine)
        {
            int bracketCount = 0;
            int arrayCount = 0;
            int quotes = 0;
            Dictionary<string, IClogEventArg> ret = new Dictionary<string, IClogEventArg>();
            string piece = "";
            int lastEqual = -1;
            int startIndex = 0;

            for (int i = 0;
                    i < traceLine.Length + 1;
                    ++i) //<-- go one beyond the array, we catch this in the if block below
            {
                if ((i >= traceLine.Length || traceLine[i] == ',') && 0 == bracketCount && 0 == arrayCount && 0 == quotes)
                {
                    string key = traceLine.Substring(startIndex, lastEqual - startIndex).Trim();
                    string value = traceLine.Substring(lastEqual + 1, i - lastEqual - 1).Trim();
                    ret[key] = new LTTNGClogEvent(value);
                    piece = "";
                    startIndex = i + 1;
                    lastEqual = -1;
                    continue;
                }

                if (traceLine[i] == '{')
                {
                    ++bracketCount;
                }
                else if (bracketCount >= 1 && traceLine[i] == '}')
                {
                    --bracketCount;
                }
                else if (traceLine[i] == '[')
                {
                    ++arrayCount;
                }
                else if (arrayCount >= 1 && traceLine[i] == ']')
                {
                    --arrayCount;
                }
                else if (traceLine[i] == '=' && 0 == bracketCount && 0 == arrayCount)
                {
                    lastEqual = i;
                }
                else if (traceLine[i] == '\"' && (i != 0 && traceLine[i - 1] != '\\'))
                {
                    if (0 == quotes)
                        ++quotes;
                    else if (1 == quotes)
                        quotes--;
                    else
                        throw new ArgumentException("Escaped Quotes Off");
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

                TextReader file = Console.In;

                if (!string.IsNullOrEmpty(options.BabelTrace))
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

                try
                {
                    while (!string.IsNullOrEmpty(line = file.ReadLine()))
                    {
                        ++lines;
                        if (0 == lines % 10000)
                        {
                            Console.WriteLine($"Line : {lines}");
                        }
                        Dictionary<string, IClogEventArg> valueBag;
                        EventInformation ei;
                        CLogDecodedTraceLine bundle = lttngDecoder.DecodedTraceLine(line, out ei, out valueBag);
                        DecodeAndTraceToConsole(outputfile, bundle, line, textManifest.ConfigFile, valueBag, ei, options.ShowTimestamps, options.ShowCPUInfo);
                    }
                }
                catch (Exception e)
                {
                    CLogConsoleTrace.TraceLine(TraceType.Err, "ERROR : " + e);
                    if (null != outputfile)
                    {
                        outputfile.WriteLine("ERROR : " + e);
                    }
                }
                finally
                {
                    if (null != outputfile)
                    {
                        outputfile.Flush();
                        outputfile.Close();
                    }
                }
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

            public CLogDecodedTraceLine DecodedTraceLine(string babbleTraceLine, out EventInformation eventInfo, out Dictionary<string, IClogEventArg> args)
            {
                args = SplitBabelTraceLine(babbleTraceLine);

                EventInformation ei = eventInfo = new EventInformation();

                if (!args.ContainsKey("name") || !args.ContainsKey("event.fields"))
                {
                    Console.WriteLine("TraceHasNoArgs");
                }

                if (args.ContainsKey("timestamp"))
                {
                    string[] bits = args["timestamp"].AsString.Split(':');
                    ei.Timestamp = new DateTimeOffset(Convert.ToDateTime(args["timestamp"].AsString));
                }

                if (args.ContainsKey("stream.packet.context"))
                {
                    string packetContext = args["stream.packet.context"].AsString;
                    string packetFields = packetContext.Substring(1, packetContext.Length - 2).Trim();
                    var cpuArgs = SplitBabelTraceLine(packetFields);
                    ei.CPUId = cpuArgs["cpu_id"].AsString;
                }

                if (args.ContainsKey("stream.event.context"))
                {
                    string packetContext = args["stream.event.context"].AsString;
                    string packetFields = packetContext.Substring(1, packetContext.Length - 2).Trim();
                    var cpuArgs = SplitBabelTraceLine(packetFields);
                    ei.ThreadId = Convert.ToInt64(cpuArgs["vtid"].AsString).ToString("x");
                    ei.ProcessId = Convert.ToInt64(cpuArgs["vpid"].AsString).ToString("x");
                }

                CLogDecodedTraceLine bundle = _sidecar.FindBundle(args["name"].AsString);

                string fields = args["event.fields"].AsString.Substring(1, args["event.fields"].AsString.Length - 2).Trim();

                if (0 == fields.Length)
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

            public ulong AsPointer
            {
                get
                {
                    if (AsString.StartsWith("0x"))
                    {
                        return Convert.ToUInt64(AsString, 16);
                    }
                    return Convert.ToUInt64(AsString);
                }
            }

            public byte[] AsBinary
            {
                get
                {
                    int firstOpen = AsString.IndexOf("[") + 1;
                    int lastClose = AsString.LastIndexOf("]") - 1;

                    string bits = AsString.Substring(firstOpen, AsString.Length - (AsString.Length - lastClose) - firstOpen);

                    if(String.IsNullOrEmpty(bits))
                    {
                        return new byte[0];
                    }
                    var splits = SplitBabelTraceLine(bits);

                    List<byte> ret = new List<byte>();
                    int idx = 0;
                    for (; ; )
                    {
                        IClogEventArg arg;
                        if (!splits.TryGetValue($"[{idx}]", out arg))
                            break;

                        ret.Add((byte)arg.AsInt32);
                        ++idx;
                    }

                    return ret.ToArray();
                }
            }
        }
    }
}
