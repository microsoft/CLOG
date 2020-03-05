/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    Contains commandline arguments and definations for clog2text_windows

--*/

using CommandLine;

namespace clog2text_windows
{
    public class CommandLineArguments
    {
        [Option('i', "input etl file", Required = false, HelpText = "Full path to ETL output")]
        public string ETLFile { get; set; }

        [Option('s', "sidecar", Required = true, HelpText = "Full path to clog sidecar")]
        public string SideCarFile { get; set; }
    }
}
