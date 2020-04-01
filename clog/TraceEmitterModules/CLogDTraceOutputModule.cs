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

        public void FinishedProcessing(StringBuilder header, StringBuilder sourceFile)
        {
        }

        public void TraceLineDiscovered(string sourceFile, CLogDecodedTraceLine decodedTraceLine, CLogSidecar sidecar, StringBuilder macroPrefix, StringBuilder inline, StringBuilder function)
        {
            //
            // Generate a function name that is unique; this is where you'll attach a DTrace probe.
            //
            //     ScopePrefix is passed in during compliation, it is a unique name that indicates the library
            //
            string uid = "DTRACE_" + decodedTraceLine.configFile.ScopePrefix + "_" + Path.GetFileName(sourceFile).Replace(".", "_");
            uid = uid.Replace("{", "");
            uid = uid.Replace("}", "");
            uid = uid.Replace("-", "");
            
            //
            // Only emit the function once;  we may be called multiple times should someone emit an event multiple times in the same file 
            //    (usually error paths)
            //
            if(alreadyEmitted.Contains(uid))
            {
                return;
            }

            alreadyEmitted.Add(uid);

            string argsString = string.Empty;
            string macroString = string.Empty;

            foreach(var arg in decodedTraceLine.splitArgs)
            {
                CLogEncodingCLogTypeSearch v = decodedTraceLine.configFile.FindType(arg, decodedTraceLine);

                if(!v.Synthesized)
                {
                    string seperatorA = "";
                    string seperatorB = "";

                    if(string.IsNullOrEmpty(argsString)){
                        seperatorA = ",";
                        seperatorB = "";
                    } else {
                        seperatorA = "";
                        seperatorB = ",";
                    }
                
                    // If the encided type is 'binary' (length and payload) - for DTrace we emit the payload
                    //   length with the variable name <suggestedName>_len
                    if(CLogEncodingType.ByteArray == v.EncodingType)
                    {
                        argsString += $", unsigned int {arg.VariableInfo.SuggestedTelemetryName}_len{seperatorA}";
                        macroString += $"{seperatorB} {arg.MacroVariableName}_len";
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
            function.AppendLine($"void {uid}({argsString})" + "{}\r\n\r\n");
        }
    }
}
