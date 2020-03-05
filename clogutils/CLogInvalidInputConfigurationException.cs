/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    A stream manages the send and receive queues for application data. This file
    contains the initialization and cleanup functionality for the stream.

--*/

using System;

namespace clogutils
{
    public class CLogInvalidInputConfigurationException : CLogHandledException
    {
        public static bool AreReadOnly;

        public CLogInvalidInputConfigurationException(string msg, bool silent = false, Exception e = null) : base(msg,
            null, silent, e)
        {
            AreReadOnly = true;
            Exception = e;
        }
    }
}
