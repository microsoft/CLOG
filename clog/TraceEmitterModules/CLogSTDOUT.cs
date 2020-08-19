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
    public class CLogSTDOUT : ICLogOutputModule
    {
        //
        // alreadyEmitted maintains a list of all the functions that we have emitted
        //
        private readonly HashSet<string> alreadyEmitted = new HashSet<string>();


        public string ModuleName
        {
            get
            {
                return "STDOUT";
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
            header.AppendLine($"// CLOG STDIO {DateTime.Now}------");
            header.AppendLine($"#include <stdio.h>");
        }

        public void FinishedProcessing(StringBuilder header, StringBuilder sourceFile)
        {
        }

        bool emittedHeader = false;
        public void TraceLineDiscovered(string sourceFile, CLogDecodedTraceLine decodedTraceLine, CLogSidecar sidecar, StringBuilder macroPrefix, StringBuilder inline, StringBuilder function)
        {
            string clean;

            CLogFileProcessor.CLogTypeContainer[] types = CLogFileProcessor.BuildTypes(decodedTraceLine.configFile, null, decodedTraceLine.TraceString, null, out clean);
            CLogExportModuleDefination moduleSettings = decodedTraceLine.GetMacroConfigurationProfile().FindExportModule(ModuleName);

            string printmacro;
            if (!moduleSettings.CustomSettings.TryGetValue("PrintMacro", out printmacro))
                printmacro = "printf";           


            if (!emittedHeader)
            {
                string printHeader;
                if (!moduleSettings.CustomSettings.TryGetValue("PrintHeader", out printHeader))
                    printHeader = "stdio.h";

                function.AppendLine($"// STDIO {DateTime.Now}------");
                function.AppendLine("#include <" + printHeader + ">");
                function.AppendLine("#define CLOG_ENCODE_STDIO_BYTES(ptr, len) if((int)len <= (int)bytesRemaining) { memcpy(head, ptr, len); head+=len; bytesRemaining-=len;}");
                emittedHeader = true;
            }

            //
            // Generate a function name that is unique; this is where you'll attach a DTrace probe.
            //
            //     ScopePrefix is passed in during compliation, it is a unique name that indicates the library
            //
            string uid = "CLOG_STDOUT_ENCODER_TRACE_" + decodedTraceLine.configFile.ScopePrefix + "_" + Path.GetFileName(sourceFile) + "_" + decodedTraceLine.UniqueId;
            uid = uid.Replace("{", "");
            uid = uid.Replace("}", "");
            uid = uid.Replace("-", "");
            uid = uid.Replace(".", "_");
            uid = uid.ToUpper();

            //
            // Only emit the function once;  we may be called multiple times should someone emit an event multiple times in the same file 
            //    (usually error paths)
            //
            if (alreadyEmitted.Contains(uid))
            {
                return;
            }

            alreadyEmitted.Add(uid);

            string argsString = string.Empty;
            string macroString = string.Empty;

            foreach (var arg in decodedTraceLine.splitArgs)
            {
                CLogEncodingCLogTypeSearch v = decodedTraceLine.configFile.FindType(arg, decodedTraceLine);

                if (!v.Synthesized)
                {
                    string seperatorA = "";
                    string seperatorB = "";

                    if (string.IsNullOrEmpty(argsString))
                    {
                        seperatorA = ",";
                        seperatorB = "";
                    }
                    else
                    {
                        seperatorA = "";
                        seperatorB = ",";
                    }

                    // If the encided type is 'binary' (length and payload) - for DTrace we emit the payload
                    //   length with the variable name <suggestedName>_len
                    if (CLogEncodingType.ByteArray == v.EncodingType)
                    {
                        argsString += $"{seperatorB} unsigned int {arg.VariableInfo.SuggestedTelemetryName}_len{seperatorA}";
                        macroString += $"{seperatorB} {arg.MacroVariableName}_len{seperatorA}";
                    }

                    argsString += $"{seperatorB} {v.CType} {arg.MacroVariableName}";
                    macroString += $"{seperatorB} {arg.MacroVariableName}";
                }
            }

            //
            // Emit a forward declare of our function into the header file
            //
            inline.AppendLine($"{uid}({macroString});\\");

            //
            // Emit into the CLOG macro (this is the actual code that goes into the product)
            // 
            macroPrefix.AppendLine("void " + uid + "(" + argsString + ");\r\n");

            //
            // Emit our implementation into the .c file that CLOG generates
            //
            function.AppendLine($"void {uid}({argsString})" + "{");
            function.AppendLine($"    unsigned char encodeBuffer[160];");
            function.AppendLine($"    unsigned int bytesRemaining = sizeof(encodeBuffer);");
            function.AppendLine($"    unsigned int header = 0;");
            function.AppendLine($"    unsigned char *head = &encodeBuffer[0];");
            function.AppendLine("    const char *uid = \"" + decodedTraceLine.UniqueId + "\";");


            EncodeVariable(function, "CLOG_UID", "uid", CLogEncodingType.ANSI_String, "strlen(uid)");

            foreach (var arg in decodedTraceLine.splitArgs)
            {
                CLogEncodingCLogTypeSearch v = decodedTraceLine.configFile.FindType(arg, decodedTraceLine);
                if (v.Synthesized)
                    continue;

                string dataLen;
                string varEncoding = "&" + arg.MacroVariableName;
                switch (v.EncodingType)
                {
                    case CLogEncodingType.Int32:
                        dataLen = "4";
                        break;
                    case CLogEncodingType.UInt32:
                        dataLen = "4";
                        break;
                    case CLogEncodingType.Int64:
                        dataLen = "8";
                        break;
                    case CLogEncodingType.UInt64:
                        dataLen = "8";
                        break;
                    case CLogEncodingType.ANSI_String:
                        varEncoding = arg.MacroVariableName;
                        dataLen = $"strlen({arg.MacroVariableName})";
                        break;/*
                    case CLogEncodingType.UNICODE_String:
                        function.AppendLine($"CLOG_ENCODE_BYTES({v.EncodingType});");
                        break;*/
                    case CLogEncodingType.Pointer:
                        dataLen = "8";
                        break;/*
                    case CLogEncodingType.GUID:
                        function.AppendLine($"CLOG_ENCODE_BYTES({v.EncodingType});");
                        break;*/
                    case CLogEncodingType.Int16:
                        dataLen = "2";
                        break;
                    case CLogEncodingType.UInt16:
                        dataLen = "2";
                        break;
                    case CLogEncodingType.Int8:
                        dataLen = "1";
                        break;
                    case CLogEncodingType.UInt8:
                        dataLen = "1";
                        break;
                    //case CLogEncodingType.ByteArray:
                    //function.AppendLine($"CLOG_ENCODE_BYTES({v.EncodingType});");
                    //    break;
                    default:
                        function.AppendLine($"    //************ SKIPPED DATA TYPE //************");
                        dataLen = "0";
                        break;
                }

                EncodeVariable(function, arg.VariableInfo.SuggestedTelemetryName, varEncoding, v.EncodingType, dataLen);
            }


            string printf = "";
            foreach (var t in types)
            {
                printf += t.LeadingString;
                switch (t.TypeNode.EncodingType)
                {
                    case CLogEncodingType.Int32:
                        printf += "%d";
                        break;
                    case CLogEncodingType.UInt32:
                        printf += "%u";
                        break;
                    case CLogEncodingType.Int64:
                        printf += "%lld";
                        break;
                    case CLogEncodingType.UInt64:
                        printf += "%llu";
                        break;
                    case CLogEncodingType.ANSI_String:
                        printf += "%s";
                        break;
                    case CLogEncodingType.UNICODE_String:
                        printf += "%S";
                        break;
                    case CLogEncodingType.Pointer:
                        printf += "%p";
                        break;
                    case CLogEncodingType.GUID:
                        printf += "%p";
                        break;
                    case CLogEncodingType.Int16:
                        printf += "%d";
                        break;
                    case CLogEncodingType.UInt16:
                        printf += "%d";
                        break;
                    case CLogEncodingType.Int8:
                        printf += "%d";
                        break;
                    case CLogEncodingType.UInt8:
                        printf += "%d";
                        break;
                    case CLogEncodingType.ByteArray:
                        printf += "%p";
                        break;
                }
            }

            // Print the remainder of user text
            if (types.Length >= 1)
            {
                string tail = decodedTraceLine.TraceString.Substring(types[types.Length - 1].ArgStartingIndex + types[types.Length - 1].ArgLength);
                printf += tail;
            }


            function.Append($"    {printmacro}(\"{printf}\"");
            for (int i = 0; i < decodedTraceLine.splitArgs.Length; ++i)
            {
                string cast = "";
                switch (types[i].TypeNode.EncodingType)
                {
                    case CLogEncodingType.Int32:
                        cast = "(int)";
                        break;
                    case CLogEncodingType.UInt32:
                        cast = "(unsigned int)";
                        break;
                    case CLogEncodingType.Int64:
                        cast = "(__int64)";
                        break;
                    case CLogEncodingType.UInt64:
                        cast = "(unsigned __int64)";
                        break;
                    case CLogEncodingType.ANSI_String:
                        break;
                    case CLogEncodingType.UNICODE_String:
                        break;
                    case CLogEncodingType.Pointer:
                        cast = "(void*)";
                        break;
                    case CLogEncodingType.GUID:
                        cast = "(void*)";
                        break;
                    case CLogEncodingType.Int16:
                        cast = "(__int16)";
                        break;
                    case CLogEncodingType.UInt16:
                        cast = "(unsigned __int16)";
                        break;
                    case CLogEncodingType.Int8:
                        cast = "(int)";
                        break;
                    case CLogEncodingType.UInt8:
                        cast = "(int)";
                        break;
                    case CLogEncodingType.ByteArray:
                        cast = "(void*)";
                        break;
                }
                function.Append($", {cast}(" + decodedTraceLine.splitArgs[i].MacroVariableName + ")");
            }

            function.Append(");");

            function.AppendLine("    unsigned int len =  sizeof(encodeBuffer) - bytesRemaining;");
            function.AppendLine("    char dest[(160*2)+2];");
            function.AppendLine("    int i = 0;");
            function.AppendLine("    int j = 0;");
            function.AppendLine("    while (len)");
            function.AppendLine("    {");
            function.AppendLine("        char a = (encodeBuffer[i] & 0xF0) >> 4;");
            function.AppendLine("        char b = (encodeBuffer[i] & 0x0F);");
            function.AppendLine("        dest[j] = a + 'a';");
            function.AppendLine("        dest[j + 1] = b + 'a';");
            function.AppendLine("        i++;");
            function.AppendLine("        j+=2;");
            function.AppendLine("        --len;");
            function.AppendLine("    }");
            function.AppendLine("    dest[i] = 0;");
            function.AppendLine("    " + printmacro + "(\"CLOG:%s:CLOG\\r\\n\", dest);");

            function.AppendLine("}\r\n\r\n");
        }

        private static void EncodeVariable(StringBuilder function, string SuggestedTelemetryName, string MacroVariableName, CLogEncodingType encodingType, string dataLen)
        {            
            function.AppendLine($"    //{SuggestedTelemetryName}");
            function.AppendLine($"    // Header = [version][EncodingType][ArgNameLen][DataLen]");
            function.AppendLine($"    header = (1 << 24) | ({((uint)encodingType)} << 16) | ({SuggestedTelemetryName.Length} << 8) | ({dataLen} << 0);");
            function.AppendLine($"    CLOG_ENCODE_STDIO_BYTES(&header, 4);");
            function.AppendLine( "    CLOG_ENCODE_STDIO_BYTES(\"" + SuggestedTelemetryName + "\"" + $", {SuggestedTelemetryName.Length});");
            function.AppendLine($"    CLOG_ENCODE_STDIO_BYTES({MacroVariableName}, {dataLen});");            
        }
    }
}
