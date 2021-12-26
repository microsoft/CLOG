/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    This class implements the main configuration and setup for reading clog config files, your source code, side cars etc.

    By implementing the main() for clog, it's mostly connecting up other clases

--*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using clog.TraceEmitterModules;
using clogutils;
using clogutils.ConfigFile;
using CommandLine;

namespace clog
{
    internal class clog
    {
        private static int Main(string[] args)
        {
            ParserResult<CommandLineArguments> o = Parser.Default.ParseArguments<CommandLineArguments>(args);

            return o.MapResult(
                options =>
                {
                    string currentFile = null;
                    try
                    {
                        //
                        // The CommandLineArguments library validates most input arguments for us,  there are few ones that are complicated
                        //    this secondary check looks for those and errors out if present
                        //
                        if (!options.IsValid())
                        {
                            CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, "Invalid args");
                            return -1;
                        }


                        CLogConfigurationFile configFile = CLogConfigurationFile.FromFile(options.ConfigurationFile);
                        configFile.ProfileName = options.ConfigurationProfile;

                        if (options.ReadOnly && !String.IsNullOrEmpty(Environment.GetEnvironmentVariable("CLOG_FORCE_WRITABLE")))
                        {
                            options.ReadOnly = false;
                            CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Wrn, "WARNING: CLOG was instructed via --readOnly not to emit changes however this was overridden with CLOG_FORCE_WRITABLE environment variable");
                        }

                        if (options.OverwriteHashCollisions || !String.IsNullOrEmpty(Environment.GetEnvironmentVariable("CLOG_OVERWRITE_COLLISIONS")))
                        {
                            options.OverwriteHashCollisions = true;
                            options.ReadOnly = false;
                            CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, "");
                            CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, "");
                            CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, "");
                            CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, "***********************************************");
                            CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, "");
                            CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, "Overwriting of sidecare collisions is set by environment variable CLOG_OVERWRITE_COLLISIONS.  This setting is only to be used while making large refactors and should not be included in build environments or standard development environments");
                            CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, "");
                            CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, "***********************************************");
                            CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, "");
                            CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, "");
                            CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, "");
                        }

                        if (options.Devmode || !String.IsNullOrEmpty(Environment.GetEnvironmentVariable("CLOG_DEVELOPMENT_MODE")))
                        {
                            options.ReadOnly = false;
                            options.OverwriteHashCollisions = true;
                            configFile.DeveloperMode = true;
                            CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Wrn, "WARNING: CLOG was instructed to enter a developer mode");
                        }

                        if (options.LintConfig)
                        {
                            if (!configFile.MarkPhase)
                            {
                                CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, "Lint operation only works on config files placed into the 'MarkPhase'.  This can be a destricutive action, please read the docs for more information");
                                return -10;
                            }
                            configFile.Lint();
                            configFile.Save(false);
                            CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Std, "Lint operation complete");
                            return 0;
                        }

                        if (options.UpgradeConfigFile)
                        {
                            configFile.UpdateVersion();
                            configFile.Save(false);
                            CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Std, "Config upgrade complete");
                            return 0;
                        }

                        CLogSidecar sidecar;
                        if (!Directory.Exists(Path.GetDirectoryName(options.SidecarFile)))
                            Directory.CreateDirectory(Path.GetDirectoryName(options.SidecarFile));

                        if (!File.Exists(options.SidecarFile))
                        {
                            sidecar = new CLogSidecar();
                        }
                        else
                        {
                            string json = File.ReadAllText(options.SidecarFile);
                            sidecar = CLogSidecar.FromJson(json);
                            if (null == sidecar)
                                sidecar = new CLogSidecar();
                        }
                        sidecar.SetConfigFile(configFile);


                        if (options.RefreshCustomTypeProcessor)
                        {
                            configFile.ForceDecoderCompile();
                            sidecar.Save(options.SidecarFile);
                            return 0;
                        }

                        //
                        // BUGBUG: refactoring needed for handling batches of input files, expecially
                        //    now that managed languages are coming along.  Leaving this unaddressed
                        //    for now while the best way is still elusive
                        //
                        List<ICLogBatchingModule> batchingModules = new List<ICLogBatchingModule>();

                        Console.WriteLine("Number of files : " + (new List<string>(options.InputFiles)).Count);

                        foreach (string inputFile in options.InputFiles)
                        {
                            Console.WriteLine("Processing: " + inputFile);
                            currentFile = inputFile;

                            string outputFile = options.GetOutputFileName(inputFile);
                            string outputCFile = Path.Combine(Path.GetDirectoryName(outputFile), options.ScopePrefix + "_" + Path.GetFileName(outputFile)) + ".c";

                            configFile.ScopePrefix = options.ScopePrefix;
                            configFile.FilePath = Path.GetFullPath(options.ConfigurationFile);
                            configFile.OverwriteHashCollisions = options.OverwriteHashCollisions;

                            //Delete the output file; we want to encourage build breaks if something goes wrong
                            if (File.Exists(outputFile))
                            {
                                File.Delete(outputFile);
                            }

                            if (File.Exists(outputCFile))
                            {
                                File.Delete(outputCFile);
                            }

                            CLogFileProcessor processor = new CLogFileProcessor(configFile);
                            CLogFullyDecodedMacroEmitter fullyDecodedMacroEmitter = new CLogFullyDecodedMacroEmitter(inputFile, sidecar);

                            fullyDecodedMacroEmitter.AddClogModule(sidecar);

                            CLogTraceLoggingOutputModule traceLoggingEmitter = new CLogTraceLoggingOutputModule();
                            fullyDecodedMacroEmitter.AddClogModule(traceLoggingEmitter);

                            CLogDTraceOutputModule dtrace = new CLogDTraceOutputModule();
                            fullyDecodedMacroEmitter.AddClogModule(dtrace);


                            CLogSysLogModule syslog = new CLogSysLogModule();
                            fullyDecodedMacroEmitter.AddClogModule(syslog);

                            CLogSTDOUT stdout = new CLogSTDOUT();
                            fullyDecodedMacroEmitter.AddClogModule(stdout);

                            CLogManifestedETWOutputModule manifestedEtwOutput = new CLogManifestedETWOutputModule(options.ReadOnly);
                            fullyDecodedMacroEmitter.AddClogModule(manifestedEtwOutput);

                            CLogLTTNGOutputModule lttngOutput = new CLogLTTNGOutputModule(inputFile, outputFile, outputFile + ".lttng.h", options.DynamicTracepointProvider);
                            fullyDecodedMacroEmitter.AddClogModule(lttngOutput);

                            if (!File.Exists(inputFile))
                            {
                                CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Std, $"Invalid Input File (file doesnt exist) : {inputFile}");
                                throw new CLogEnterReadOnlyModeException("InvalidInputFile", CLogHandledException.ExceptionType.InvalidInputFile, null);
                            }

                            CLogOutputInfo outputInfo = new CLogOutputInfo();
                            outputInfo.OutputDirectory = options.OutputDirectory;
                            outputInfo.OutputFileName = outputFile;
                            outputInfo.InputFileName = inputFile;

                            string content = File.ReadAllText(inputFile);
                            string output = processor.ConvertFile(configFile, outputInfo, fullyDecodedMacroEmitter, content, inputFile, false);

                            // TODO: BUGBUG - Java won't ever do this
                            //if (!content.Contains(Path.GetFileName(outputFile)))
                            //{
                            //    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Std, $"You must #include the clog output file {Path.GetFileName(outputFile)}");
                            //    throw new CLogEnterReadOnlyModeException("MustIncludeCLogHeader", CLogHandledException.ExceptionType.SourceMustIncludeCLOGHeader, null);
                            //}

                            fullyDecodedMacroEmitter.FinishedProcessing(outputInfo);

                            StringBuilder clogFile = new StringBuilder();

                            clogFile.AppendLine($"#ifndef CLOG_DO_NOT_INCLUDE_HEADER");
                            clogFile.AppendLine("#include <clog.h>");
                            clogFile.AppendLine($"#endif");


                            clogFile.Append(fullyDecodedMacroEmitter.HeaderInit);

                            foreach (var macro in processor.MacrosInUse)
                            {
                                clogFile.AppendLine($"#ifndef _clog_MACRO_{macro.MacroName}");
                                clogFile.AppendLine($"#define _clog_MACRO_{macro.MacroName}  1");
                                clogFile.AppendLine(
                                    $"#define {macro.MacroName}(a, ...) _clog_CAT(_clog_ARGN_SELECTOR(__VA_ARGS__), _clog_CAT(_,a(#a, __VA_ARGS__)))");
                                clogFile.AppendLine("#endif");
                            }

                            clogFile.AppendLine("#ifdef __cplusplus");
                            clogFile.AppendLine("extern \"C\" {");
                            clogFile.AppendLine("#endif");

                            clogFile.Append(fullyDecodedMacroEmitter.HeaderFile);
                            clogFile.AppendLine("#ifdef __cplusplus");
                            clogFile.AppendLine("}");
                            clogFile.AppendLine("#endif");

                            clogFile.AppendLine("#ifdef CLOG_INLINE_IMPLEMENTATION");
                            clogFile.AppendLine("#include \"" + Path.GetFileName(outputCFile) + "\"");
                            clogFile.AppendLine("#endif");

                            //
                            // BUGBUG : the intent in saving needs to be
                            //    1. delete any output files early - before we start, getting us into a safe state (build breaks)
                            //    2. save sidecars and other items that make assumptions about the emitted code
                            //    3. save emitted code
                            //
                            // the goal of these orderings is to be safe in the event of failure/crash
                            //
                            if (!Directory.Exists(Path.GetDirectoryName(outputFile)))
                            {
                                Console.WriteLine("Creating Directory for Output : " + Path.GetDirectoryName(outputFile));
                                Directory.CreateDirectory(Path.GetDirectoryName(outputFile));
                            }

                            if (sidecar.AreDirty || configFile.AreWeDirty())
                            {
                                if (options.ReadOnly)
                                {
                                    if (sidecar.AreDirty)
                                        Console.WriteLine("Sidecar is dirty");
                                    if (configFile.AreWeDirty())
                                        Console.WriteLine("ConfigFile is dirty");

                                    sidecar.PrintDirtyReasons();
                                    throw new CLogEnterReadOnlyModeException("WontWriteWhileInReadonlyMode:SideCar", CLogHandledException.ExceptionType.WontWriteInReadOnlyMode, null);
                                }
                                configFile.ForceDecoderCompile();
                                sidecar.Save(options.SidecarFile);
                            }

                            if (configFile.AreWeDirty() || configFile.AreWeInMarkPhase())
                            {
                                if (options.ReadOnly)
                                {
                                    throw new CLogEnterReadOnlyModeException("WontWriteWhileInReadonlyMode:ConfigFile", CLogHandledException.ExceptionType.WontWriteInReadOnlyMode, null);
                                }
                                Console.WriteLine("Configuration file was updated, saving...");
                                Console.WriteLine($"    {configFile.FilePath}");
                                configFile.Save(false);
                            }

                            File.WriteAllText(outputFile, clogFile.ToString());
                            File.WriteAllText(outputCFile, fullyDecodedMacroEmitter.SourceFile);
                        }
                        currentFile = null;

                        //
                        // Enumerate batching modules, allowing them to save
                        //
                        foreach (var m in batchingModules)
                        {
                            CLogOutputInfo outputInfo = new CLogOutputInfo();
                            outputInfo.OutputDirectory = options.OutputDirectory;
                            m.FinishedBatch(outputInfo);
                        }

                    }
                    catch (CLogHandledException e)
                    {
                        if (null != currentFile)
                            CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, $"Failure in file : {currentFile}");
                        e.PrintDiagnostics(options.VerboseErrors);
                        return -2;
                    }
                    catch (Exception e)
                    {
                        CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, $"---------------------------------------------------------------------------");
                        CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, $"CLOG has crashed processing : {currentFile}.");
                        CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, $"   if you're reading this, we consider seeing this message a bug.  Even if the message");
                        CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, $"   you're about to read is sufficient to diagnose, we'd like to improve the user experience");
                        CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, $"   please consider filing a bug, with repro files and --verboseErrors");
                        CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, $"");
                        CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, $"");
                        CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, $"Exception:");

                        if (!options.VerboseErrors)
                        {
                            CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, "    " + e.Message);
                            return -1; 
                        }
                        else
                        {
                            CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, e.ToString());
                        }

                    }

                    return 0;
                }, err =>
                {
                    Console.WriteLine("Bad Args : " + err);
                    return -1;
                });
        }
    }
}
