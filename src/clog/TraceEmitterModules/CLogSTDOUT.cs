/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    STDOUT output module for CLOG.  This module emits both human readable traces (think printf()) as well as encodes the name/value pairs into a
    printable string, that can be decoded into it's originating pieces.  This is useful for using STDOUT as a tranmission source
    for CLOG logs - in doing so the user retains both human readable characteristics, as well as the ability to decode
    into a form typically reserved for binary encoders

--*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using clogutils;
using clogutils.MacroDefinations;

namespace clog.TraceEmitterModules
{
    public class CLogSTDOUT : ICLogOutputModule
    {
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

        public void FinishedProcessing(CLogOutputInfo outputInfo, StringBuilder header, StringBuilder sourceFile)
        {
        }

        bool emittedHeader = false;
        public void TraceLineDiscovered(string sourceFile, CLogOutputInfo outputInfo, CLogDecodedTraceLine decodedTraceLine, CLogSidecar sidecar, StringBuilder macroPrefix, StringBuilder inline, StringBuilder function)
        {
            CLogFileProcessor.DecomposedString clean;
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

                emittedHeader = true;
            }

            //
            // Only emit the function once;  we may be called multiple times should someone emit an event multiple times in the same file
            //    (usually error paths)
            //
            string argsString = string.Empty;
            string macroString = string.Empty;

            foreach (var arg in decodedTraceLine.splitArgs)
            {
                if (!arg.TypeNode.Synthesized &&
                    arg.TypeNode.EncodingType != CLogEncodingType.UniqueAndDurableIdentifier &&
                    arg.TypeNode.EncodingType != CLogEncodingType.UserEncodingString)
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
                    if (CLogEncodingType.ByteArray == arg.TypeNode.EncodingType)
                    {
                        argsString += $"{seperatorB} unsigned int {arg.VariableInfo.SuggestedTelemetryName}_len{seperatorA}";
                        macroString += $"{seperatorB} {arg.MacroVariableName}_len{seperatorA}";
                    }

                    argsString += $"{seperatorB} {arg.TypeNode.CType} {arg.MacroVariableName}";
                    macroString += $"{seperatorB} {arg.MacroVariableName}";
                }
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
                        printf += "0x%llx";
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
                        printf += "[Not_Supported]";
                        break;
                }
            }

            //
            // Print the remainder of user text (the tail end);  if there are no types at all then 'TraceString' is just a constant string
            //
            if (types.Length >= 1)
            {
                string tail = decodedTraceLine.TraceString.Substring(types[types.Length - 1].ArgStartingIndex + types[types.Length - 1].ArgLength);
                printf += tail;
            }
            else
            {
                printf += decodedTraceLine.TraceString;
            }

            printf += "\\n";

            inline.Append($"    {printmacro}(\"{printf}\"");
            foreach (var arg in decodedTraceLine.splitArgs)
            {
                if (arg.TypeNode.Synthesized || arg.TypeNode.EncodingType == CLogEncodingType.UniqueAndDurableIdentifier || arg.TypeNode.EncodingType == CLogEncodingType.UserEncodingString)
                    continue;

                string cast = "";
                switch (arg.TypeNode.EncodingType)
                {
                    case CLogEncodingType.Int32:
                        //cast = "(int)";
                        break;
                    case CLogEncodingType.UInt32:
                        //cast = "(unsigned int)";
                        break;
                    case CLogEncodingType.Int64:
                        //cast = "(__int64)";
                        break;
                    case CLogEncodingType.UInt64:
                        //cast = "(unsigned __int64)";
                        break;
                    case CLogEncodingType.ANSI_String:
                        break;
                    case CLogEncodingType.UNICODE_String:
                        break;
                    case CLogEncodingType.Pointer:
                        cast = "(unsigned long long int)";
                        break;
                    case CLogEncodingType.GUID:
                        cast = "(void*)";
                        break;
                    case CLogEncodingType.Int16:
                        //cast = "(__int16)";
                        break;
                    case CLogEncodingType.UInt16:
                        //cast = "(unsigned __int16)";
                        break;
                    case CLogEncodingType.Int8:
                        //cast = "(int)";
                        break;
                    case CLogEncodingType.UInt8:
                        //cast = "(int)";
                        break;
                    case CLogEncodingType.ByteArray:
                        //cast = "(void*)";
                        continue;
                }
                inline.Append($", {cast}(" + arg.MacroVariableName + ")");
            }

            inline.Append(");");
        }
    }
}
