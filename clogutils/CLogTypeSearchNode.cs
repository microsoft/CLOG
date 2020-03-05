/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    A stream manages the send and receive queues for application data. This file
    contains the initialization and cleanup functionality for the stream.

--*/

using System.Collections.Generic;

namespace clogutils
{
    public class CLogTypeSearchNode
    {
        public Dictionary<char, CLogTypeSearchNode> Nodes = new Dictionary<char, CLogTypeSearchNode>();

        public CLogEncodingCLogTypeSearch UserNode { get; set; }
    }
}
