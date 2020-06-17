/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    Helpers for printing to the console (in color)

--*/

using clogutils.ConfigFile;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace clogutils
{
    public partial class CLogConsoleTrace
    {
        public enum TraceType
        {
            Std = 1,
            Err = 2,
            Wrn = 3,
            Tip = 4
        }

        public static void Trace(TraceType type, string msg)
        {
            ConsoleColor old = Console.ForegroundColor;

            switch (type)
            {
                case TraceType.Err:
                    Console.ForegroundColor = ConsoleColor.Red;
                    break;

                case TraceType.Wrn:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    break;

                case TraceType.Tip:
                    Console.ForegroundColor = ConsoleColor.Blue;
                    break;
            }

            Console.Write(msg);
            Console.ForegroundColor = old;
        }

        public static void TraceLine(TraceType type, string msg)
        {
            Trace(type, msg + Environment.NewLine);
        }

        public static string GetFileLine(CLogLineMatch TraceLine)
        {
            if (null != TraceLine && null != TraceLine.SourceFile)
            {
                int line = 1;
                int lastLine = 1;
                string file = System.IO.File.ReadAllText(TraceLine.SourceFile).Substring(0, TraceLine.MatchedRegEx.Index);
                for (int i = 0; i < file.Length; ++i)
                {
                    if (file[i] == '\n')
                    {
                        string prev = file.Substring(lastLine, i - 1 - lastLine);
                        //Console.WriteLine("Line: " + line + " " + prev);
                        lastLine = i + 1;
                        ++line;
                    }
                }

                string fullPath = System.IO.Path.GetFullPath(TraceLine.SourceFile);
                return $"{fullPath}({line},1)";
            }
            else
            {
                return "";
            }
        }

        public static void DecodeAndTraceToConsole(StreamWriter outputfile, CLogDecodedTraceLine bundle, string errorLine, CLogConfigurationFile config, Dictionary<string, IClogEventArg> valueBag, EventInformation eventInfo, bool showTimeStamp, bool showCPUInfo)
        {
            try
            {
                if (null == bundle)
                {
                    Console.WriteLine($"Invalid TraceLine : {errorLine}");
                    return;
                }

                StringBuilder toPrint = new StringBuilder();

                if (null != eventInfo)
                {
                    if (showTimeStamp)
                    {
                        toPrint.Append("[" + eventInfo.Timestamp.ToString("hh:mm:ss.ffffff") + "]");
                    }

                    if (showCPUInfo)
                    {
                        toPrint.Append("[");

                        bool havePid = false, haveTID = false;

                        if (!String.IsNullOrEmpty(eventInfo.ProcessId))
                        {
                            toPrint.Append(eventInfo.ProcessId);
                            havePid = true;
                        }

                        if (!String.IsNullOrEmpty(eventInfo.ThreadId))
                        {
                            if (havePid)
                                toPrint.Append(",");

                            toPrint.Append(eventInfo.ThreadId);
                            haveTID = true;
                        }

                        if (!String.IsNullOrEmpty(eventInfo.CPUId))
                        {
                            if (havePid)
                                toPrint.Append(",");

                            toPrint.Append(eventInfo.CPUId);
                        }

                        toPrint.Append("]");
                    }
                }

                string clean;

                CLogFileProcessor.CLogTypeContainer[] types = CLogFileProcessor.BuildTypes(config, null, bundle.TraceString, null, out clean);

                if (0 == types.Length)
                {
                    toPrint.Append(bundle.TraceString);
                    goto toPrint;
                }

                CLogFileProcessor.CLogTypeContainer first = types[0];


                if (valueBag.Count > 0)
                {
                    int argIndex = 0;

                    foreach (CLogFileProcessor.CLogTypeContainer type in types)
                    {
                        var arg = bundle.splitArgs[argIndex];

                        if (0 != arg.DefinationEncoding.CompareTo(type.TypeNode.DefinationEncoding))
                        {
                            Console.WriteLine("Invalid Types in Traceline");
                            throw new Exception("InvalidType : " + arg.DefinationEncoding);
                        }


                        CLogEncodingCLogTypeSearch payload = type.TypeNode;

                        if (!valueBag.TryGetValue(arg.VariableInfo.SuggestedTelemetryName, out IClogEventArg value))
                        {
                            toPrint.Append($"<SKIPPED:BUG:MISSINGARG:{arg.VariableInfo.SuggestedTelemetryName}:{payload.EncodingType}>");
                        }
                        else
                        {
                            if (string.IsNullOrEmpty(type.TypeNode.CustomDecoder))
                            {
                                toPrint.Append($"{type.LeadingString}{value.AsString}");
                            }
                            else
                            {
                                string decodedValue;
                                config.DecodeUsingCustomDecoder(type.TypeNode, value, bundle.match, out decodedValue);
                                toPrint.Append($"{type.LeadingString}{decodedValue}");
                            }
                        }


                        first = type;
                        ++argIndex;
                    }

                    string tail = bundle.TraceString.Substring(types[types.Length - 1].ArgStartingIndex + types[types.Length - 1].ArgLength);
                    toPrint.Append(tail);
                }
                else
                {
                    toPrint.Clear();
                    toPrint.Append(bundle.TraceString);
                }

            toPrint:
                if (null == outputfile)
                    Console.WriteLine(toPrint);
                else
                    outputfile.WriteLine(toPrint);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Invalid TraceLine : {errorLine} " + e);
            }
        }

        public class EventInformation
        {
            public System.DateTimeOffset Timestamp { get; set; }
            public string CPUId { get; set; }
            public string ThreadId { get; set; }
            public string ProcessId { get; set; }
        }

        public interface IClogEventArg
        {
            string AsString { get; }

            int AsInt32 { get; }

            uint AsUInt32 { get; }

            ulong AsPointer { get; }

            byte[] AsBinary { get; }
        }
    }
}
