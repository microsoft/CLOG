/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    This class implements methods that emit C/C++ code that interacts with clog.h (which is used within your product code to define events)

    Should other lanaguages be desired (C# for example), this is the emission code that would likly need to be extended

--*/

using clogutils;
using clogutils.ConfigFile;
using clogutils.MacroDefinations;
using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace clogutils
{
    public class CLogFullyDecodedMacroEmitter : CLogFileProcessor.ICLogFullyDecodedLineCallbackInterface
    {
        private readonly StringBuilder _headerFile = new StringBuilder();
        private readonly StringBuilder _headerInit = new StringBuilder();
        private readonly string _inputSourceFile;

        private readonly HashSet<string> _knownHashes = new HashSet<string>();
        private readonly List<ICLogOutputModule> _modules = new List<ICLogOutputModule>();
        private readonly CLogSidecar _sidecar;
        private readonly StringBuilder _sourceFile = new StringBuilder();

        private readonly HashSet<ICLogOutputModule> m_unusedModules = new HashSet<ICLogOutputModule>();

        public CLogFullyDecodedMacroEmitter(string inputSourceFile, CLogSidecar sidecar)
        {
            _inputSourceFile = inputSourceFile;
            _sidecar = sidecar;
            _sourceFile.AppendLine($"// CLOG generated {DateTimeOffset.Now}");
            _sourceFile.AppendLine("#include <clog.h>");
        }

        public string HeaderInit
        {
            get { return _headerInit.ToString(); }
        }

        public string HeaderFile
        {
            get { return _headerFile.ToString(); }
        }

        public string SourceFile
        {
            get { return _sourceFile.ToString(); }
        }

        public void TraceLineDiscovered(CLogDecodedTraceLine decodedTraceLine, StringBuilder r)
        {
            r = null;

            /*
            if (_knownHashes.ContainsKey(decodedTraceLine.UniqueId) && _knownHashes[decodedTraceLine.UniqueId] == decodedTraceLine.
                return;
            */

            string argsString = "";
            int clogArgCountForMacroAlignment = 2; // decodedTraceLine.splitArgs.Length + 1;

            foreach (var arg in decodedTraceLine.splitArgs)
            {
                switch (decodedTraceLine.configFile.FindType(arg, decodedTraceLine).EncodingType)
                {
                    case CLogEncodingType.ByteArray:
                        clogArgCountForMacroAlignment += 2;

                        // Verify the input argument contains CLOG_BYTEARRAY - this will aid in debugging
                        if (!arg.VariableInfo.UserSuppliedTrimmed.Contains("CLOG_BYTEARRAY"))
                        {
                            CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, $"Trace ID '{decodedTraceLine.UniqueId}' contains a ByteArray type that is not using the CLOG_BYTEARRAY macro");
                            CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, "    Please encode the following argument with CLOG_BYTEARRAY(length, pointer)");
                            CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, "");
                            CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, $"// {decodedTraceLine.match.MatchedRegEx}");
                            CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, "");
                            CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, $"Failing Arg: ");
                            CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, arg.VariableInfo.UserSuppliedTrimmed);
                            throw new CLogEnterReadOnlyModeException("ByteArrayNotUsingCLOG_BYTEARRAY", CLogHandledException.ExceptionType.ByteArrayMustUseMacro, decodedTraceLine.match);
                        }
                        break;
                    default:
                        clogArgCountForMacroAlignment++;
                        break;
                }
            }


            string implSignature = $" clogTraceImpl_{clogArgCountForMacroAlignment}_ARGS_TRACE_{decodedTraceLine.UniqueId}(";

            string macroName = $"_clog_{clogArgCountForMacroAlignment}_ARGS_TRACE_{decodedTraceLine.UniqueId}";
            _headerFile.AppendLine($"#ifndef {macroName}");

            if (-1 != decodedTraceLine.macro.EncodedArgNumber)
            {
                implSignature += "const char *uniqueId";
                argsString += "uniqueId";

                int idx = 1;
                foreach (var arg in decodedTraceLine.splitArgs)
                {
                    CLogEncodingCLogTypeSearch v = decodedTraceLine.configFile.FindType(arg, decodedTraceLine);

                    if (idx == decodedTraceLine.macro.EncodedArgNumber)
                    {
                        implSignature += ", const char *encoded_arg_string";
                        argsString += ", encoded_arg_string";
                    }

                    ++idx;

                    if (!v.Synthesized)
                    {
                        implSignature += $", {v.CType} {arg.VariableInfo.SuggestedTelemetryName}";
                        argsString += $", {arg.VariableInfo.SuggestedTelemetryName}";

                        if (v.EncodingType == CLogEncodingType.ByteArray)
                        {
                            implSignature += $", int {arg.VariableInfo.SuggestedTelemetryName}_len";
                            argsString += $", {arg.VariableInfo.SuggestedTelemetryName}_len";
                        }

                        arg.MacroVariableName = $"{arg.VariableInfo.SuggestedTelemetryName}";
                    }
                }

                if (idx == decodedTraceLine.macro.EncodedArgNumber)
                {
                    implSignature += ", const char *encoded_arg_string";
                    argsString += ", encoded_arg_string";
                }
            }
            else
            {
                argsString += "uniqueId";
            }

            implSignature += ")";

            StringBuilder macroBody = new StringBuilder();
            
            _headerFile.AppendLine("");
            _headerFile.AppendLine("");
            _headerFile.AppendLine("");
            _headerFile.AppendLine("/*----------------------------------------------------------");
            _headerFile.AppendLine($"// Decoder Ring for {decodedTraceLine.UniqueId}");
            _headerFile.AppendLine($"// {decodedTraceLine.TraceString}");
            _headerFile.AppendLine($"// {decodedTraceLine.match.MatchedRegEx}");

            foreach (var arg in decodedTraceLine.splitArgs)
            {
                _headerFile.AppendLine($"// {arg.MacroVariableName} = {arg.VariableInfo.SuggestedTelemetryName} = {arg.VariableInfo.UserSuppliedTrimmed}");
            }

            _headerFile.AppendLine("----------------------------------------------------------*/");

            //
            // BUGBUG: not fully implemented - the intent of 'implSignature' is to give a turn key
            //    way for a module to emit code without the need for the module to emit its own function
            //
            //_headerFile.AppendLine($"void {implSignature};");
            macroBody.AppendLine($"#define {macroName}({argsString})" + "\\");
        

            foreach (ICLogOutputModule module in _modules)
            {
                CLogConfigurationProfile configProfile = decodedTraceLine.GetMacroConfigurationProfile();

                if (module.ManditoryModule || configProfile.ModuleNames.Contains(module.ModuleName.ToUpper()))
                {
                    if (m_unusedModules.Contains(module))
                    {
                        module.InitHeader(_headerInit);
                        m_unusedModules.Remove(module);
                    }

                    CLogTraceLineInformation existingTraceInfo;

                    if (!_sidecar.ModuleUniqueness.IsUnique(module, decodedTraceLine, out existingTraceInfo))
                    {
                        if (decodedTraceLine.configFile.OverwriteHashCollisions || existingTraceInfo.UniquenessHash == Guid.Empty)
                        {
                            CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Wrn, "    The signature for the previously defined event is being overwritten:");
                            CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Wrn, $"        ConfigFile:{decodedTraceLine.configFile.FilePath}");
                            CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Wrn, $"        TraceID:{existingTraceInfo.TraceID}");
                            CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Wrn, $"        UniquenessHash:{existingTraceInfo.UniquenessHash}");

                            _sidecar.RemoveTraceLine(existingTraceInfo);
							_knownHashes.Remove(decodedTraceLine.UniqueId);
                            _sidecar.TraceLineDiscovered(_inputSourceFile, decodedTraceLine, _sidecar, _headerFile,
                                macroBody,
                                _sourceFile);
                        }
                        else
                        {
                            if (existingTraceInfo.Unsaved)
                            {
                                CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, $"Trace ID '{existingTraceInfo.TraceID}' is not unique within this file");
                                CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, $"");
                                CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, $"Previous Declaration: ");
                                CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, $"File/Line: {CLogConsoleTrace.GetFileLine(existingTraceInfo.PreviousFileMatch.match)}");
                                CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, $"{existingTraceInfo.PreviousFileMatch.match}");
                                CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, $"");
                                CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, $"");

                                CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, $"Current Declaration: ");
                                CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, $"File/Line: {CLogConsoleTrace.GetFileLine(decodedTraceLine.match)}");
                                CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, $"{decodedTraceLine.match}");
                                CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, $"");
                                CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, $"");
                            }
                            else
                            {
                                CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, $"Trace ID '{existingTraceInfo.TraceID}' is not unique - somewhere else in your library this unique ID has different");
                                CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, "    encoding string, or argument types.  You either need to back out your change, or use a different unique identifier");
                                CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, "    If you've made changes that wont impact tools (for example fixing a");
                                CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, "    typo in a trace string) - have two options to override/refresh the signature check");
                                CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Std, "");
                                CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Std, "   Force/Clobber the event signature - indicating you desire breaking the uniqueness contract");
                                CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Std, $"    1. remove UniquenessHash ({existingTraceInfo.UniquenessHash}) frome this TraceID({existingTraceInfo.TraceID}) in file {decodedTraceLine.configFile.FilePath}");
                                CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Std, $"    2. specify the --overwriteHashCollisions command line argument (good if you're making lots of changes that are all safe)");
                                CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Tip, $"    3. set the environment variable CLOG_DEVELOPMENT_MODE=1  ($env:CLOG_DEVELOPMENT_MODE=1)");
                                CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Std, "");
                                CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Std, "");
                                CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Wrn, "    The signature for the previously defined event:");
                                CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Wrn, $"        ConfigFile:{decodedTraceLine.configFile.FilePath}");
                                CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Wrn, $"        TraceID:{existingTraceInfo.TraceID}");
                                CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Wrn, $"        UniquenessHash:{existingTraceInfo.UniquenessHash}");
                            }

                            throw new CLogEnterReadOnlyModeException("TraceIDNotUnique", CLogHandledException.ExceptionType.TaceIDNotUnique, decodedTraceLine.match);
                        }
                    }

                    if (!_knownHashes.Contains(decodedTraceLine.UniqueId))
                    {
                        _sidecar.InsertTraceLine(module, decodedTraceLine);

                        var c = decodedTraceLine.configFile.MacroConfigurations[decodedTraceLine.macro.MacroConfiguration[decodedTraceLine.configFile.ProfileName]];
                        if (!c.SkipProcessing)
                        {
                            module.TraceLineDiscovered(_inputSourceFile, decodedTraceLine, _sidecar, _headerFile,
                                macroBody,
                                _sourceFile);
                        }
                    }
                }
            }

            _knownHashes.Add(decodedTraceLine.UniqueId);
            _headerFile.AppendLine(macroBody.ToString());
            _headerFile.AppendLine("#endif");
            _headerFile.AppendLine("");
            _headerFile.AppendLine("");
            _headerFile.AppendLine("");
            _headerFile.AppendLine("");
        }

        public void AppendLineToHeaderInit(string l)
        {
            _headerInit.AppendLine(l);
        }

        public void AddClogModule(ICLogOutputModule m)
        {
            _modules.Add(m);
            m_unusedModules.Add(m);
        }

        public void FinishedProcessing()
        {
            foreach (var module in _modules)
            {
                if (m_unusedModules.Contains(module))
                {
                    continue;
                }

                module.FinishedProcessing(_headerFile, _sourceFile);
            }

            _modules.Clear();
        }
    }
}
