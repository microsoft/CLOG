/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    Throwing a read only exception puts CLOG into a read only mode - this is the suggested way to terminate the program such that errors are produced and sent to the user

--*/

using System;
using System.Text.RegularExpressions;

namespace clogutils
{
    public class CLogEnterReadOnlyModeException : CLogHandledException
    {
        public static bool AreReadOnly;

        public CLogEnterReadOnlyModeException(string msg, CLogHandledException.ExceptionType type, CLogLineMatch traceLine, bool silent = false, Exception e = null) :
            base(msg, type, traceLine, silent, e)
        {
            AreReadOnly = true;
            Exception = e;
        }
    }
}
