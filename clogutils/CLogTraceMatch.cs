/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    Describes a source line that matches with a CLOG macro

--*/

using System;
using System.Text.RegularExpressions;

namespace clogutils
{
    public class CLogLineMatch
    {
        public Match MatchedRegEx { get; private set; }

        public string SourceFile { get; private set; }

        public CLogLineMatch(string sourcefile, Match m)
        {
            if (string.IsNullOrEmpty(sourcefile))
                throw new ArgumentException();

            SourceFile = sourcefile;
            MatchedRegEx = m;
        }

        private CLogLineMatch()
        {
        }
    }
}
