using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using clogutils;
using clogutils.ConfigFile;
using clogutils.MacroDefinations;
using CommandLine;
using static clogutils.CLogFileProcessor;

namespace syslog2clog
{
    public class SysLogToClog : ICLogFullyDecodedLineCallbackInterface
    {
        private bool skip;
        private HashSet<string> takenIds = new HashSet<string>();

        public void TraceLineDiscovered(CLogDecodedTraceLine decodedTraceLine, CLogOutputInfo outputInfo, StringBuilder results)
        {
            Dictionary<int, string> map = new Dictionary<int, string>();
            int idx = 1;

            if (skip)
            {
                results.Append(decodedTraceLine.match.MatchedRegExX.ToString());
                return;
            }

            CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Std, decodedTraceLine.match.MatchedRegExX.ToString());
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

                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Std, $"{idx}. <skip the rest in this file and save>");

                    string choice = null;
                    while (String.IsNullOrEmpty(choice))
                    {
                        try
                        {
                            choice = Console.ReadLine();
                            c = Convert.ToInt32(choice);
                        }
                        catch (Exception)
                        {
                            Console.WriteLine("try again please");
                        }
                    }

                    if (c == idx)
                    {
                        skip = true;
                        results.Append(decodedTraceLine.match.MatchedRegExX.ToString());
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

            while (takenIds.Contains(id))
            {
                Console.WriteLine("ID is taken please use a unique ID");
                id = Console.ReadLine().Trim().ToUpper();
            }
            takenIds.Add(id);

            results.Append($"{map[c]}(");
            results.Append("" + id);
            results.Append($", \"{decodedTraceLine.TraceString}\"");

            for (int i = 2; i < decodedTraceLine.splitArgs.Length; ++i)
            {
                var arg = decodedTraceLine.splitArgs[i];
                results.Append($", {arg.VariableInfo.UserSuppliedTrimmed}");
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
                        sidecar.SetConfigFile(configFile);


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
                                             
                        string content = File.ReadAllText(options.InputFile);
                        string output = processor.ConvertFile(configFile, null, converter, content, options.InputFile, true);

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
