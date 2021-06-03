/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    Throwing a read only exception puts CLOG into a read only mode - this is the suggested way to terminate the program such that errors are produced and sent to the user

--*/

using System;

namespace clogutils
{
    public class CLogEnterReadOnlyModeException : CLogHandledException
    {
        public static bool AreReadOnly;

        public CLogEnterReadOnlyModeException(string msg, CLogHandledException.ExceptionType type, CLogLineMatch traceLine, Exception e = null) :
            base(msg, type, traceLine, e)
        {
            AreReadOnly = true;
            Exception = e;
            InfoString = msg;
        }
    }
}
