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
        public Match MatchedRegExX { get; private set; }

        public string SourceFile { get; private set; }

        public string UniqueID { get; private set; }

        public string EncodingString { get; private set; }

        public string AllArgs { get; private set; }

        public string[] Args { get; private set; }

        public CLogLineMatch(string sourcefile, Match m, string uniqueId, string encodingString, string allArgs, string[] args)
        {
            if (string.IsNullOrEmpty(sourcefile))
                throw new ArgumentException();

            SourceFile = sourcefile;
            MatchedRegExX = m;
            UniqueID = uniqueId;
            AllArgs = allArgs;
            EncodingString = encodingString.Trim();

            Args = args;
        }

        private CLogLineMatch()
        {
        }
    }
}
