/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    Defines how a CLOG event ID is encoded - presently we're using 'Basic' which just means that an event ID is the string

    for example:
        Trace(MESSAGEID, "Hello world");   

        if you'd like to encode information within "MESSAGEID" this is how you'd do it - specifically StringAndNumerical (MESSAGE_12) could be worked 
        such that 12 is the numerical representation of MESSAGE.


    ***Presently Unused and incomplete***

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
