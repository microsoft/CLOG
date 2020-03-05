/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    A stream manages the send and receive queues for application data. This file
    contains the initialization and cleanup functionality for the stream.

--*/

using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace clogutils.MacroDefinations
{
    [DataContract]
    [JsonConverter(typeof(StringEnumConverter))]
    public enum CLogUniqueIDEncoder
    {
        Unspecified = 0,
        Basic = 1,
        StringAndNumerical = 2
    }
}
