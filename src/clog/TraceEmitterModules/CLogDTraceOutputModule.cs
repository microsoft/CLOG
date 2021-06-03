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
    public class CLogDTraceOutputModule : ICLogOutputModule
    {
        //
        // alreadyEmitted maintains a list of all the functions that we have emitted
        //
        private readonly HashSet<string> alreadyEmitted = new HashSet<string>();

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

        public void FinishedProcessing(CLogOutputInfo outputInfo, StringBuilder header, StringBuilder sourceFile)
        {
        }

        public void TraceLineDiscovered(string sourceFile, CLogOutputInfo outputInfo, CLogDecodedTraceLine decodedTraceLine, CLogSidecar sidecar, StringBuilder macroPrefix, StringBuilder inline, StringBuilder function)
        {
            //
            // Generate a function name that is unique; this is where you'll attach a DTrace probe.
            //
            //     ScopePrefix is passed in during compliation, it is a unique name that indicates the library
            //
            string uid = "DTRACE_" + decodedTraceLine.configFile.ScopePrefix + "_" + Path.GetFileName(sourceFile).Replace(".", "_") + "_" + decodedTraceLine.UniqueId;
            uid = uid.Replace("{", "");
            uid = uid.Replace("}", "");
            uid = uid.Replace("-", "");

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
            // Emit into the CLOG macro (this is the actual code that goes into the product)
            // 
            macroPrefix.AppendLine("void " + uid + "(" + argsString + ");\r\n");

            //
            // Emit our foward delcaration and implementation into the .c file that CLOG generates
            //
            if (!alreadyEmitted.Contains(uid))
            {
                inline.AppendLine($"{uid}({macroString});\\");
                function.AppendLine($"void {uid}({argsString})" + "{}\r\n\r\n");
                alreadyEmitted.Add(uid);
            }
        }
    }
}
