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

namespace clog.TraceEmitterModules
{
    public class CLogSystemTapModule : ICLogOutputModule
    {
        //
        // alreadyEmitted maintains a list of all the functions that we have emitted
        //
        private readonly HashSet<string> alreadyEmitted = new HashSet<string>();
        private bool emittedHeader = false;

        public string ModuleName
        {
            get
            {
                return "SYSTEMTAP";
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
        }

        public void FinishedProcessing(StringBuilder header, StringBuilder sourceFile)
        {
        }

        public void TraceLineDiscovered(string sourceFile, CLogDecodedTraceLine decodedTraceLine, CLogSidecar sidecar, StringBuilder macroPrefix, StringBuilder inline, StringBuilder function)
        {
            if (!emittedHeader)
            {
                function.AppendLine($"// SYSTEMTAP {DateTime.Now}------");
                function.AppendLine("#include <sys/sdt.h>");
                emittedHeader = true;
            }

            //
            // Generate a function name that is unique; this is where you'll attach a DTrace probe.
            //
            //     ScopePrefix is passed in during compliation, it is a unique name that indicates the library
            //
            string uid = "PROBE_DTRACE_" + decodedTraceLine.configFile.ScopePrefix + "_" + Path.GetFileName(sourceFile).Replace(".", "_");
            uid = uid.Replace("{", "");
            uid = uid.Replace("}", "");
            uid = uid.Replace("-", "");

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

            if (0 == decodedTraceLine.splitArgs.Length)
            {
                function.AppendLine($"DTRACE_PROBE({decodedTraceLine.configFile.ScopePrefix}, {decodedTraceLine.UniqueId});");
            }
            else
            {
                function.Append($"DTRACE_PROBE{decodedTraceLine.splitArgs.Length}({decodedTraceLine.configFile.ScopePrefix}, {decodedTraceLine.UniqueId}");
                foreach (var arg in decodedTraceLine.splitArgs)
                {
                    function.Append($", {arg.MacroVariableName}");
                }
                function.AppendLine(");");
            }

            function.AppendLine("}\r\n\r\n");
        }
    }
}
