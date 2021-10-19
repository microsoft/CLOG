/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    A stream manages the send and receive queues for application data. This file
    contains the initialization and cleanup functionality for the stream.

--*/

using System;
using System.Text;
using clogutils;

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

        public void FinishedProcessing(CLogOutputInfo outputInfo, StringBuilder header, StringBuilder sourceFile)
        {
        }

        public void TraceLineDiscovered(string sourceFile, CLogOutputInfo outputInfo, CLogDecodedTraceLine decodedTraceLine, CLogSidecar sidecar, StringBuilder macroPrefix, StringBuilder inline, StringBuilder function)
        {
            string priority = decodedTraceLine.GetConfigurationValue(ModuleName, "Priority");

            inline.Append($"syslog({priority}, \"{decodedTraceLine.TraceString}\"");

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

                inline.Append(", " + arg.MacroVariableName);
            }
            inline.Append(");\\\n");
        }
    }
}
