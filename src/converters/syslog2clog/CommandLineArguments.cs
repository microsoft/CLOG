/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    Contains commandline arguments and definations for syslog2clog

--*/

using clogutils;
using CommandLine;

namespace syslog2clog
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

        [Option("refreshCustomTypeProcessor", SetName = "debug", Required = false, HelpText = "[DEBUGGING] update the C# custom type processor for the specified sidecar ")]
        public bool RefreshCustomTypeProcessor
        {
            get;
            set;
        }

        [Option('p', "configProfile", HelpText = "Configuration profile name")]
        public string ConfigurationProfile
        {
            get;
            set;
        }

        [Option('r', "readOnly", HelpText = "Put CLOG in readonly mode - use this in a build system to prevent manifest or sidecar modifications.  You can set CLOG_FORCE_WRITABLE (or the more broad CLOG_DEVELOPMENT_MODE) environment if you're in a development mode")]
        public bool ReadOnly
        {
            get;
            set;
        }

        [Option("scopePrefix", SetName = "build", Required = false, HelpText = "scope prefix;  this value will prefix CLOG functions() and helps to provide scope.  Typically this value is reused by an entire module or directory (your choice)")]
        public string ScopePrefix
        {
            get;
            set;
        }

        [Option('s', "sideCar", Required = false, HelpText = "Full path to sidecar")]
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
                if (!LintConfig && !UpgradeConfigFile && !RefreshCustomTypeProcessor)
                {
                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, "input file and output file are required if not linting or upgrading config file");
                    return false;
                }
            }
            else
            {
                if (string.IsNullOrEmpty(this.InputFile) || string.IsNullOrEmpty(this.OutputFile))
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

            if (RefreshCustomTypeProcessor)
            {
                if (string.IsNullOrEmpty(this.SidecarFile) || string.IsNullOrEmpty(this.ConfigurationFile))
                {
                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, "Please specify both the side car to update, and the configuration file that contains a reference to the new type processor");
                    return false;
                }
            }

            //
            // Makesure ConfigurationProfile is specified for all but those who do not need it
            //
            if (!RefreshCustomTypeProcessor)
            {
                if (string.IsNullOrEmpty(this.ConfigurationProfile))
                {
                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, "Please specify both the side car to update, and the configuration file that contains a reference to the new type processor");
                    return false;
                }
            }

            return true;
        }
    }
}
