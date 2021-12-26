/*++
s
    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    This file contains the code to iterate across your C/C++ code looking for a regular expression describing an event

--*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using clogutils.ConfigFile;
using clogutils.MacroDefinations;
using Newtonsoft.Json;

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
            string inspectToken;


            if (!inspect.ClassFunctionEncoding)
                inspectToken = inspect.MacroName + "\\s*" + @"\((?<args>.*?)\);";
            else
                inspectToken = inspect.MacroName + "\\.(?<methodname>[A-Za-z0-9_-]*)" + @"\((?<args>.*?)\);";

            Regex r = new Regex(inspectToken, RegexOptions.Singleline);
            SortedList<int, CLogLineMatch> matches = new SortedList<int, CLogLineMatch>();

            foreach (Match m in r.Matches(data))
            {
                string uid = "";
                string args = "";
                string encodedString = "";

                List<string> splitArgs = new List<string>();
                if(inspect.NoEncodingOk)
                {
                    args = m.Groups["args"].ToString();

                    splitArgs = new List<string>(SplitWithEscapedQuotes(args, ','));

                    if (0 != args.Length && inspect.EncodedArgNumber >= splitArgs.Count)
                    {
                        throw new CLogHandledException("EncodedArgNumberTooLarge", CLogHandledException.ExceptionType.EncodedArgNumberInvalid, null);
                    }
                }
                else if (inspect.ClassFunctionEncoding)
                {
                    uid = m.Groups["methodname"].ToString();
                    args = m.Groups["args"].ToString();

                    splitArgs = new List<string>(SplitWithEscapedQuotes(args, ','));

                    if (inspect.EncodedArgNumber >= splitArgs.Count)
                    {
                        throw new CLogHandledException("EncodedArgNumberTooLarge", CLogHandledException.ExceptionType.EncodedArgNumberInvalid, null);
                    }

                    encodedString = splitArgs[inspect.EncodedArgNumber];
                }
                else
                {
                    args = m.Groups["args"].ToString();

                    splitArgs = new List<string>(SplitWithEscapedQuotes(args, ','));
                    uid = splitArgs[0].Trim();

                    if (inspect.EncodedArgNumber >= splitArgs.Count)
                    {
                        throw new CLogHandledException("EncodedArgNumberTooLarge", CLogHandledException.ExceptionType.EncodedArgNumberInvalid, null);
                    }

                    encodedString = splitArgs[inspect.EncodedArgNumber];
                }

                CLogLineMatch lineMatch = new CLogLineMatch(sourceFileName, m, uid, encodedString, args, splitArgs.ToArray());
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
                            throw new CLogEnterReadOnlyModeException($"Unable to split string: {info}", CLogHandledException.ExceptionType.InvalidInput, null);
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


        public class DecomposedString
        {
            public void AddEncoding(EncodingArg arg)
            {
                encodings.Add(arg);
            }

            public EncodingArg CreateNewArg()
            {
                EncodingArg arg = new EncodingArg();
                encodings.Add(arg);
                return arg;
            }
            public class EncodingArg
            {
                public string Prefix { get; set; } = "";
                public CLogEncodingCLogTypeSearch Type { get; set; }
            }

            public string AsPrintF
            {
                get
                {
                    string ret = "";
                    int idx = 0;
                    foreach (var e in encodings)
                    {
                        if (null != e.Type)
                        {
                            switch(e.Type.EncodingType)
                            {
                                case CLogEncodingType.ByteArray:
                                    ret += "p";
                                    break;
                                default:
                                    ret += e.Type.DefinationEncoding;
                                    break;
                            }

                            ++idx;
                        }
                        ret += e.Prefix;
                    }

                    return ret;
                }
            }
            public string AsManifestedETWEncoding
            {
                get
                {
                    string ret = "";
                    int idx = 1;
                    foreach(var e in encodings)
                    {
                        if(null != e.Type)
                        {
                            //ret += e.Type.DefinationEncoding;
                            ret += idx;
                            ++idx;
                        }
                        ret += e.Prefix;
                    }

                    return ret;
                }
            }

            private List<EncodingArg> encodings = new List<EncodingArg>();
        }

        public static CLogTypeContainer[] BuildTypes(CLogConfigurationFile configFile, CLogLineMatch traceLineMatch, string argString,
            string traceLine,
            out DecomposedString decompString)
        {
            List<CLogTypeContainer> ret = new List<CLogTypeContainer>();
            string pieces = string.Empty;
            int argCount = 0;

            decompString = new DecomposedString();

            string prefixString = "";

            DecomposedString.EncodingArg currentArg = decompString.CreateNewArg();

            if (string.IsNullOrEmpty(argString))
            {
                return new CLogTypeContainer[0];
            }

            // Make sure we start and stop with a quote - this prevents L"" (unicode) as well as other oddities that seem to be 'okay' in WPP but shoudlnt be okay
            argString = argString.Trim();

            for (int i = 0; i < argString.Length; ++i)
            {
                pieces += argString[i];
                currentArg.Prefix += argString[i];

                if ('%' == argString[i])
                {
                    pieces += (argCount++) + 1; ;

                    CLogTypeContainer newNode = new CLogTypeContainer();
                    newNode.LeadingString = prefixString;
                    newNode.ArgStartingIndex = i;

                    currentArg = decompString.CreateNewArg();

                    ++i;

                    // Check to see if a custom name is specified for this type
                    string preferredName = "";
                    if ('{' == argString[i])
                    {
                        // Skip the opening brace
                        i++;
                        if (i == argString.Length)
                        {
                            throw new CLogEnterReadOnlyModeException("InvalidNameFormatInTypeSpcifier", CLogHandledException.ExceptionType.TooFewArguments, traceLineMatch);
                        }

                        while (',' != argString[i])
                        {
                            // If we find a closing brace or a space before finding the comma, it's a parsing error
                            if ('}' == argString[i] || char.IsWhiteSpace(argString[i]))
                            {
                                throw new CLogEnterReadOnlyModeException("InvalidNameFormatInTypeSpcifier", CLogHandledException.ExceptionType.WhiteSpaceNotAllowed, traceLineMatch);
                            }

                            preferredName += argString[i];

                            i++;
                            if (i == argString.Length)
                            {
                                throw new CLogEnterReadOnlyModeException("InvalidNameFormatInTypeSpcifier", CLogHandledException.ExceptionType.TooFewArguments, traceLineMatch);
                            }
                        }

                        // Skip the comma
                        i++;

                        // Don't allow white spaces after comma
                        if(char.IsWhiteSpace(argString[i]))
                              throw new CLogEnterReadOnlyModeException("InvalidNameFormatInTypeSpcifier", CLogHandledException.ExceptionType.WhiteSpaceNotAllowed, traceLineMatch);

                        if (i == argString.Length)
                        {
                            throw new CLogEnterReadOnlyModeException("InvalidNameFormatInTypeSpcifier", CLogHandledException.ExceptionType.TooFewArguments, traceLineMatch);
                        }
                    }

                    CLogEncodingCLogTypeSearch t;

                    try
                    {
                        // 'i' will point to the final character on a match (such that i+1 is the next fresh character)
                        t = configFile.FindTypeAndAdvance(argString, traceLineMatch, ref i);

                    }
                    catch (CLogTypeNotFoundException)
                    {
                        throw;
                    }

                    // If we found a preferred name, the next character after the type should be a closing brace
                    if (preferredName.Length != 0)
                    {
                        i++;
                        if (i == argString.Length || '}' != argString[i])
                        {
                            throw new CLogEnterReadOnlyModeException("InvalidNameFormatInTypeSpcifier", CLogHandledException.ExceptionType.TooFewArguments, traceLineMatch);
                        }

                        newNode.PreferredName = preferredName;
                    }

                    // Compute lengths
                    newNode.TypeNode = t;
                    newNode.ArgLength = i - newNode.ArgStartingIndex + 1;
                    currentArg.Type = t;

                    prefixString = "";

                    ret.Add(newNode);
                }
                else
                {
                    prefixString += argString[i];
                }
            }

            if (!pieces.Equals(decompString.AsManifestedETWEncoding))
                throw new Exception("ETW strings dont match");

            return ret.ToArray();
        }

        private static (string, string)[] MakeVariable(CLogConfigurationFile configFile, string[] args)
        {
            List<(string, string)> ret = new List<(string, string)>();
            HashSet<string> inUse = new HashSet<string>();

            for (int i = 0; i < args.Length; ++i)
            {
                string value = args[i];
                string name = "arg" + i;
                ret.Add((value, name));
                inUse.Add(name);
            }

            return ret.ToArray();
        }

        private static CLogDecodedTraceLine BuildArgsFromEncodedArgsX(CLogConfigurationFile configFile, string sourcefile,
            CLogTraceMacroDefination macroDefination, CLogLineMatch traceLineMatch, string traceLine)
        {
            string userArgs = String.Empty;

            if (macroDefination.NoEncodingOk)
                userArgs = "";
            else
                userArgs = macroDefination.CombinePrefixWithEncodedString(traceLineMatch.EncodingString);

            CLogFileProcessor.DecomposedString decompString;

            //
            // Loop across all types, ignoring the ones that are not specified in the source code
            //
            Queue<CLogTypeContainer> types = new Queue<CLogTypeContainer>();
            foreach (var type in BuildTypes(configFile, traceLineMatch, userArgs, traceLine, out decompString))
            {
                if (type.TypeNode.Synthesized)
                {
                    continue;
                }

                types.Enqueue(type);
            }

            var vars = MakeVariable(configFile, traceLineMatch.Args);
            List<CLogVariableBundle> finalArgs = new List<CLogVariableBundle>();

            for (int i = 0; i < traceLineMatch.Args.Length; ++i)
            {
                if (i == macroDefination.EncodedArgNumber)
                {

                    CLogTypeContainer item = new CLogTypeContainer();
                    var info = VariableInfo.X(traceLineMatch.Args[i], vars[i].Item2, i);
                    var bundle = new CLogVariableBundle();
                    var type = new CLogEncodingCLogTypeSearch();

                    type.EncodingType = CLogEncodingType.UserEncodingString;

                    bundle.TypeNode = type;
                    finalArgs.Add(bundle);
                }
                else
                {
                    // If this is C/C++ encoding, the 0'th arg is the identifier
                    if (!macroDefination.ClassFunctionEncoding && 0 == i)
                    {
                        CLogTypeContainer item = new CLogTypeContainer();
                        var info = VariableInfo.X(traceLineMatch.Args[i], vars[i].Item2, i);
                        var bundle = new CLogVariableBundle();
                        var type = new CLogEncodingCLogTypeSearch();

                        type.EncodingType = CLogEncodingType.UniqueAndDurableIdentifier;

                        bundle.TypeNode = type;
                        finalArgs.Add(bundle);
                    }
                    else if(!String.IsNullOrEmpty(macroDefination.MacroNameConversionName))
                    {

                    }
                    else
                    {
                        if (0 == types.Count)
                        {
                            CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, "Trace line is has the incorrect format - too few arguments were specified");
                            CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, $"    Event Descriptor : {userArgs}");
                            throw new CLogEnterReadOnlyModeException("TooFewArguments", CLogHandledException.ExceptionType.TooFewArguments, traceLineMatch);
                        }

                        CLogTypeContainer item = types.Dequeue();
                        var info = VariableInfo.X(traceLineMatch.Args[i], vars[i].Item2, i);

                        // If a preferred name was found in the format string, use that
                        if (!String.IsNullOrEmpty(item.PreferredName))
                        {
                            // Check to see if their preferred name is valid
                            bool hasBadChars = false;
                            foreach (char c in item.PreferredName)
                            {
                                if (!char.IsDigit(c) && !char.IsLetter(c) && c != '_')
                                {
                                    hasBadChars = true;
                                }
                            }

                            if (item.PreferredName.Length > configFile.MaximumVariableLength || hasBadChars)
                            {
                                Console.WriteLine($"WARNING: {item.PreferredName} contains invalid characters (must be <= {configFile.MaximumVariableLength} characters and containing only alphanumeric plus underscore. Ignoring user specified {item.PreferredName} and  using {info.SuggestedTelemetryName} instead!");
                            }
                            else
                            {
                                info.SuggestedTelemetryName = item.PreferredName;
                            }
                        }

                        var bundle = CLogVariableBundle.CreateVariableBundle(info, item.TypeNode.DefinationEncoding, null);
                        CLogEncodingCLogTypeSearch type = configFile.FindType(bundle, traceLineMatch);
                        bundle.TypeNode = type;
                        finalArgs.Add(bundle);
                    }
                }
            }

            if (0 != types.Count)
            {
                CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, "Too many arguments were specified in trace line");
                throw new CLogEnterReadOnlyModeException("TooManyArguments", CLogHandledException.ExceptionType.TooFewArguments, traceLineMatch);
            }

            Regex rg = new Regex(@"^[a-zA-Z0-9_]*$");
            if (!rg.IsMatch(traceLineMatch.UniqueID))
            {
                CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, $"CLOG Unique ID's must be alpha numeric and {traceLineMatch.UniqueID} is not");
                throw new CLogEnterReadOnlyModeException("InvalidUniqueID", CLogHandledException.ExceptionType.InvalidUniqueId, traceLineMatch);
            }

            //if(traceLineMatch.UniqueID.Length >= 47)
            //    throw new CLogEnterReadOnlyModeException("TooManyCharacters", CLogHandledException.ExceptionType.InvalidUniqueId, traceLineMatch);

            CLogDecodedTraceLine decodedTraceLine = new CLogDecodedTraceLine(traceLineMatch.UniqueID, sourcefile, userArgs, traceLineMatch.EncodingString, traceLineMatch, configFile, macroDefination, finalArgs.ToArray(), decompString);

            return decodedTraceLine;
        }

        public string ConvertFile(CLogConfigurationFile configFile, CLogOutputInfo outputInfo, ICLogFullyDecodedLineCallbackInterface callbacks,
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
                            int idx = match.Value.MatchedRegExX.Index - 1;

                            while (idx > 0 && (contents[idx] == ' ' || contents[idx] == '\t'))
                            {
                                idx--;
                            }

                            end = match.Value.MatchedRegExX.Index;

                            string keep = contents.Substring(start, end - start);

                            results.Append(keep);

                            CLogDecodedTraceLine traceLine = BuildArgsFromEncodedArgsX(configFile, contentsFileName, macro, match.Value, match.Value.AllArgs);
                            callbacks.TraceLineDiscovered(traceLine, outputInfo, results);
                            start = end = match.Value.MatchedRegExX.Index + match.Value.MatchedRegExX.Length;
                        }
                        catch (CLogHandledException)
                        {
                            throw;
                        }
                        catch (Exception e)
                        {
                            throw new CLogEnterReadOnlyModeException("Cant Read Line Input", CLogHandledException.ExceptionType.InvalidInput, match.Value, e);
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

            public string IndexBasedName { get { return "arg" + _index; } }

            private int _index;

            public static VariableInfo X(string user, string suggestedName, int index)
            {
                foreach (char c in suggestedName)
                {
                    if (!char.IsLetter(c) && !char.IsNumber(c) && c != '_')
                    {
                        throw new Exception($"VariableName isnt valid {suggestedName}");
                    }
                }

                VariableInfo v = new VariableInfo();
                v.UserSuppliedTrimmed = user.Trim();
                v.UserSpecifiedUnModified = user;
                v.SuggestedTelemetryName = suggestedName;
                v._index = index;
                return v;
            }
        }

        public class CLogTypeContainer
        {
            public string LeadingString { get; set; }

            public CLogEncodingCLogTypeSearch TypeNode { get; set; }

            public int ArgStartingIndex { get; set; }

            public int ArgLength { get; set; }

            public string PreferredName { get; set; }
        }

        [JsonObject(MemberSerialization.OptIn)]
        public class CLogVariableBundle
        {
            //[JsonProperty]
            public VariableInfo VariableInfo { get; set; }

            [JsonProperty] public string DefinationEncoding { get; set; }

            [JsonProperty] public string MacroVariableName { get; set; }

            [JsonProperty] public string EventVariableName { get; set; }

            public bool ShouldSerializeEventVariableName()
            {
                //
                // In an attempt to reduce disk space in the sidecar
                //    only serialize the EventVariableName if it's set and if its
                //    different from the MacroVariableName
                //
                if (null == MacroVariableName)
                    return true;
                if (null == EventVariableName)
                    return false;

                return !MacroVariableName.Equals(EventVariableName); ;
            }

            public CLogEncodingCLogTypeSearch TypeNode { get; set; }

            public static CLogVariableBundle CreateVariableBundle(VariableInfo i, string definationEncoding, CLogEncodingCLogTypeSearch typeNode)
            {
                CLogVariableBundle b = new CLogVariableBundle();
                b.VariableInfo = i;
                b.DefinationEncoding = definationEncoding;
                return b;
            }
        }

        public interface ICLogFullyDecodedLineCallbackInterface
        {
            void TraceLineDiscovered(CLogDecodedTraceLine decodedTraceLine, CLogOutputInfo outputInfo, StringBuilder results);
        }

        public interface ICLogPartiallyDecodedLineCallbackInterfaceX
        {
            string ReplaceLineWith(CLogDecodedTraceLine decodedTraceLine);
        }
    }
}
