/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    A stream manages the send and receive queues for application data. This file
    contains the initialization and cleanup functionality for the stream.

--*/

using clogutils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using clogutils.MacroDefinations;

namespace clog.TraceEmitterModules
{
    public class CLogDiagTrace : ICLogOutputModule
    {
        public string ModuleName
        {
            get
            {
                return "UDIAG";
            }
        }

        public bool ManditoryModule
        {
            get
            {
                return false;
            }
        }

        public void InitHeader(StringBuilder header)
        {
            header.AppendLine($"// CLOG DiagTrace Experiment ------");

            header.AppendLine($"#ifndef CLOG_DIAGTRACE_BPF");
            header.AppendLine($"#include <diagtrace.h>");
            header.AppendLine($"#include <string.h>");
            header.AppendLine($"#endif");

            header.AppendLine($"#define DIAG_MIN(a,b) (((a)<(b))?(a):(b))");

            header.AppendLine($"#ifndef CLOG_DIAGTRACE_UNSET_BPF_TYPES");
            header.AppendLine($"#define DIAG_TRACE_8 char");
            header.AppendLine($"#define DIAG_TRACE_U8 unsigned char");

            header.AppendLine($"#define DIAG_TRACE_16 short");
            header.AppendLine($"#define DIAG_TRACE_U16 unsigned short");

            header.AppendLine($"#define DIAG_TRACE_32 int");
            header.AppendLine($"#define DIAG_TRACE_U32 unsigned int");

            header.AppendLine($"#define DIAG_TRACE_64 long long");
            header.AppendLine($"#define DIAG_TRACE_U64 unsigned long long");

            header.AppendLine($"#define DIAG_TRACE_POINTER void *");
            header.AppendLine($"#endif");
        }


        public void FinishedProcessing(CLogOutputInfo outputInfo, StringBuilder header, StringBuilder sourceFile)
        {
            string ebpfHeaderFileName = Path.Combine(outputInfo.OutputDirectory, "ebpfDecode.h");
            string ebpfHeader = "";
            string myHeader = "#include <" + Path.GetFileName(outputInfo.OutputFileName) + ">\n";

            if(!Directory.Exists(Path.GetDirectoryName(ebpfHeaderFileName)))
                Directory.CreateDirectory(Path.GetDirectoryName(ebpfHeaderFileName));

            if(File.Exists(ebpfHeaderFileName))
            {
                ebpfHeader = File.ReadAllText(ebpfHeaderFileName);
                File.Delete(ebpfHeaderFileName);
            }
            else
            {
                ebpfHeader = $"#define CLOG_DECODE_ALL(dataPtr, dataLen) \\\n".Replace(".","_");
            }

            if(!ebpfHeader.Contains(myHeader))
                ebpfHeader = myHeader + ebpfHeader;


            foreach(string s in decoderMacros)
            {
                if(!ebpfHeader.Contains(s))
                    ebpfHeader += s.ToString();
            }

            File.WriteAllText(ebpfHeaderFileName, ebpfHeader);
        }

        private HashSet<string> STRUCT_DEFS = new HashSet<string>();
        private List<string> decoderMacros = new List<string>();
        private StringBuilder printFunctions = new StringBuilder();

        public void TraceLineDiscovered(string sourceFile, CLogOutputInfo outputInfo, CLogDecodedTraceLine decodedTraceLine, CLogSidecar sidecar, StringBuilder headerFile, StringBuilder macroBody, StringBuilder outputSourceFile)
        {
            CLogExportModuleDefination moduleSettings = decodedTraceLine.GetMacroConfigurationProfile().FindExportModule(this.ModuleName);

            if (!moduleSettings.CustomSettings.ContainsKey("ProviderIndex"))
                throw new CLogEnterReadOnlyModeException("ProviderIndex missing from CLOG config file entry for DiagTrace", CLogHandledException.ExceptionType.RequiredConfigParameterUnspecified, decodedTraceLine.match);

            if (!moduleSettings.CustomSettings.ContainsKey("ProviderHandle"))
                throw new CLogEnterReadOnlyModeException("ProviderHandle missing from CLOG config file entry for DiagTrace", CLogHandledException.ExceptionType.RequiredConfigParameterUnspecified, decodedTraceLine.match);

            string providerIndex = moduleSettings.CustomSettings["ProviderIndex"];
            string providerHandle = moduleSettings.CustomSettings["ProviderHandle"];

            headerFile.AppendLine($"extern int " + providerIndex + ";");
            headerFile.AppendLine($"extern int " + providerHandle + ";");

            StringBuilder structDef = new StringBuilder();
            string structName = "CLOG_DIAGTRACE_" + decodedTraceLine.UniqueId.ToUpper();

            string traceString = decodedTraceLine.TraceString;
            while(traceString.Contains("%!"))
            {
                traceString = traceString.Replace("%!", "<CLOG_BUG>[0x%p]!");
            }

            string existingId = decodedTraceLine.GetConfigFileProperty(ModuleName, "EventID");
            string durableId = decodedTraceLine.UniqueId;//decodedTraceLine.UniqueId.ToString();

           //durableId = decodedTraceLine.UniqueId.GetHashCode().ToString();
           //if(!durableId.Equals(existingId))
           //    throw new CLogEnterReadOnlyModeException("DurableID hash algorithm failed", CLogHandledException.ExceptionType.DuplicateId, decodedTraceLine.match);

            decodedTraceLine.AddConfigFileProperty(ModuleName, "EventID", durableId.ToString());

            structDef.AppendLine($"#define {structName}_ID \"{durableId}\"");
            structDef.AppendLine($"#define {structName}_TEXT \"{decodedTraceLine.DecomposedString.AsPrintF}\"");

            string msg = "";
            msg += $" \\\nDIAG_TYPE({structName}, {structName}_ID, {structName}_FN, dataPtr, dataLen)";

            decoderMacros.Add(msg);

            // NOTE: do not change this max size without also updating diagtrace.h
            int idMaxSize = 48;
            int currentOffset = 0;

            structDef.AppendLine("#pragma pack(1)");
            structDef.AppendLine("typedef struct " + structName);
            structDef.AppendLine("{");
            structDef.AppendLine($"    struct CLOG_UDIAG_EVENT eventHeader;");

            decodedTraceLine.AddConfigFileProperty(ModuleName, $"clog_ID", currentOffset.ToString());
            decodedTraceLine.AddConfigFileProperty(ModuleName, $"clog_ID_Len", idMaxSize.ToString());
            currentOffset += idMaxSize;

            macroBody.AppendLine("{\\");
            macroBody.AppendLine("    if (diagtrace_register_page[" + providerIndex + "])\\");
            macroBody.AppendLine("    {\\");
            macroBody.AppendLine("        " +structName + " myEvent = {0};\\");
            macroBody.AppendLine("        strncpy(myEvent.eventHeader.clog_ID, \"" +durableId + "\", "+(idMaxSize-1).ToString()+");\\");
            macroBody.AppendLine("        myEvent.eventHeader.clog_ID["+(idMaxSize-1) +"] = 0;\\");

             // Only define a struct once
            if(STRUCT_DEFS.Contains(decodedTraceLine.UniqueId.ToUpper()))
                return;
            STRUCT_DEFS.Add(decodedTraceLine.UniqueId.ToUpper());

            string args = "";
            bool hasAnyDefs = false;

            foreach (var a in decodedTraceLine.splitArgs)
            {
                CLogFileProcessor.CLogVariableBundle arg = a;

                if (!arg.TypeNode.IsEncodableArg)
                    continue;

                CLogEncodingCLogTypeSearch node = decodedTraceLine.configFile.FindType(arg, decodedTraceLine);

                switch (node.EncodingType)
                {
                    case CLogEncodingType.Synthesized:
                        structDef.AppendLine( $"    // Skipping Synthesized {arg.VariableInfo.SuggestedTelemetryName};");
                        continue;

                    case CLogEncodingType.Skip:
                        structDef.AppendLine( $"    // Skipping Skipped {arg.VariableInfo.SuggestedTelemetryName};");
                        continue;
                }

                switch (node.EncodingType)
                {
                    case CLogEncodingType.ByteArray:
                        //BUGBUG : Limit to 32.
                        structDef.AppendLine( $"    DIAG_TRACE_8 clog_{arg.VariableInfo.SuggestedTelemetryName}[32]; // BUGBUG Limited");
                        macroBody.AppendLine($"        if ({arg.MacroVariableName}_len > 0) " +"{ \\");
                        macroBody.AppendLine($"            CLOG_COPY(myEvent.clog_{arg.VariableInfo.SuggestedTelemetryName}, {arg.MacroVariableName}, DIAG_MIN(32, {arg.MacroVariableName}_len)); \\");
                        macroBody.AppendLine( "        } \\");
                        
                        decodedTraceLine.AddConfigFileProperty(ModuleName, $"{arg.VariableInfo.SuggestedTelemetryName}", currentOffset.ToString());
                        decodedTraceLine.AddConfigFileProperty(ModuleName, $"{arg.VariableInfo.SuggestedTelemetryName}_Len", "32");
                        currentOffset += 32;

                        hasAnyDefs = true;
                        break;

                    case CLogEncodingType.Int8:
                        structDef.AppendLine($"    DIAG_TRACE_8 clog_{arg.VariableInfo.SuggestedTelemetryName};");
                        macroBody.AppendLine("        myEvent.clog_" + arg.VariableInfo.SuggestedTelemetryName + " = (" + arg.MacroVariableName + "); \\");

                        decodedTraceLine.AddConfigFileProperty(ModuleName, $"{arg.VariableInfo.SuggestedTelemetryName}", currentOffset.ToString());
                        decodedTraceLine.AddConfigFileProperty(ModuleName, $"{arg.VariableInfo.SuggestedTelemetryName}_Len", "1");
                        currentOffset += 1;
                        hasAnyDefs = true;
                        break;

                    case CLogEncodingType.UInt8:
                        structDef.AppendLine($"    DIAG_TRACE_U8 clog_{arg.VariableInfo.SuggestedTelemetryName};");
                        macroBody.AppendLine("        myEvent.clog_" + arg.VariableInfo.SuggestedTelemetryName + " = (" + arg.MacroVariableName + "); \\");

                        decodedTraceLine.AddConfigFileProperty(ModuleName, $"{arg.VariableInfo.SuggestedTelemetryName}", currentOffset.ToString());
                        decodedTraceLine.AddConfigFileProperty(ModuleName, $"{arg.VariableInfo.SuggestedTelemetryName}_Len", "1");
                        currentOffset += 1;
                        hasAnyDefs = true;
                        break;

                    case CLogEncodingType.Int16:
                        structDef.AppendLine($"    DIAG_TRACE_16 clog_{arg.VariableInfo.SuggestedTelemetryName};");
                        macroBody.AppendLine("        myEvent.clog_" + arg.VariableInfo.SuggestedTelemetryName + " = (" + arg.MacroVariableName + "); \\");

                        decodedTraceLine.AddConfigFileProperty(ModuleName, $"{arg.VariableInfo.SuggestedTelemetryName}", currentOffset.ToString());
                        decodedTraceLine.AddConfigFileProperty(ModuleName, $"{arg.VariableInfo.SuggestedTelemetryName}_Len", "2");
                        currentOffset += 2;
                        hasAnyDefs = true;
                        break;

                    case CLogEncodingType.UInt16:
                        structDef.AppendLine($"    DIAG_TRACE_U16 clog_{arg.VariableInfo.SuggestedTelemetryName};");
                        macroBody.AppendLine("        myEvent.clog_" + arg.VariableInfo.SuggestedTelemetryName + " = (" + arg.MacroVariableName + "); \\");

                        decodedTraceLine.AddConfigFileProperty(ModuleName, $"{arg.VariableInfo.SuggestedTelemetryName}", currentOffset.ToString());
                        decodedTraceLine.AddConfigFileProperty(ModuleName, $"{arg.VariableInfo.SuggestedTelemetryName}_Len", "2");
                        currentOffset += 2;
                        hasAnyDefs = true;
                        break;

                    case CLogEncodingType.Int32:
                        structDef.AppendLine($"    DIAG_TRACE_32 clog_{arg.VariableInfo.SuggestedTelemetryName};");
                        macroBody.AppendLine("        myEvent.clog_" + arg.VariableInfo.SuggestedTelemetryName + " = (" + arg.MacroVariableName + "); \\");

                        decodedTraceLine.AddConfigFileProperty(ModuleName, $"{arg.VariableInfo.SuggestedTelemetryName}", currentOffset.ToString());
                        decodedTraceLine.AddConfigFileProperty(ModuleName, $"{arg.VariableInfo.SuggestedTelemetryName}_Len", "4");
                        currentOffset += 4;
                        hasAnyDefs = true;
                        break;

                    case CLogEncodingType.UInt32:
                        structDef.AppendLine($"    DIAG_TRACE_U32 clog_{arg.VariableInfo.SuggestedTelemetryName};");
                        macroBody.AppendLine("        myEvent.clog_" + arg.VariableInfo.SuggestedTelemetryName + " = (" + arg.MacroVariableName + "); \\");

                        decodedTraceLine.AddConfigFileProperty(ModuleName, $"{arg.VariableInfo.SuggestedTelemetryName}", currentOffset.ToString());
                        decodedTraceLine.AddConfigFileProperty(ModuleName, $"{arg.VariableInfo.SuggestedTelemetryName}_Len", "4");
                        currentOffset += 4;
                        hasAnyDefs = true;
                        break;

                    case CLogEncodingType.Int64:
                        structDef.AppendLine($"    DIAG_TRACE_64 clog_{arg.VariableInfo.SuggestedTelemetryName};");
                        macroBody.AppendLine("        myEvent.clog_" + arg.VariableInfo.SuggestedTelemetryName + " = (" + arg.MacroVariableName + "); \\");

                        decodedTraceLine.AddConfigFileProperty(ModuleName, $"{arg.VariableInfo.SuggestedTelemetryName}", currentOffset.ToString());
                        decodedTraceLine.AddConfigFileProperty(ModuleName, $"{arg.VariableInfo.SuggestedTelemetryName}_Len", "8");
                        currentOffset += 8;
                        hasAnyDefs = true;
                        break;

                    case CLogEncodingType.UInt64:
                        structDef.AppendLine($"    DIAG_TRACE_U64 clog_{arg.VariableInfo.SuggestedTelemetryName};");
                        macroBody.AppendLine("        myEvent.clog_" + arg.VariableInfo.SuggestedTelemetryName + " = (" + arg.MacroVariableName + "); \\");

                        decodedTraceLine.AddConfigFileProperty(ModuleName, $"{arg.VariableInfo.SuggestedTelemetryName}", currentOffset.ToString());
                        decodedTraceLine.AddConfigFileProperty(ModuleName, $"{arg.VariableInfo.SuggestedTelemetryName}_Len", "8");
                        currentOffset += 8;
                        hasAnyDefs = true;
                        break;

                    case CLogEncodingType.Pointer:
                        structDef.AppendLine( $"    DIAG_TRACE_POINTER clog_{arg.VariableInfo.SuggestedTelemetryName};");
                        macroBody.AppendLine("        myEvent.clog_" + arg.VariableInfo.SuggestedTelemetryName + " = (DIAG_TRACE_POINTER)(" + arg.MacroVariableName + "); \\");

                        decodedTraceLine.AddConfigFileProperty(ModuleName, $"{arg.VariableInfo.SuggestedTelemetryName}", currentOffset.ToString());
                        decodedTraceLine.AddConfigFileProperty(ModuleName, $"{arg.VariableInfo.SuggestedTelemetryName}_Len", "8");
                        currentOffset += 8;
                        hasAnyDefs = true;
                        break;

                    case CLogEncodingType.ANSI_String:
                        //BUGBUG : Limit to 132.
                        structDef.AppendLine( $"    DIAG_TRACE_U8 clog_{arg.VariableInfo.SuggestedTelemetryName}[132]; // BUGBUG Limited");
                        macroBody.AppendLine($"        CLOG_COPY(myEvent.clog_{arg.VariableInfo.SuggestedTelemetryName}, {arg.MacroVariableName}, DIAG_MIN(132-1, strlen({arg.MacroVariableName}))); \\");
                        macroBody.AppendLine($"        myEvent.clog_{arg.VariableInfo.SuggestedTelemetryName}[131] = 0;\\");

                        decodedTraceLine.AddConfigFileProperty(ModuleName, $"{arg.VariableInfo.SuggestedTelemetryName}", currentOffset.ToString());
                        decodedTraceLine.AddConfigFileProperty(ModuleName, $"{arg.VariableInfo.SuggestedTelemetryName}_Len", "132");
                        currentOffset += 132;

                        hasAnyDefs = true;
                        break;

                    case CLogEncodingType.UNICODE_String:

                        //BUGBUG : Limit to 32.
                        structDef.AppendLine($"    DIAG_TRACE_U16 clog_{arg.VariableInfo.SuggestedTelemetryName}[32]; // BUGBUG Limited");
                        macroBody.AppendLine($"        DIAG_TRACE_COPY_WCHAR(myEvent.clog_{arg.VariableInfo.SuggestedTelemetryName}, ({arg.MacroVariableName})); \\");
                        macroBody.AppendLine($"        myEvent.clog_{arg.VariableInfo.SuggestedTelemetryName}[31] = 0;\\");

                        decodedTraceLine.AddConfigFileProperty(ModuleName, $"{arg.VariableInfo.SuggestedTelemetryName}", currentOffset.ToString());
                        decodedTraceLine.AddConfigFileProperty(ModuleName, $"{arg.VariableInfo.SuggestedTelemetryName}_Len", "32");
                        currentOffset += 32;

                        hasAnyDefs = true;
                        break;

                    default:
                        throw new CLogEnterReadOnlyModeException("DiagTrace:UnknownType:" + node.EncodingType, CLogHandledException.ExceptionType.EncoderIncompatibleWithType, decodedTraceLine.match);
                }

                if(!String.IsNullOrEmpty(args))
                    args += ",";
                args += $"CLOG_DECODED_ARG->clog_{arg.VariableInfo.SuggestedTelemetryName}";

            }

	        if (!hasAnyDefs)
            {
                structDef.AppendLine("    DIAG_TRACE_POINTER unused;");
            }


            structDef.AppendLine($"    int alignment;");
            macroBody.AppendLine("        myEvent.alignment = 0xAABBAABB; \\");
            decodedTraceLine.AddConfigFileProperty(ModuleName, $"alignment", currentOffset.ToString());
            decodedTraceLine.AddConfigFileProperty(ModuleName, $"alignment_Len", "4");
            currentOffset += 4;

            structDef.AppendLine("} " + structName + ";");
            structDef.AppendLine("#pragma pack(0)");


            // Only define _ARGS if there are args - this makes build breaks, which are easier to detect
            if(!String.IsNullOrEmpty(args))
                args = "," + args;
            headerFile.AppendLine($"#define {structName}_ARGS(CLOG_DECODED_ARG) {args}");

            headerFile.AppendLine(structDef.ToString());

            macroBody.AppendLine("        DIAG_WRITE(" + providerHandle + ", &myEvent, sizeof(myEvent));\\");

            macroBody.AppendLine("    }\\");
            macroBody.AppendLine("}\\");
        }
    }
}
