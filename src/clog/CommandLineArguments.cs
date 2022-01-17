/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    Contains commandline arguments and definations for clog2text_lttng

--*/

using System.Collections.Generic;
using System.IO;
using System.Linq;
using clogutils;
using CommandLine;

namespace clog
{
    public class CommandLineArguments
    {

        [Option("inputFiles", SetName = "build", Required = false, HelpText = "Full path to one (or more) source file for CLOG to generate logging stubs")]
        public IEnumerable<string> InputFiles
        {
            get;
            set;
        }

        [Option("outputDirectory", SetName = "build", Required = false, HelpText = "If you'd prefer to specify an output directory over an outputfile, CLOG will output a file into this directory with the postfix .clog.h")]
        public string OutputDirectory
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

        [Option('p', "configProfile", HelpText = "Configuration profile name;  this value selects configuration from the specified configuration file")]
        public string ConfigurationProfile
        {
            get;
            set;
        }

        [Option('r', "readOnly", HelpText = "Put CLOG in readonly mode - use this in a build system to prevent manifest or sidecar modifications.  You can set CLOG_FORCE_WRITABLE environment if you're in a development mode")]
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

        [Option("developerMode", SetName = "build", Required = false, Default = false, HelpText = "Developer Mode")]
        public bool Devmode
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

        [Option('d', "dynamicTracepointProvider", Required = false, Default = false, HelpText = "[OPTIONAL] Set to use a dynamic tracepoint provider rather then static (LTTng specific)")]
        public bool DynamicTracepointProvider
        {
            get;
            set;
        }

        [Option("verboseErrors", Required = false, Default = false, HelpText = "[OPTIONAL] Set this to see exceptions information, should you encouter a bug in CLOG")]
        public bool VerboseErrors
        {
            get;
            set;
        }

        public string GetOutputFileName(string inputFile)
        {
            string ret = Path.Combine(this.OutputDirectory, Path.GetFileName(inputFile));
            ret += ".clog.h";
            return ret;
        }

        public bool IsValid()
        {
            if (!string.IsNullOrEmpty(this.OutputDirectory))
            {
                if (0 == this.InputFiles.Count())
                {
                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, "OutputDirectory specified, and InputFile is empty");
                    return false;
                }
            }

            //
            // If either input or output is empty, require that we're linting or upgrading
            //
            if (0 == this.InputFiles.Count())
            {
                if (!LintConfig && !UpgradeConfigFile && !RefreshCustomTypeProcessor)
                {
                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, "input file and output file are required if not linting or upgrading config file");
                    return false;
                }
            }
            else
            {
                if (0 == this.InputFiles.Count() || string.IsNullOrEmpty(this.OutputDirectory))
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
                if (0 != this.InputFiles.Count() || !string.IsNullOrEmpty(this.OutputDirectory))
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
                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, $"Please verify the sidecar, configuration file, and profile are all correct");
                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, $"    SideCar: {this.SidecarFile}");
                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, $"    Configuration File: {this.ConfigurationFile}");
                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, $"    Configuration Profile: {this.ConfigurationProfile}");
                    return false;
                }
            }

            return true;
        }
    }
}
