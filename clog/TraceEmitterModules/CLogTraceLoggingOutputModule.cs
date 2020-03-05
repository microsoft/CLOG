﻿/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    A stream manages the send and receive queues for application data. This file
    contains the initialization and cleanup functionality for the stream.

--*/

using System.Collections.Generic;
using System.Text;
using clogutils;

namespace clog.TraceEmitterModules
{
    public class CLogTraceLoggingOutputModule : ICLogOutputModule
    {
        private readonly HashSet<string> knownHashes = new HashSet<string>();

        public string ModuleName
        {
            get { return "TRACELOGGING"; }
        }

        public bool ManditoryModule
        {
            get { return false; }
        }

        public void InitHeader(StringBuilder header)
        {
        }

        public void FinishedProcessing(StringBuilder header, StringBuilder sourceFile)
        {
        }

        public void TraceLineDiscovered(string sourceFile, CLogDecodedTraceLine decodedTraceLine, CLogSidecar sidecar, StringBuilder macroPrefix, StringBuilder inline,
            StringBuilder function)
        {
            int hashUInt;
            string hash;


            decodedTraceLine.macro.DecodeUniqueId(decodedTraceLine.match, decodedTraceLine.UniqueId, out hash, out hashUInt);


            if (knownHashes.Contains(hash))
            {
                return;
            }

            knownHashes.Add(hash);

            inline.AppendLine(
                $"__annotation(L\"Debug\", L\"CLOG\", L\"{hash}\"); \\"); //, msg, id, \"{sourceFile}\");");

            string traceloggingLine = "TraceLoggingWrite(clog_hTrace, \"" + decodedTraceLine.UniqueId +
                                      "\"";


            foreach (var a in decodedTraceLine.splitArgs)
            {
                CLogFileProcessor.CLogVariableBundle arg = a;
                CLogEncodingCLogTypeSearch node = decodedTraceLine.configFile.FindType(arg);

                switch (node.EncodingType)
                {
                    case CLogEncodingType.Synthesized:
                        continue;

                    case CLogEncodingType.Skip:
                        continue;


                    case CLogEncodingType.ANSI_String:
                        continue;

                    case CLogEncodingType.UNICODE_String:
                        continue;
                }

                //
                // Documentation for each of the TraceLogging macros
                // https://docs.microsoft.com/en-gb/windows/win32/tracelogging/tracelogging-wrapper-macros
                //
                switch (node.EncodingType)
                {
                    case CLogEncodingType.Int8:
                        traceloggingLine += ",\\\n    TraceLoggingInt8" + $"({arg.MacroVariableName},\"{arg.VariableInfo.SuggestedTelemetryName}\")";
                        break;

                    case CLogEncodingType.UInt8:
                        traceloggingLine += ",\\\n    TraceLoggingUInt8" + $"({arg.MacroVariableName},\"{arg.VariableInfo.SuggestedTelemetryName}\")";
                        break;

                    case CLogEncodingType.Int16:
                        traceloggingLine += ",\\\n    TraceLoggingInt16" + $"({arg.MacroVariableName},\"{arg.VariableInfo.SuggestedTelemetryName}\")";
                        break;

                    case CLogEncodingType.UInt16:
                        traceloggingLine += ",\\\n    TraceLoggingUInt16" + $"({arg.MacroVariableName},\"{arg.VariableInfo.SuggestedTelemetryName}\")";
                        break;

                    case CLogEncodingType.Int32:
                        traceloggingLine += ",\\\n    TraceLoggingInt32" + $"({arg.MacroVariableName},\"{arg.VariableInfo.SuggestedTelemetryName}\")";
                        break;

                    case CLogEncodingType.UInt32:
                        traceloggingLine += ",\\\n    TraceLoggingUInt32" + $"({arg.MacroVariableName},\"{arg.VariableInfo.SuggestedTelemetryName}\")";
                        break;

                    case CLogEncodingType.Int64:
                        traceloggingLine += ",\\\n    TraceLoggingInt64" + $"({arg.MacroVariableName},\"{arg.VariableInfo.SuggestedTelemetryName}\")";
                        break;

                    case CLogEncodingType.UInt64:
                        traceloggingLine += ",\\\n    TraceLoggingUInt64" + $"({arg.MacroVariableName},\"{arg.VariableInfo.SuggestedTelemetryName}\")";
                        break;

                    case CLogEncodingType.Pointer:
                        traceloggingLine += ",\\\n    TraceLoggingPointer" + $"({arg.MacroVariableName},\"{arg.VariableInfo.SuggestedTelemetryName}\")";
                        break;

                    case CLogEncodingType.ByteArray:
                        traceloggingLine += ",\\\n    TraceLoggingUInt8Array" + $"({arg.MacroVariableName}, {arg.MacroVariableName}_len, \"{arg.VariableInfo.SuggestedTelemetryName}\")";
                        break;

                    case CLogEncodingType.ANSI_String:
                        traceloggingLine += ",\\\n    TraceLoggingAnsiString" + $"({arg.MacroVariableName},\"{arg.VariableInfo.SuggestedTelemetryName}\")";
                        break;
                }
            }

            traceloggingLine += "); \\";
            inline.AppendLine(traceloggingLine);
        }
    }
}