﻿using CommandLine;
using System;
using clogutils;
using clogutils.ConfigFile;
using System.IO;
using System.Text;
using clogutils.MacroDefinations;
using static clogutils.CLogFileProcessor;
using System.Collections.Generic;

namespace syslog2clog
{
    public class SysLogToClog : ICLogFullyDecodedLineCallbackInterface
    {
        private bool skip;
        public void TraceLineDiscovered(CLogDecodedTraceLine decodedTraceLine, StringBuilder results)
        {
            Dictionary<int, string> map = new Dictionary<int, string>();
            int idx = 1;

            if(skip)
            {
                results.Append(decodedTraceLine.match.MatchedRegEx.ToString());
                return;
            }

            CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Std, decodedTraceLine.match.MatchedRegEx.ToString());
            int c = -1;
            try
            {
                for (; ; )
                {
                    foreach (var m in decodedTraceLine.configFile.AllKnownMacros())
                    {
                        map[idx] = m.MacroName;
                        CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Std, $"{idx}. {m.MacroName}");
                        ++idx;
                    }

                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Std, $"{idx}. <skip the reset in this file and save");

                    string choice = Console.ReadLine();
                    c = Convert.ToInt32(choice);

                    if (c == idx)
                    {
                        skip = true;
                        results.Append(decodedTraceLine.match.MatchedRegEx.ToString());
                        return;
                    }

                    break;
                }
            }
            catch (Exception)
            {
                CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, "ERROR : invalid input");
            }


            CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Std, "UNIQUE ID");
            string id = Console.ReadLine().Trim().ToUpper();

            results.Append($"{map[c]}(");
            results.Append("" + id);
            results.Append($", \"{decodedTraceLine.TraceString}\"");

            foreach(var arg in decodedTraceLine.splitArgs)
            {
                results.Append($", {arg.VariableInfo.UserSpecifiedUnModified}");
            }
            results.Append(");");
        }
    }

    class syslog2clog
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


                        string outputCFile = Path.Combine(Path.GetDirectoryName(options.OutputFile),
                                                 options.ScopePrefix + "_" + Path.GetFileName(options.OutputFile)) + ".c";

                        configFile.ScopePrefix = options.ScopePrefix;
                        configFile.FilePath = Path.GetFullPath(options.ConfigurationFile);
                        configFile.OverwriteHashCollisions = options.OverwriteHashCollisions;

                        CLogTraceMacroDefination syslog = new CLogTraceMacroDefination();
                        syslog.EncodedArgNumber = 1;
                        syslog.MacroName = "syslog";
                        syslog.MacroConfiguration = new System.Collections.Generic.Dictionary<string, string>();
                        syslog.MacroConfiguration[options.ConfigurationProfile] = options.ConfigurationProfile;

                        configFile.SourceCodeMacros.Add(syslog);


                        CLogFileProcessor processor = new CLogFileProcessor(configFile);
                        SysLogToClog converter = new SysLogToClog();

                      //  CLogFullyDecodedMacroEmitter fullyDecodedMacroEmitter = new CLogFullyDecodedMacroEmitter(options.InputFile, sidecar);

                       // fullyDecodedMacroEmitter.AddClogModule(converter);

                        string content = File.ReadAllText(options.InputFile);
                        string output = processor.ConvertFile(configFile, converter, content, options.InputFile, true);

                       // fullyDecodedMacroEmitter.FinishedProcessing();
                                               
                        if (!Directory.Exists(Path.GetDirectoryName(options.OutputFile)))
                            Directory.CreateDirectory(Path.GetDirectoryName(options.OutputFile));

                        
                        File.WriteAllText(options.InputFile, output);
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
