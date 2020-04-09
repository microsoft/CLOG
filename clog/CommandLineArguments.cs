/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    Contains commandline arguments and definations for clog2text_lttng

--*/

using clogutils;
using clogutils.ConfigFile;
using CommandLine;

namespace clog
{
    public class CommandLineArguments
    {
        [Option('i', "inputFile", SetName = "build", Required = false, HelpText = "Full path to one WPP source file for conversion")]
        public string InputFile
        {
            get;
            set;
        }

        [Option('o', "outputFile", SetName = "build", Required = false, HelpText = "Full path to output file")]
        public string OutputFile
        {
            get;
            set;
        }

        [Option('p', "configProfile", Required = true, HelpText = "Configuration profile name")]
        public string ConfigurationProfile
        {
            get;
            set;
        }

        [Option("scopePrefix", SetName = "build", Required = false, HelpText = "scope prefix")]
        public string ScopePrefix
        {
            get;
            set;
        }

        [Option('s', "sideCar", SetName = "build", Required = false, HelpText = "Full path to sidecar")]
        public string SidecarFile
        {
            get;
            set;
        }

        [Option("overwriteHashCollisions", SetName = "build", Required = false, Default = false, HelpText = "CAUTION: overwrite trace signatures should a collsion occur.  please read documentation before using this (you may also set CLOG_OVERWRITE_COLLISIONS in your environemnt)")]
        public bool OverwriteHashCollisions
        {
            get;
            set;
        }

        [Option("lintConfig", SetName = "lint", Required = false, Default = false, HelpText = "CAUTION: Run lint operation on config file")]
        public bool LintConfig
        {
            get;
            set;
        }        

        [Option('c', "configFile", Required = true, HelpText = "Full path to clog configuration file")]
        public string ConfigurationFile
        {
            get;
            set;
        }            

        [Option("upgradeConfigFile", Required = false, Default = false, HelpText = "[OPTIONAL] Upgrade (by overwriting) the input config file to be of the latest version")]
        public bool UpgradeConfigFile
        {
            get;
            set;
        }


        public bool IsValid()
        {
            //
            // If either input or output is empty, require that we're linting or upgrading
            //
            if (string.IsNullOrEmpty(this.InputFile) || string.IsNullOrEmpty(this.OutputFile))
            {
                if (!LintConfig && !UpgradeConfigFile)
                {
                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, "input file and output file are required if not linting or upgrading config file");
                    return false;
                }
            }
            else
            {
                if(string.IsNullOrEmpty(this.InputFile) || string.IsNullOrEmpty(this.OutputFile))
                {
                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, "please specify both an input and and output file");
                    return false;
                }

                if (string.IsNullOrEmpty(ScopePrefix))
                {
                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, "please specify scope prefix");
                    return false;
                }

                if (string.IsNullOrEmpty(SidecarFile))
                {
                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, "please specify sidecar file");
                    return false;
                }
            }

            if (LintConfig || UpgradeConfigFile)
            {
                if (!string.IsNullOrEmpty(this.InputFile) || !string.IsNullOrEmpty(this.OutputFile))
                {
                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, "do not specify input or output files if you're linting or upgrading the config file");
                    return false;
                }
            }

            return true;
        }
    }
}