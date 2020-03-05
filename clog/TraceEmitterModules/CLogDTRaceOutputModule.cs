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

namespace clog.TraceEmitterModules
{
    public class CLogDTRaceOutputModule : ICLogOutputModule
    {
        private readonly HashSet<string> alreadyEmitted = new HashSet<string>();
        private readonly HashSet<string> knownHashes = new HashSet<string>();

        public string ModuleName
        {
            get
            {
                return "DTRACE";
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
            header.AppendLine($"// DTrace {DateTime.Now}------");
        }

        public void FinishedProcessing(StringBuilder header, StringBuilder sourceFile)
        {
        }

        public void TraceLineDiscovered(string sourceFile, CLogDecodedTraceLine decodedTraceLine, CLogSidecar sidecar, StringBuilder macroPrefix, StringBuilder inline, StringBuilder function)
        {
            string uid = "MNO_" + decodedTraceLine.configFile.BUGBUG_String + "_" + Path.GetFileName(sourceFile).Replace(".", "_") + "_" + decodedTraceLine.UniqueId;

            uid = "DTRACE_" + decodedTraceLine.configFile.BUGBUG_String + "_" + Path.GetFileName(sourceFile).Replace(".", "_") + "_" + decodedTraceLine.UniqueId + Guid.NewGuid();
            uid = uid.Replace("{", "");
            uid = uid.Replace("}", "");
            uid = uid.Replace("-", "");

            if(alreadyEmitted.Contains(uid))
            {
                return;
            }

            alreadyEmitted.Add(uid);


            string argsString = string.Empty;
            string macroString = string.Empty;

            foreach(var arg in decodedTraceLine.splitArgs)
            {
                CLogEncodingCLogTypeSearch v = decodedTraceLine.configFile.FindType(arg);

                if(!v.Synthesized)
                {
                    if(string.IsNullOrEmpty(argsString))
                    {
                        if(CLogEncodingType.ByteArray == v.EncodingType)
                        {
                            argsString += $"unsigned int {arg.VariableInfo.SuggestedTelemetryName}_len, ";
                            macroString += $"{arg.MacroVariableName}_len";
                        }

                        argsString += $"{v.CType} {arg.MacroVariableName}";
                        macroString += $"{arg.MacroVariableName}";
                    }
                    else
                    {
                        if(CLogEncodingType.ByteArray == v.EncodingType)
                        {
                            argsString += $", unsigned int {arg.VariableInfo.SuggestedTelemetryName}_len";
                            macroString += $", {arg.MacroVariableName}_len";
                        }

                        argsString += $", {v.CType} {arg.MacroVariableName}";
                        macroString += $",{arg.MacroVariableName}";
                    }
                }
            }

            macroPrefix.AppendLine("void " + uid + "(" + argsString + ");\r\n");
            inline.AppendLine($"{uid}({macroString});\\");
            function.AppendLine($"void {uid}({argsString})" + "{}\r\n\r\n");
        }
    }
}
