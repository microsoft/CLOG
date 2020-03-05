﻿/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    A stream manages the send and receive queues for application data. This file
    contains the initialization and cleanup functionality for the stream.

--*/

using System;
using System.Collections.Generic;
using System.Text;
using clogutils.ConfigFile;

namespace clogutils
{
    public class CLogConsoleTrace
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

        public static void DecodeAndTraceToConsole(CLogDecodedTraceLine bundle, string errorLine, CLogConfigurationFile config, Dictionary<string, IClogEvent> valueBag)
        {
            try
            {
                if (null == bundle)
                {
                    Console.WriteLine($"Invalid TraceLine : {errorLine}");
                    return;
                }

                StringBuilder toPrint = new StringBuilder();

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

                        if (!valueBag.TryGetValue(arg.VariableInfo.SuggestedTelemetryName, out IClogEvent value))
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
                                toPrint.Append($"{type.LeadingString}{config.TypeEncoders.DecodeUsingCustomDecoder(type.TypeNode, value)}");
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
                Console.WriteLine(toPrint);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Invalid TraceLine : {errorLine} " + e);
            }
        }

        public interface IClogEvent
        {
            string AsString { get; }

            int AsInt32 { get; }

            uint AsUInt32 { get; }

            byte[] AsBinary { get; }
        }
    }
}