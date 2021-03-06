﻿/*++

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
    public class CLogSysLogModule : ICLogOutputModule
    {
        public string ModuleName
        {
            get
            {
                return "SYSLOG";
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
            header.AppendLine($"// CLOG SysLog {DateTime.Now}------");
            header.AppendLine($"#include <syslog.h>");
        }

        public void FinishedProcessing(StringBuilder header, StringBuilder sourceFile)
        {
        }

        public void TraceLineDiscovered(string sourceFile, CLogDecodedTraceLine decodedTraceLine, CLogSidecar sidecar, StringBuilder macroPrefix, StringBuilder inline, StringBuilder function)
        {
            string priority = decodedTraceLine.GetConfigurationValue(ModuleName, "Priority");

            inline.Append($"syslog({priority}, \"{decodedTraceLine.TraceString}\"");
            for (int i = 0; i < decodedTraceLine.splitArgs.Length; ++i)
            {
                inline.Append(", " + decodedTraceLine.splitArgs[i].MacroVariableName);
            }
            inline.Append(");\\\n");
        }
    }
}
