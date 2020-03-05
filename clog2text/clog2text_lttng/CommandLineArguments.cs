/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    A stream manages the send and receive queues for application data. This file
    contains the initialization and cleanup functionality for the stream.

--*/
using CommandLine;

namespace clog2text_lttng
{
    public class CommandLineArguments
    {
        [Option('i', "input babeltrace output", Required = false, HelpText = "Full path to babeltrace output")]
        public string BabelTrace
        {
            get;
            set;
        }

        [Option('s', "sidecar", Required = true, HelpText = "Full path to clog sidecar")]
        public string SideCarFile
        {
            get;
            set;
        }
    }
}
