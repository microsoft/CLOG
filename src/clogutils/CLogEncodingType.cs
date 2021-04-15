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
        // UInt8Array, // Not used, use ByteArray
        Struct = 29
    }
}
