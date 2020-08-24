/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    A stream manages the send and receive queues for application data. This file
    contains the initialization and cleanup functionality for the stream.

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

        public void FinishedProcessing(StringBuilder header, StringBuilder sourceFile)
        {
        }

        public void TraceLineDiscovered(string sourceFile, CLogDecodedTraceLine decodedTraceLine, CLogSidecar sidecar, StringBuilder macroPrefix, StringBuilder inline, StringBuilder function)
        {
            //string priority = decodedTraceLine.GetConfigurationValue(ModuleName, "Priority");
            string clean;

            CLogFileProcessor.CLogTypeContainer[] types = CLogFileProcessor.BuildTypes(decodedTraceLine.configFile, null, decodedTraceLine.TraceString, null, out clean);

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
                        printf += "%p";
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

            if (types.Length >= 1)
            {
                string tail = decodedTraceLine.TraceString.Substring(types[types.Length - 1].ArgStartingIndex + types[types.Length - 1].ArgLength);
                printf += tail;
            }

            inline.Append($"printf(\"{printf}\\r\\n\"");
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
                        throw new NotImplementedException("UNICODE NOT SUPPORTED");
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
                inline.Append($", {cast}(" + decodedTraceLine.splitArgs[i].MacroVariableName + ")");
            }
            inline.Append(");\\\n");
        }
    }
}
