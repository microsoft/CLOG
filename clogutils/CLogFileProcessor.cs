/*++
s
    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    This file contains the code to iterate across your C/C++ code looking for a regular expression describing an event

--*/

using clogutils.ConfigFile;
using clogutils.MacroDefinations;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace clogutils
{
    public class CLogFileProcessor
    {
        private readonly HashSet<CLogTraceMacroDefination> _inUseMacro = new HashSet<CLogTraceMacroDefination>();

        public CLogFileProcessor(CLogConfigurationFile configFile)
        {
            ConfigFile = configFile;
        }

        public CLogConfigurationFile ConfigFile { get; }

        public CLogTraceMacroDefination[] MacrosInUse
        {
            get { return _inUseMacro.ToArray(); }
        }

        private static SortedList<int, CLogLineMatch> UpdateMatches(string data, string sourceFileName, CLogTraceMacroDefination inspect)
        {
            string inspectToken = inspect.MacroName + "\\s*" + @"\((?<args>.*?)\);";
            Regex r = new Regex(inspectToken, RegexOptions.Singleline);
            SortedList<int, CLogLineMatch> matches = new SortedList<int, CLogLineMatch>();

            foreach (Match m in r.Matches(data))
            {
                CLogLineMatch lineMatch = new CLogLineMatch(sourceFileName, m);
                matches.Add(m.Groups["0"].Index, lineMatch);
            }

            return matches;
        }

        public static Guid GenerateMD5Hash(string hashString)
        {
            var stringBuilder = new StringBuilder();

            // calculate the MD5 hash of the string
            using (var md5 = MD5.Create())
            {
                md5.Initialize();
                md5.ComputeHash(Encoding.UTF8.GetBytes(hashString));

                var hash = md5.Hash;

                for (int i = 0; i < hash.Length; i++)
                {
                    stringBuilder.Append(hash[i].ToString("x2"));
                }
            }

            // convert the string hash to guid
            return Guid.Parse(stringBuilder.ToString());
        }

        private static string[] SplitWithEscapedQuotes(string info, char splitChar)
        {
            int start = 0;
            int end = 0;
            int numParan = 0;
            bool inQuotes = false;
            List<string> ret = new List<string>();

            for (int i = 0; i < info.Length; ++i)
            {
                if (info[i] == '\\')
                {
                }
                else if (info[i] == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (inQuotes)
                {
                }
                else
                {
                    if (info[i] == '(')
                    {
                        ++numParan;
                    }
                    else if (info[i] == ')')
                    {
                        if (0 == numParan)
                        {
                            throw new Exception("Invalid Input");
                        }

                        --numParan;
                    }
                    else if (splitChar == info[i] && 0 == numParan)
                    {
                        string bits = info.Substring(start, end - start);
                        ret.Add(bits);
                        start = end + 1;
                    }
                }

                ++end;
            }

            if (start < end)
            {
                string final = info.Substring(start, end - start);
                ret.Add(final);
            }

            return ret.ToArray();
        }

        public static CLogTypeContainer[] BuildTypes(CLogConfigurationFile configFile, CLogLineMatch traceLineMatch, string argString,
            string traceLine,
            out string cleanedString)
        {
            List<CLogTypeContainer> ret = new List<CLogTypeContainer>();
            string pieces = string.Empty;
            int argCount = 0;
            cleanedString = string.Empty;
            string prefixString = "";

            if (string.IsNullOrEmpty(argString))
            {
                return new CLogTypeContainer[0];
            }

            // Make surew e start and stop with a quote - this prevents L"" (unicode) as well as other oddities that seem to be 'okay' in WPP but shoudlnt be okay
            argString = argString.Trim();

            for (int i = 0; i < argString.Length; ++i)
            {
                pieces += argString[i];

                if ('%' == argString[i])
                {
                    pieces += argCount++;
                    pieces += " ";

                    CLogTypeContainer newNode = new CLogTypeContainer();
                    newNode.LeadingString = prefixString;
                    newNode.ArgStartingIndex = i;

                    ++i;

                    CLogEncodingCLogTypeSearch t;

                    try
                    {
                        // 'i' will point to the final character on a match (such that i+1 is the next fresh character)
                        t = configFile.FindTypeAndAdvance(argString, traceLineMatch, ref i);
                    }
                    catch (CLogTypeNotFoundException e)
                    {
                        throw e;
                    }

                    newNode.TypeNode = t;
                    newNode.ArgLength = i - newNode.ArgStartingIndex + 1;
                    prefixString = "";

                    ret.Add(newNode);
                }
                else
                {
                    prefixString += argString[i];
                }
            }

            cleanedString = pieces;
            return ret.ToArray();
        }

        private static (string, string)[] MakeVariable(string[] args)
        {
            List<(string, string)> ret = new List<(string, string)>();
            HashSet<string> inUse = new HashSet<string>();

            for (int i = 0; i < args.Length; ++i)
            {
                string input = "_" + args[i].Trim();
                input = input.Replace("*", "").Replace("(", "").Replace(")", "").Replace(".", "");
                input = input.Replace(",", "").Replace("-", "").Replace(">", "");

                bool hasBadChars = false;

                foreach (char c in input)
                {
                    if (!char.IsDigit(c) && !char.IsLetter(c))
                    {
                        hasBadChars = true;
                    }
                }

                if (input.Length > 20 || hasBadChars)
                {
                    input = "arg" + i;
                }

                if (inUse.Contains(input))
                {
                    Console.WriteLine($"WARNING: {input} is already in use, using {input}{i} instead");
                    input = input + i;
                }

                ret.Add((args[i], input));
            }

            return ret.ToArray();
        }

        private static CLogDecodedTraceLine BuildArgsFromEncodedArgs(CLogConfigurationFile configFile, string sourcefile,
            CLogTraceMacroDefination macroDefination, CLogLineMatch traceLineMatch, string traceLine, string[] splitArgs)
        {
            string userArgs = macroDefination.CombinePrefixWithEncodedString(splitArgs[macroDefination.EncodedArgNumber].Trim());

            //
            // Loop across all types, ignoring the ones that are not specified in the source code
            //
            Queue<CLogTypeContainer> types = new Queue<CLogTypeContainer>();

            foreach (var type in BuildTypes(configFile, traceLineMatch, macroDefination.EncodedPrefix, traceLine, out _))
            {
                if (type.TypeNode.Synthesized)
                {
                    continue;
                }

                types.Enqueue(type);
            }

            foreach (var type in BuildTypes(configFile, traceLineMatch, splitArgs[macroDefination.EncodedArgNumber].Trim(), traceLine, out _))
            {
                if (type.TypeNode.Synthesized)
                {
                    continue;
                }

                types.Enqueue(type);
            }


            var vars = MakeVariable(splitArgs);
            List<CLogVariableBundle> finalArgs = new List<CLogVariableBundle>();

            for (int i = 1; i < splitArgs.Length; ++i)
            {
                if (i == macroDefination.EncodedArgNumber)
                {
                    continue;
                }

                if (0 == types.Count)
                {
                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, "Trace line is has the incorrect format - too few arguments were specified");
                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, $"    Event Descriptor : {userArgs}");

                    throw new CLogEnterReadOnlyModeException("TooFewArguments", CLogHandledException.ExceptionType.TooFewArguments, traceLineMatch);
                }

                CLogTypeContainer item = types.Dequeue();
                finalArgs.Add(CLogVariableBundle.X(VariableInfo.X(splitArgs[i], vars[i].Item2), item.TypeNode.DefinationEncoding));
            }

            if (0 != types.Count)
            {
                CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, "Too many arguments were specified in trace line");
                throw new CLogEnterReadOnlyModeException("TooManyArguments", CLogHandledException.ExceptionType.TooFewArguments, traceLineMatch);
            }

            string uniqueId = splitArgs[0].Trim();
            Regex rg = new Regex(@"^[a-zA-Z0-9_]*$");
            if (!rg.IsMatch(uniqueId))
            {
                CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, $"CLOG Unique ID's must be alpha numeric and {uniqueId} is not");
                throw new CLogEnterReadOnlyModeException("InvalidUniqueID", CLogHandledException.ExceptionType.InvalidUniqueId, traceLineMatch);
            }

            CLogDecodedTraceLine decodedTraceLine = new CLogDecodedTraceLine(uniqueId, sourcefile, userArgs, splitArgs[macroDefination.EncodedArgNumber].Trim(), traceLineMatch, configFile, macroDefination, finalArgs.ToArray());

            return decodedTraceLine;
        }

        public string ConvertFile(CLogConfigurationFile configFile, ICLogFullyDecodedLineCallbackInterface callbacks,
            string contents, string contentsFileName, bool conversionMode)
        {
            string remaining = contents;

            foreach (CLogTraceMacroDefination macro in ConfigFile.AllKnownMacros())
            {
                StringBuilder results = new StringBuilder();
                int start = 0, end = 0;

                start = 0;
                end = 0;
                results = new StringBuilder();
                KeyValuePair<int, CLogLineMatch> lastMatch = new KeyValuePair<int, CLogLineMatch>(0, null);

                try
                {
                    foreach (var match in UpdateMatches(contents, contentsFileName, macro))
                    {
                        // We track which macros actually got used, in this way we can emit only what is needed to the .clog file
                        _inUseMacro.Add(macro);

                        try
                        {
                            lastMatch = match;

                            string[] splitArgs = SplitWithEscapedQuotes(match.Value.MatchedRegEx.Groups["args"].ToString(), ',');

                            int idx = match.Value.MatchedRegEx.Index - 1;

                            while (idx > 0 && (contents[idx] == ' ' || contents[idx] == '\t'))
                            {
                                idx--;
                            }

                            end = match.Value.MatchedRegEx.Index;

                            string keep = contents.Substring(start, end - start);

                            results.Append(keep);

                            CLogDecodedTraceLine traceLine = BuildArgsFromEncodedArgs(configFile, contentsFileName, macro, match.Value, match.Value.MatchedRegEx.Groups["args"].ToString(), splitArgs);
                        /*    if (callbacks is ICLogPartiallyDecodedLineCallbackInterfaceX)
                            {
                                string toAdd = ((ICLogPartiallyDecodedLineCallbackInterfaceX)callbacks).ReplaceLineWith(traceLine);
                                start = end = match.Value.MatchedRegEx.Index + match.Value.MatchedRegEx.Length;
                            }
                            else
                            {  */                            
                                callbacks.TraceLineDiscovered(traceLine, results);
                                start = end = match.Value.MatchedRegEx.Index + match.Value.MatchedRegEx.Length;
                          //  }                                                        
                        }
                        catch (CLogHandledException)
                        {
                            throw;
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"ERROR: cant process {match}");
                            Console.WriteLine(e);

                            throw new CLogEnterReadOnlyModeException("Cant Read Line Input", CLogHandledException.ExceptionType.InvalidInput, match.Value);
                        }
                    }

                    if (end != contents.Length - 1)
                    {
                        results.Append(contents.Substring(start, contents.Length - end));
                    }

                    if (conversionMode)
                        contents = results.ToString();
                }
                catch (Exception e)
                {
                    if (null == lastMatch.Value)
                    {
                        throw new CLogHandledException("NoLine", CLogHandledException.ExceptionType.InvalidInput, null, e);
                    }

                    throw;
                }
            }

            return contents;
        }

        public class VariableInfo
        {
            public string UserSpecifiedUnModified { get; set; }

            public string UserSuppliedTrimmed { get; set; }

            public string SuggestedTelemetryName { get; set; }

            public static VariableInfo X(string user, string suggestedName)
            {
                foreach (char c in suggestedName)
                {
                    if (!char.IsLetter(c) && !char.IsNumber(c))
                    {
                        throw new Exception($"VariableName isnt valid {suggestedName}");
                    }
                }

                VariableInfo v = new VariableInfo();
                v.UserSuppliedTrimmed = user.Trim();
                v.UserSpecifiedUnModified = user;
                v.SuggestedTelemetryName = suggestedName;
                return v;
            }
        }

        public class CLogTypeContainer
        {
            public string LeadingString { get; set; }

            public CLogEncodingCLogTypeSearch TypeNode { get; set; }

            public int ArgStartingIndex { get; set; }

            public int ArgLength { get; set; }
        }

        [JsonObject(MemberSerialization.OptIn)]
        public class CLogVariableBundle
        {
            [JsonProperty] public VariableInfo VariableInfo { get; set; }

            [JsonProperty] public string DefinationEncoding { get; set; }

            [JsonProperty] public string MacroVariableName { get; set; }

            public static CLogVariableBundle X(VariableInfo i, string definationEncoding)
            {
                CLogVariableBundle b = new CLogVariableBundle();
                b.VariableInfo = i;
                b.DefinationEncoding = definationEncoding;
                return b;
            }
        }

        public interface ICLogFullyDecodedLineCallbackInterface
        {
            void TraceLineDiscovered(CLogDecodedTraceLine decodedTraceLine, StringBuilder results);
        }

        public interface ICLogPartiallyDecodedLineCallbackInterfaceX
        {
            string ReplaceLineWith(CLogDecodedTraceLine decodedTraceLine);
        }
    }
}
