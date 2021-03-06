﻿/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    A stream manages the send and receive queues for application data. This file
    contains the initialization and cleanup functionality for the stream.

--*/

using System.Data;
using System.IO;
using System.Text;
using clogutils;

namespace clog.TraceEmitterModules
{
    public class CLogLTTNGOutputModule : ICLogOutputModule
    {
        private readonly string _clogFile;
        private readonly string _lttngHeaderFileName;
        private readonly string _lttngProviderName;
        private readonly bool _lttngDynamicTracepoint;
        private readonly StringBuilder lttngFile = new StringBuilder();

        public CLogLTTNGOutputModule(string sourceFile, string clogFile, string lttngHeaderFileName, bool dynamicTracepoint)
        {
            _clogFile = clogFile;
            _lttngHeaderFileName = lttngHeaderFileName;
            _lttngProviderName = "CLOG_" + Path.GetFileName(sourceFile).ToUpper().Replace(".", "_");
            _lttngDynamicTracepoint = dynamicTracepoint;

            if (File.Exists(_lttngHeaderFileName))
            {
                File.Delete(_lttngHeaderFileName);
            }
        }

        public string ModuleName
        {
            get
            {
                return "LTTNG";
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
            header.AppendLine("#undef TRACEPOINT_PROVIDER");
            header.AppendLine($"#define TRACEPOINT_PROVIDER {_lttngProviderName}");

            if (_lttngDynamicTracepoint)
            {
                header.AppendLine("#undef TRACEPOINT_PROBE_DYNAMIC_LINKAGE");
                header.AppendLine("#define  TRACEPOINT_PROBE_DYNAMIC_LINKAGE");
            }

            header.AppendLine("#undef TRACEPOINT_INCLUDE");
            header.AppendLine($"#define TRACEPOINT_INCLUDE \"{_lttngHeaderFileName}\"");

            header.AppendLine($"#if !defined(DEF_{_lttngProviderName}) || defined(TRACEPOINT_HEADER_MULTI_READ)");
            header.AppendLine($"#define DEF_{_lttngProviderName}");

            header.AppendLine("#include <lttng/tracepoint.h>");

            header.AppendLine("#define __int64 __int64_t");


            header.AppendLine($"#include \"{_lttngHeaderFileName}\"");
            header.AppendLine("#endif");


            header.AppendLine("#include <lttng/tracepoint-event.h>");
        }

        public void FinishedProcessing(StringBuilder header, StringBuilder sourceFile)
        {
            string dir = Path.GetDirectoryName(_lttngHeaderFileName);

            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(_lttngHeaderFileName, lttngFile.ToString());

            if (_lttngDynamicTracepoint)
            {
                sourceFile.AppendLine("#ifdef BUILDING_TRACEPOINT_PROVIDER");
                sourceFile.AppendLine("#define TRACEPOINT_CREATE_PROBES");
                sourceFile.AppendLine("#else");
                sourceFile.AppendLine("#define TRACEPOINT_DEFINE");
                sourceFile.AppendLine("#endif");
            }
            else
            {
                sourceFile.AppendLine("#define TRACEPOINT_CREATE_PROBES");
                sourceFile.AppendLine("#define TRACEPOINT_DEFINE");
            }

            sourceFile.AppendLine($"#include \"{Path.GetFullPath(_clogFile)}\"");
        }


        public void TraceLineDiscovered(string sourceFile, CLogDecodedTraceLine decodedTraceLine, CLogSidecar sidecar, StringBuilder macroPrefix, StringBuilder inline, StringBuilder function)
        {
            int hashUInt;
            string hash;

            decodedTraceLine.macro.DecodeUniqueId(decodedTraceLine.match, decodedTraceLine.UniqueId, out hash, out hashUInt);

            int usedArguments = 0;
            foreach (var a in decodedTraceLine.splitArgs)
            {
                CLogFileProcessor.CLogVariableBundle arg = a;
                CLogEncodingCLogTypeSearch node = decodedTraceLine.configFile.FindType(arg, decodedTraceLine);

                switch (node.EncodingType)
                {
                    case CLogEncodingType.Synthesized:
                        continue;
                    case CLogEncodingType.ByteArray:
                        usedArguments += 2;
                        break;
                    default:
                        ++usedArguments;
                        break;
                }
            }

            if (usedArguments >= 11)
            {
                throw new ReadOnlyException($"Too Many arguments in {hash},  LTTNG accepts a max of 10");
            }

            lttngFile.AppendLine("");
            lttngFile.AppendLine("");
            lttngFile.AppendLine("");
            lttngFile.AppendLine("/*----------------------------------------------------------");
            lttngFile.AppendLine($"// Decoder Ring for {decodedTraceLine.UniqueId}");
            lttngFile.AppendLine($"// {decodedTraceLine.TraceString}");
            lttngFile.AppendLine($"// {decodedTraceLine.match.MatchedRegEx}");

            foreach (var arg in decodedTraceLine.splitArgs)
            {
                lttngFile.AppendLine($"// {arg.MacroVariableName} = {arg.MacroVariableName} = {arg.UserSuppliedTrimmed}");
            }
            lttngFile.AppendLine("----------------------------------------------------------*/");

            lttngFile.AppendLine($"TRACEPOINT_EVENT({_lttngProviderName}, {hash},");

            int argNum = 0;
            lttngFile.AppendLine("    TP_ARGS(");

            foreach (var a in decodedTraceLine.splitArgs)
            {
                CLogFileProcessor.CLogVariableBundle arg = a;
                CLogEncodingCLogTypeSearch node = decodedTraceLine.configFile.FindType(arg, decodedTraceLine);

                switch (node.EncodingType)
                {
                    case CLogEncodingType.Synthesized:
                        continue;

                    case CLogEncodingType.Skip:
                        continue;

                    case CLogEncodingType.UNICODE_String:
                        continue;
                }

                if (0 != argNum)
                {
                    if (CLogEncodingType.ByteArray == node.EncodingType)
                    {
                        lttngFile.Append(",");
                        lttngFile.AppendLine("");
                        lttngFile.Append($"        unsigned int, {arg.MacroVariableName}_len");
                    }

                    lttngFile.Append(",");
                    lttngFile.AppendLine("");
                    lttngFile.Append($"        {ConvertToClogType(node)}, {arg.MacroVariableName}");
                }
                else
                {
                    if (CLogEncodingType.ByteArray == node.EncodingType)
                    {
                        lttngFile.Append($"        unsigned int, {arg.MacroVariableName}_len");
                        lttngFile.Append(",");
                        lttngFile.AppendLine("");
                    }

                    lttngFile.Append($"        {ConvertToClogType(node)}, {arg.MacroVariableName}");
                }

                ++argNum;
            }


            lttngFile.Append("), ");
            lttngFile.AppendLine("");
            lttngFile.AppendLine("    TP_FIELDS(");

            foreach (var a in decodedTraceLine.splitArgs)
            {
                CLogFileProcessor.CLogVariableBundle arg = a;
                CLogEncodingCLogTypeSearch node = decodedTraceLine.configFile.FindType(arg, decodedTraceLine);

                switch (node.EncodingType)
                {
                    case CLogEncodingType.Synthesized:
                        continue;

                    case CLogEncodingType.Skip:
                        continue;

                    case CLogEncodingType.UNICODE_String:
                        continue;

                    case CLogEncodingType.ByteArray:
                        lttngFile.AppendLine(
                            $"        ctf_integer(unsigned int, {arg.MacroVariableName}_len, {arg.MacroVariableName}_len)");

                        lttngFile.AppendLine(
                            $"        ctf_sequence(char, {arg.MacroVariableName}, {arg.MacroVariableName}, unsigned int, {arg.MacroVariableName}_len)");
                        break;

                    case CLogEncodingType.Int8:
                        lttngFile.AppendLine(
                            $"        ctf_integer(char, {arg.MacroVariableName}, {arg.MacroVariableName})");
                        break;

                    case CLogEncodingType.UInt8:
                        lttngFile.AppendLine(
                            $"        ctf_integer(unsigned char, {arg.MacroVariableName}, {arg.MacroVariableName})");
                        break;


                    case CLogEncodingType.Int16:
                        lttngFile.AppendLine(
                            $"        ctf_integer(short, {arg.MacroVariableName}, {arg.MacroVariableName})");
                        break;

                    case CLogEncodingType.UInt16:
                        lttngFile.AppendLine(
                            $"        ctf_integer(unsigned short, {arg.MacroVariableName}, {arg.MacroVariableName})");
                        break;

                    case CLogEncodingType.Int32:
                        lttngFile.AppendLine(
                            $"        ctf_integer(int, {arg.MacroVariableName}, {arg.MacroVariableName})");
                        break;

                    case CLogEncodingType.UInt32:
                        lttngFile.AppendLine(
                            $"        ctf_integer(unsigned int, {arg.MacroVariableName}, {arg.MacroVariableName})");
                        break;

                    case CLogEncodingType.Int64:
                        lttngFile.AppendLine(
                            $"        ctf_integer(int64_t, {arg.MacroVariableName}, {arg.MacroVariableName})");
                        break;

                    case CLogEncodingType.UInt64:
                        lttngFile.AppendLine(
                            $"        ctf_integer(uint64_t, {arg.MacroVariableName}, {arg.MacroVariableName})");
                        break;

                    case CLogEncodingType.Pointer:
                        lttngFile.AppendLine(
                            $"        ctf_integer_hex(uint64_t, {arg.MacroVariableName}, {arg.MacroVariableName})");
                        break;

                    case CLogEncodingType.ANSI_String:
                        lttngFile.AppendLine(
                            $"        ctf_string({arg.MacroVariableName}, {arg.MacroVariableName})");
                        break;

                    default:
                        throw new CLogEnterReadOnlyModeException("LTTNG:UnknownType:" + node.EncodingType, CLogHandledException.ExceptionType.EncoderIncompatibleWithType, decodedTraceLine.match);
                }

                ++argNum;
            }

            lttngFile.AppendLine("    )"); //TRACEPONT_ARGS
            lttngFile.AppendLine(")"); //TRACEPOINT_EVENT


            string traceLine = $"tracepoint({_lttngProviderName}, {hash} ";

            foreach (var a in decodedTraceLine.splitArgs)
            {
                CLogFileProcessor.CLogVariableBundle arg = a;
                CLogEncodingCLogTypeSearch node = decodedTraceLine.configFile.FindType(arg, decodedTraceLine);

                if (string.IsNullOrEmpty(node.CType))
                {
                    throw new ReadOnlyException($"Missing CType Field : {node.DefinationEncoding}");
                }

                switch (node.EncodingType)
                {
                    case CLogEncodingType.Synthesized:
                        continue;

                    case CLogEncodingType.Skip:
                        continue;

                    case CLogEncodingType.UNICODE_String:
                        continue;
                }

                if (node.EncodingType == CLogEncodingType.ByteArray)
                {
                    traceLine += $", {arg.MacroVariableName}_len";
                }

                traceLine += $", {arg.MacroVariableName}";
            }

            traceLine += ");\\";
            inline.AppendLine(traceLine);
        }


        private string ConvertToClogType(CLogEncodingCLogTypeSearch node)
        {
            switch (node.EncodingType)
            {
                case CLogEncodingType.Int8:
                    return "char";

                case CLogEncodingType.UInt8:
                    return "unsigned char";

                case CLogEncodingType.Int32:
                    return "int";

                case CLogEncodingType.UInt32:
                    return "unsigned int";

                case CLogEncodingType.Int64:
                    return "long long";

                case CLogEncodingType.UInt64:
                    return "unsigned long long";

                case CLogEncodingType.Pointer:
                    return "const void *";

                case CLogEncodingType.Int16:
                    return "short";

                case CLogEncodingType.UInt16:
                    return "unsigned short";

                case CLogEncodingType.ByteArray:
                    return "const void *";

                case CLogEncodingType.ANSI_String:
                    return "const char *";

                default:
                    throw new CLogEnterReadOnlyModeException("InvalidType:" + node.EncodingType, CLogHandledException.ExceptionType.EncoderIncompatibleWithType, null);
            }
        }
    }
}
