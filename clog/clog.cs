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
                        CLogConfigurationFile configFile = CLogConfigurationFile.FromFile(options.ConfigurationFile);

                        string outputCFile = Path.Combine(Path.GetDirectoryName(options.OutputFile),
                                                 options.ScopePrefix + "_" + Path.GetFileName(options.OutputFile)) + ".c";

                        if (options.UpgradeConfigFile)
                        {
                            configFile.UpdateAndSave();
                        }

                        configFile.BUGBUG_String = options.ScopePrefix;
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

                        CLogMTTNGOuputModule lttngOutput = new CLogMTTNGOuputModule(options.InputFile, options.OutputFile,
                            options.OutputFile + ".lttng.h");
                        fullyDecodedMacroEmitter.AddClogModule(lttngOutput);

                        string content = File.ReadAllText(options.InputFile);
                        string output = processor.ConvertFile(configFile, fullyDecodedMacroEmitter, content);

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
                        File.WriteAllText(options.OutputFile, clogFile.ToString());
                        File.WriteAllText(outputCFile, fullyDecodedMacroEmitter.SourceFile);


                        if (configFile.IsDirty)
                        {
                            Console.WriteLine("Configuration file was updated, saving...");
                            Console.WriteLine($"    {configFile.FilePath}");

                            File.WriteAllText(options.ConfigurationFile, configFile.ToJson());
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
