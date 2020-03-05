/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    A stream manages the send and receive queues for application data. This file
    contains the initialization and cleanup functionality for the stream.

--*/

using System;
using System.Text.RegularExpressions;

namespace clogutils
{
    public class CLogTypeNotFoundException : CLogHandledException
    {
        public static bool AreReadOnly;

        public CLogTypeNotFoundException(string msg, string partialType, Match traceLine, bool silent = false,
            Exception e = null) : base(msg, traceLine, silent, e)
        {
            AreReadOnly = true;
            Exception = e;
            PartialType = partialType;
        }

        public string PartialType { get; set; }
    }
}
