/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    Contains commandline arguments and definations for clog2text_lttng

--*/

using CommandLine;

namespace clog
{
    public class CommandLineArguments
    {
        [Option('i', "inputFile", Required = true, HelpText = "Full path to one WPP source file for conversion")]
        public string InputFile
        {
            get;
            set;
        }

        [Option('o', "outputFile", Required = true, HelpText = "Full path to output file")]
        public string OutputFile
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

        [Option('s', "sideCar", Required = true, HelpText = "Full path to sidecar")]
        public string SidecarFile
        {
            get;
            set;
        }

        [Option("scopePrefix", Required = true, HelpText = "scope prefix")]
        public string ScopePrefix
        {
            get;
            set;
        }

        [Option("overwriteHashCollsions", Required = false, Default = false, HelpText = "CAUTION: overwrite trace signatures should a collsion occur.  please read documentation before using this")]
        public bool OverwriteHashCollisions
        {
            get;
            set;
        }

        [Option("upgradeConfigFile", Required = false, Default = false,
                HelpText = "[OPTIONAL] Upgrade (by overwriting) the input config file to be of the latest version")]
        public bool UpgradeConfigFile
        {
            get;
            set;
        }
    }
}
