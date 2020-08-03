/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    This class implements the main configuration and setup for reading clog config files, your source code, side cars etc.

    By implementing the main() for clog, it's mostly connecting up other clases

--*/

using clog.TraceEmitterModules;
using clogutils;
using clogutils.ConfigFile;
using CommandLine;
using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace clog
{
    internal class clog
    {
        private static int PerformInstall(string outputDir)
        {
            if (File.Exists(outputDir))
            {
                CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, "Output file for install cannot be a file. It either must not exist, or be a directory");
                return -11;
            }
            Directory.CreateDirectory(outputDir);

            Assembly utilsAssembly = typeof(CLogConsoleTrace).Assembly;
            string baseName = utilsAssembly.GetName().Name;

            void ExtractFile(string name)
            {
                using Stream embeddedStream = utilsAssembly.GetManifestResourceStream($"{baseName}.{name}");
                using StreamReader reader = new StreamReader(embeddedStream);
                string contents = reader.ReadToEnd();
                string fileName = Path.Combine(outputDir, name);
                if (File.Exists(fileName))
                {
                    string existingContents = File.ReadAllText(fileName);
                    if (existingContents == contents)
                    {
                        CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Std, $"Skipping file {name} as its up to date");
                        return;
                    }
                }
                File.WriteAllText(fileName, contents);
                CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Std, $"Installed file {name}");
            }

            ExtractFile("clog.h");
            ExtractFile("CLog.cmake");

            CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Std, "--installDirectory overrides all arguments. Dependencies successfully installed!");

            return 0;
        }

        private static int Main(string[] args)
        {
            // Manually parse installDirectory, as it interferes with the required configFile
            // The argument still shows up in CommandLineArguments to be shown in help.
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--installDirectory")
                {
                    if (i + 1 == args.Length)
                    {
                        CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, "Must pass an argument to --installDirectory");
                        return -1;
                    }
                    return PerformInstall(args[i + 1]);
                }
            }

            ParserResult<CommandLineArguments> o = Parser.Default.ParseArguments<CommandLineArguments>(args);

            return o.MapResult(
                options =>
                {
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

                        if (!string.IsNullOrWhiteSpace(options.InstallDependencies))
                        {
                            CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, "Internal error, this should have been handled at a previous step");
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

                        if (options.Devmode  || !String.IsNullOrEmpty(Environment.GetEnvironmentVariable("CLOG_DEVELOPMENT_MODE")))
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
                        sidecar.ConfigFile = configFile;


                        if (options.RefreshCustomTypeProcessor)
                        {
                            sidecar.Save(options.SidecarFile);
                            return 0;
                        }

                        string outputCFile = Path.Combine(Path.GetDirectoryName(options.OutputFile),
                                                 options.ScopePrefix + "_" + Path.GetFileName(options.OutputFile)) + ".c";

                        configFile.ScopePrefix = options.ScopePrefix;
                        configFile.FilePath = Path.GetFullPath(options.ConfigurationFile);
                        configFile.OverwriteHashCollisions = options.OverwriteHashCollisions;

                        //Delete the output file; we want to encourage build breaks if something goes wrong
                        if (File.Exists(options.OutputFile))
                        {
                            File.Delete(options.OutputFile);
                        }

                        if (File.Exists(outputCFile))
                        {
                            File.Delete(outputCFile);
                        }


                        CLogFileProcessor processor = new CLogFileProcessor(configFile);
                        CLogFullyDecodedMacroEmitter fullyDecodedMacroEmitter = new CLogFullyDecodedMacroEmitter(options.InputFile, sidecar);

                        fullyDecodedMacroEmitter.AddClogModule(sidecar);

                        CLogTraceLoggingOutputModule traceLoggingEmitter = new CLogTraceLoggingOutputModule();
                        fullyDecodedMacroEmitter.AddClogModule(traceLoggingEmitter);

                        CLogDTraceOutputModule dtrace = new CLogDTraceOutputModule();
                        fullyDecodedMacroEmitter.AddClogModule(dtrace);

                        CLogSystemTapModule systemTap = new CLogSystemTapModule();
                        fullyDecodedMacroEmitter.AddClogModule(systemTap);

                        CLogSysLogModule syslog = new CLogSysLogModule();
                        fullyDecodedMacroEmitter.AddClogModule(syslog);

                        CLogManifestedETWOutputModule manifestedEtwOutput = new CLogManifestedETWOutputModule(options.ReadOnly);
                        fullyDecodedMacroEmitter.AddClogModule(manifestedEtwOutput);

                        CLogLTTNGOutputModule lttngOutput = new CLogLTTNGOutputModule(options.InputFile, options.OutputFile,
                            options.OutputFile + ".lttng.h", options.DynamicTracepointProvider);
                        fullyDecodedMacroEmitter.AddClogModule(lttngOutput);

                        if(!File.Exists(options.InputFile))
                        {
                            CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Std, $"Invalid Input File : {Path.GetFileName(options.InputFile)}");
                            throw new CLogEnterReadOnlyModeException("InvalidInputFile", CLogHandledException.ExceptionType.InvalidInputFile, null);       
                        }

                        string content = File.ReadAllText(options.InputFile);
                        string output = processor.ConvertFile(configFile, fullyDecodedMacroEmitter, content, options.InputFile, false);

                        if (!content.Contains(Path.GetFileName(options.OutputFile)))
                        {
                            CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Std, $"You must #include the clog output file {Path.GetFileName(options.OutputFile)}");
                            throw new CLogEnterReadOnlyModeException("MustIncludeCLogHeader", CLogHandledException.ExceptionType.SourceMustIncludeCLOGHeader, null);
                        }

                        fullyDecodedMacroEmitter.FinishedProcessing();

                        StringBuilder clogFile = new StringBuilder();
                        clogFile.AppendLine("#include <clog.h>");

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


                        if (!Directory.Exists(Path.GetDirectoryName(options.OutputFile)))
                        {
                            Console.WriteLine("Creating Directory for Output : " + Path.GetDirectoryName(options.OutputFile));
                            Directory.CreateDirectory(Path.GetDirectoryName(options.OutputFile));
                        }

                        if (sidecar.AreDirty || configFile.AreWeDirty())
                        {
                            if (options.ReadOnly)
                            {
                                if(sidecar.AreDirty)
                                    Console.WriteLine("Sidecar is dirty");
                                if(configFile.AreWeDirty())
                                    Console.WriteLine("ConfigFile is dirty");

                                sidecar.PrintDirtyReasons();
                                throw new CLogEnterReadOnlyModeException("WontWriteWhileInReadonlyMode:SideCar", CLogHandledException.ExceptionType.WontWriteInReadOnlyMode, null);
                            }
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

                        File.WriteAllText(options.OutputFile, clogFile.ToString());
                        File.WriteAllText(outputCFile, fullyDecodedMacroEmitter.SourceFile);
                    }
                    catch (CLogHandledException e)
                    {
                        e.PrintDiagnostics();
                        return -2;
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
