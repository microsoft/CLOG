/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    Describes a CLOG macro,  this class will be converted into JSON and stored within a CLOG configuration file

--*/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;

namespace clogutils.MacroDefinations
{
    [JsonObject(MemberSerialization.OptIn)]
    public class CLogTraceMacroDefination
    {
        [JsonProperty] public string MacroName { get; set; }

        [JsonProperty] public string EncodedPrefix { get; set; }

        [JsonProperty] public virtual int EncodedArgNumber { get; set; }

        [JsonProperty] public Dictionary<string, string> MacroConfiguration { get; set; }

        [JsonProperty] public Dictionary<string, Dictionary<string, string>> CustomSettings { get; set; }

        [JsonProperty] public bool ClassFunctionEncoding { get; set; }

        public bool NoEncodingOk { get; set; }

        [JsonProperty] public string MacroNameConversionName { get; set; }


        public string ConfigFileWithMacroDefination { get; set; }

        public bool ShouldSerializeMacroNameConversionName()
        {
            return false;
        }

        public bool ShouldSerializeClassFunctionEncoding()
        {
            return (true == ClassFunctionEncoding);
        }

        [DefaultValue(CLogUniqueIDEncoder.Basic)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public CLogUniqueIDEncoder UniqueIdEncoder { get; set; }

        public string CombinePrefixWithEncodedString(string encodedString)
        {
            return EncodedPrefix + encodedString.Substring(1, encodedString.Length - 2);
        }

        public void DecodeUniqueId(CLogLineMatch m, string arg, out string id, out int idHash)
        {
            id = null;

            switch (UniqueIdEncoder)
            {
                case CLogUniqueIDEncoder.Basic:
                    idHash = Math.Abs(arg.GetHashCode());
                    id = arg;
                    break;

                case CLogUniqueIDEncoder.StringAndNumerical:
                    {
                        int lastIdx = arg.LastIndexOf('_');

                        if (-1 == lastIdx)
                            throw new CLogEnterReadOnlyModeException(
                                "Unique ID is not in the correct format, please follow the 'StringAndNumerical' format",
                                 CLogHandledException.ExceptionType.IncorrectStringAndNumericalEncoding,
                                m);

                        id = arg.Substring(0, lastIdx);

                        if (string.IsNullOrEmpty(id))
                        {
                            throw new CLogEnterReadOnlyModeException(
                                "Unique ID is not in the correct format, please follow the 'StringAndNumerical' format",
                                 CLogHandledException.ExceptionType.IncorrectStringAndNumericalEncoding, m);
                        }

                        try
                        {
                            idHash = Convert.ToInt32(arg.Substring(lastIdx + 1));
                        }
                        catch (FormatException)
                        {
                            throw new CLogEnterReadOnlyModeException(
                                "Unique ID was located but is not in the correct format, please follow the 'StringAndNumerical' format",
                                 CLogHandledException.ExceptionType.IncorrectStringAndNumericalEncoding,
                                m);
                        }
                    }
                    break;

                default:
                    throw new CLogEnterReadOnlyModeException("Invalid ID Encoder", CLogHandledException.ExceptionType.IncorrectStringAndNumericalEncoding, m);
            }
        }

        public bool ShouldSerializeUniqueIdEncoder()
        {
            return UniqueIdEncoder != CLogUniqueIDEncoder.Basic;
        }
    }
}
