/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    Describes a CLOG search macro within a JSON configuration file

--*/

namespace clogutils
{
    public class CLogEncodingCLogTypeSearch
    {
        public CLogEncodingCLogTypeSearch(string d, bool synthesized = false)
        {
            DefinationEncoding = d;
            Synthesized = synthesized;
        }

        public CLogEncodingType EncodingType { get; set; }

        public string CType { get; set; }

        public string DefinationEncoding { get; set; }

        public string CustomDecoder { get; set; }

        public bool Synthesized { get; set; }
    }
}
