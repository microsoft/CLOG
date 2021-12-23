/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    Enumerates the set of encoding type choices within CLOG

--*/

using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace clogutils
{
    [DataContract]
    [JsonConverter(typeof(StringEnumConverter))]
    public enum CLogEncodingType : uint
    {
        Unknown = 0,
        Synthesized = 1,
        Skip = 2,
        Int32 = 3,
        UInt32 = 4,
        Int64 = 5,
        UInt64 = 6,
        ANSI_String = 7,
        UNICODE_String = 8,
        Pointer = 9,
        GUID = 10,
        Int16 = 11,
        UInt16 = 12,
        Int8 = 13,
        UInt8 = 14,
        ByteArray = 15,
        UserEncodingString = 16,
        UniqueAndDurableIdentifier = 17
    }

    public class ClogEventPrinter
    {
        public static string AsCorrectType(CLogEncodingType type, IClogEventArg arg)
        {
            switch (type)
            {
                case CLogEncodingType.Int32:
                    return arg.AsInt32.ToString();
                case CLogEncodingType.UInt32:
                    return arg.AsUInt32.ToString();
                case CLogEncodingType.Int64:
                    return arg.AsInt64.ToString();
                case CLogEncodingType.UInt64:
                    return arg.AsUInt64.ToString();
                case CLogEncodingType.ANSI_String:
                    return arg.AsString.ToString();
                /* case CLogEncodingType.UNICODE_String:
                     return arg.AsInt32.ToString();*/
                case CLogEncodingType.Pointer:
                    return arg.AsUInt64.ToString();
                /* case CLogEncodingType.GUID:
                     return arg.ToString();*/
                case CLogEncodingType.Int16:
                    return arg.AsInt16.ToString();
                case CLogEncodingType.UInt16:
                    return arg.AsUInt16.ToString();
                case CLogEncodingType.Int8:
                    return arg.AsInt8.ToString();
                case CLogEncodingType.UInt8:
                    return arg.AsUInt8.ToString();
                case CLogEncodingType.ByteArray:
                    return "UNSPECIFIED DECODER FOR BINARY TYPE";/*
                case CLogEncodingType.UserEncodingString:
                    return arg.AsInt32.ToString();
                case CLogEncodingType.UniqueAndDurableIdentifier:
                    return arg.AsInt32.ToString();*/
                case CLogEncodingType.Unknown:
                case CLogEncodingType.Synthesized:
                case CLogEncodingType.Skip:
                default:
                    throw new CLogEnterReadOnlyModeException("Invalid Type - likely a missing CLOG feature, consider using a different type or submiting a patch to CLOG", CLogHandledException.ExceptionType.EncoderIncompatibleWithType, null);
            }
        }
    }

    public interface IClogEventArg
    {
        string AsString { get; }
        int AsInt32 { get; }
        uint AsUInt32 { get; }
        System.Int64 AsInt64 { get; }
        System.UInt64 AsUInt64 { get; }
        System.Int16 AsInt16 { get; }
        System.UInt16 AsUInt16 { get; }
        sbyte AsInt8 { get; }
        byte AsUInt8 { get; }
        ulong AsPointer { get; }
        byte[] AsBinary { get; }

        /*
        ANSI_String = 7,
        UNICODE_String = 8,

        GUID = 10,
         ByteArray = 15,
        UserEncodingString = 16,
        UniqueAndDurableIdentifier = 17,
        Int32Array = 18,
        UInt32Array = 19,
        Int64Array = 20,
        UInt64Array = 21,
        ANSI_StringArray = 22,
        UNICODE_StringArray = 23,
        PointerArray = 24,
        GUIDArray = 25,
        Int16Array = 26,
        UInt16Array = 27,
        Int8Array = 28,
        */
    }
}
