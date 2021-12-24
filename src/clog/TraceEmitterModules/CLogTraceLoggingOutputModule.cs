/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    A stream manages the send and receive queues for application data. This file
    contains the initialization and cleanup functionality for the stream.

--*/

using System.Collections.Generic;
using System.Text;
using clogutils;
using clogutils.MacroDefinations;

namespace clog.TraceEmitterModules
{
    public class CLogTraceLoggingOutputModule : ICLogOutputModule
    {
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

        public void FinishedProcessing(CLogOutputInfo outputInfo, StringBuilder header, StringBuilder sourceFile)
        {
        }

        public void TraceLineDiscovered(string sourceFile, CLogOutputInfo outputInfo, CLogDecodedTraceLine decodedTraceLine, CLogSidecar sidecar, StringBuilder macroPrefix, StringBuilder inline,
            StringBuilder function)
        {
            int hashUInt;
            string hash;
            CLogExportModuleDefination moduleSettings = decodedTraceLine.GetMacroConfigurationProfile().FindExportModule(ModuleName);

            decodedTraceLine.macro.DecodeUniqueId(decodedTraceLine.match, decodedTraceLine.UniqueId, out hash, out hashUInt);

            inline.AppendLine(
                $"__annotation(L\"Debug\", L\"CLOG\", L\"{hash}\"); \\"); //, msg, id, \"{sourceFile}\");");

            string traceloggingLine = "TraceLoggingWrite(clog_hTrace, \"" + decodedTraceLine.UniqueId +
                                      "\"";

            foreach (var a in decodedTraceLine.splitArgs)
            {
                CLogFileProcessor.CLogVariableBundle arg = a;

                if (!arg.TypeNode.IsEncodableArg)
                    continue;

                CLogEncodingCLogTypeSearch node = decodedTraceLine.configFile.FindType(arg, decodedTraceLine);

                switch (node.EncodingType)
                {
                    case CLogEncodingType.Synthesized:
                        continue;

                    case CLogEncodingType.Skip:
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
                        traceloggingLine += ",\\\n    TraceLoggingString" + $"((const char *)({arg.MacroVariableName}),\"{arg.VariableInfo.SuggestedTelemetryName}\")";
                        break;

                    case CLogEncodingType.UNICODE_String:
                        traceloggingLine += ",\\\n    TraceLoggingWideString" + $"({arg.MacroVariableName},\"{arg.VariableInfo.SuggestedTelemetryName}\")";
                        break;
                }
            }

            // Emit keywords (if supplied by the user)
            if (moduleSettings.CustomSettings.ContainsKey("Keyword"))
                traceloggingLine += ",\\\n    TraceLoggingKeyword" + $"({moduleSettings.CustomSettings["Keyword"]})";

            if (moduleSettings.CustomSettings.ContainsKey("Level"))
                traceloggingLine += ",\\\n    TraceLoggingLevel" + $"({moduleSettings.CustomSettings["Level"]})";

            traceloggingLine += "); \\";
            inline.AppendLine(traceloggingLine);
        }
    }
}
