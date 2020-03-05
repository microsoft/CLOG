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
        public interface IClogEventArg
        {
            string AsString { get; }

            int AsInt32 { get; }

            uint AsUInt32 { get; }

            byte[] AsBinary { get; }
        }
    }
}
