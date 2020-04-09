/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    Describes the error which occurs should a type not be found (for example "%hello" is provided in a trace, but doesnt exist in configuration

--*/

using System;
using System.Text.RegularExpressions;

namespace clogutils
{
    public class CLogTypeNotFoundException : CLogHandledException
    {
        public static bool AreReadOnly;

        public CLogTypeNotFoundException(string msg, string partialType, CLogLineMatch traceLine,
            Exception e = null) : base(msg, ExceptionType.UndefinedType, traceLine, e)
        {
            AreReadOnly = true;
            Exception = e;
            PartialType = partialType;
        }

        public string PartialType { get; set; }
    }
}
