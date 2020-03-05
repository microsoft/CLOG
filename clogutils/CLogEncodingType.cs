﻿/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    A stream manages the send and receive queues for application data. This file
    contains the initialization and cleanup functionality for the stream.

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
        ByteArray = 15
    }
}