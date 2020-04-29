/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    Describes a CLOG event argument

--*/

namespace clogutils
{
    public partial class CLogConsoleTrace
    {
        public class EventInformation
        {
            public System.DateTimeOffset Timestamp { get; set; }
            public string CPUId { get; set; }
            public string ThreadId { get; set; }
        }

        public interface IClogEventArg
        {
            string AsString { get; }

            int AsInt32 { get; }

            uint AsUInt32 { get; }

            ulong AsPointer { get; }

            byte[] AsBinary { get; }
        }
    }
}
