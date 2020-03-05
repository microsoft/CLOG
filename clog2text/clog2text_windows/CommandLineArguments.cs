/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    A stream manages the send and receive queues for application data. This file
    contains the initialization and cleanup functionality for the stream.

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
