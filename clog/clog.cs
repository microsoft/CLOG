/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    This class implements the main configuration and setup for reading clog config files, your source code, side cars etc.
    
    By implementing the main() for clog, it's mostly connecting up other clases

--*/

using System;
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
                        
                        if(options.LintConfig)
                        {
                            if(!configFile.MarkPhase)
                            {
                                CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, "Lint operation only works on config files placed into the 'MarkPhase'.  This can be a destricutive action, please read the docs for more information");
                                return -10;
                            }
                            configFile.Lint();
                            configFile.Save();
                            CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Std, "Lint operation complete");
                            return 0;
                        }

                        if (options.UpgradeConfigFile)
                        {
                            configFile.UpdateVersion();
                            configFile.Save();
                            CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Std, "Config upgrade complete");
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


                        CLogSidecar sidecar;
                        
                        if(!Directory.Exists(Path.GetDirectoryName(options.SidecarFile)))
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

                        CLogFileProcessor processor = new CLogFileProcessor(configFile);
                        CLogFullyDecodedMacroEmitter fullyDecodedMacroEmitter = new CLogFullyDecodedMacroEmitter(options.InputFile, sidecar);

                        fullyDecodedMacroEmitter.AddClogModule(sidecar);
                        
                        CLogTraceLoggingOutputModule traceLoggingEmitter = new CLogTraceLoggingOutputModule();
                        fullyDecodedMacroEmitter.AddClogModule(traceLoggingEmitter);

                        CLogDTRaceOutputModule dtrace = new CLogDTRaceOutputModule();
                        fullyDecodedMacroEmitter.AddClogModule(dtrace);

                        CLogManifestedETWOutputModule manifestedEtwOutput = new CLogManifestedETWOutputModule();
                        fullyDecodedMacroEmitter.AddClogModule(manifestedEtwOutput);

                        CLogLTTNGOutputModule lttngOutput = new CLogLTTNGOutputModule(options.InputFile, options.OutputFile,
                            options.OutputFile + ".lttng.h");
                        fullyDecodedMacroEmitter.AddClogModule(lttngOutput);

                        string content = File.ReadAllText(options.InputFile);
                        string output = processor.ConvertFile(configFile,fullyDecodedMacroEmitter, content, options.InputFile);

                        if (!content.Contains(Path.GetFileName(options.OutputFile)))
                        {
                            CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Std, $"You must #include the clog output file {Path.GetFileName(options.OutputFile)}");
                            throw new CLogEnterReadOnlyModeException("MustIncludeCLogHeader", null);
                        }

                        sidecar.TypeEncoder.Merge(configFile.InUseTypeEncoders);


                        sidecar.CustomTypeProcessorsX[Path.GetFileName(configFile.FilePath)] = configFile.TypeEncoders.CustomTypeDecoder;
                        foreach (var c in configFile._chainedConfigFiles)
                        {
                            sidecar.CustomTypeProcessorsX[Path.GetFileName(c.FilePath)] = c.TypeEncoders.CustomTypeDecoder;
                        }


                        sidecar.SaveOnFinish(options.SidecarFile);
                        fullyDecodedMacroEmitter.FinishedProcessing();

                        StringBuilder clogFile = new StringBuilder();
                        clogFile.AppendLine("#include \"clog.h\"");

                        clogFile.AppendLine("#ifdef __cplusplus");
                        clogFile.AppendLine("extern \"C\" {");                        
                        clogFile.AppendLine("#endif");
                      
                        clogFile.Append(fullyDecodedMacroEmitter.HeaderInit);

                        foreach (var macro in processor.MacrosInUse)
                        {
                            clogFile.AppendLine($"#ifndef CLOG_MACRO_{macro.MacroName}");
                            clogFile.AppendLine($"#define CLOG_MACRO_{macro.MacroName}  1");
                            clogFile.AppendLine(
                                $"#define {macro.MacroName}(a, ...) CLOG_CAT(CLOG_ARGN_SELECTOR(__VA_ARGS__), CLOG_CAT(_,a(#a, __VA_ARGS__)))");
                            clogFile.AppendLine("#endif");
                        }

                        clogFile.Append(fullyDecodedMacroEmitter.HeaderFile);
                        clogFile.AppendLine("#ifdef __cplusplus");
                        clogFile.AppendLine("}");
                        clogFile.AppendLine("#endif");
                        
                        if(!Directory.Exists(Path.GetDirectoryName(options.OutputFile)))
                            Directory.CreateDirectory(Path.GetDirectoryName(options.OutputFile));

                        File.WriteAllText(options.OutputFile, clogFile.ToString());
                        File.WriteAllText(outputCFile, fullyDecodedMacroEmitter.SourceFile);

                        if (configFile.AreWeDirty() || configFile.AreWeInMarkPhase())
                        {
                            Console.WriteLine("Configuration file was updated, saving...");
                            Console.WriteLine($"    {configFile.FilePath}");
                            configFile.Save();
                        }
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
