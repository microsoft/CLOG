/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    Holds unique ID information for an individual trace line.  This code is what gurantees two events are not emitted with the same event ID across one config file.

--*/

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace clogutils.ConfigFile
{
    [JsonObject(MemberSerialization.OptIn)]
    public class CLogModuleUsageInformation_V1
    {
        [JsonProperty] public List<CLogTraceLineInformation> TraceInformation { get; set; } = new List<CLogTraceLineInformation>();
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class CLogModuleUsageInformation_V2
    {
        [JsonProperty] public List<CLogTraceLineInformation_V2> TraceInformation { get; set; } = new List<CLogTraceLineInformation_V2>();

        public static CLogModuleUsageInformation_V2 ConvertFromV1(CLogModuleUsageInformation_V1 v1)
        {
            CLogModuleUsageInformation_V2 ret = new CLogModuleUsageInformation_V2();
            foreach (var trace in v1.TraceInformation)
            {
                ret.TraceInformation.Add(CLogTraceLineInformation_V2.ConvertFromV1(trace));
            }
            return ret;
        }

        public void Sort()
        {
            TraceInformation.Sort((a, b) => a.TraceID.CompareTo(b.TraceID));
        }
    }


    public class CLogModuleUsageInformation
    {
        public CLogModuleUsageInformation(CLogModuleUsageInformation_V2 myFile)
        {
            _me = myFile;
        }

        private CLogModuleUsageInformation()
        {
        }
        private CLogModuleUsageInformation_V2 _me;

        public bool IsUnique(ICLogOutputModule module, CLogDecodedTraceLine traceLine, out CLogTraceLineInformation_V2 existingTraceInformation)
        {
            existingTraceInformation = _me.TraceInformation
                .Where(x => x.TraceID.Equals(traceLine.UniqueId)).FirstOrDefault();

            if (null == existingTraceInformation)
            {
                return true;
            }

            string asString;
            Guid hash = GenerateUniquenessHash(module, traceLine, out asString);

            if (hash != existingTraceInformation.UniquenessHash)
            {
                return false;
            }

            return true;
        }

        public Guid GenerateUniquenessHash(ICLogOutputModule module, CLogDecodedTraceLine decodedTraceLine, out string asString)
        {
            string info = decodedTraceLine.macro.MacroName + "|" + decodedTraceLine.UniqueId + "|" +
                          decodedTraceLine.TraceString + "|";


            foreach (var arg in decodedTraceLine.splitArgs)
            {
                if (arg.TypeNode.EncodingType == CLogEncodingType.UserEncodingString || arg.TypeNode.EncodingType == CLogEncodingType.UniqueAndDurableIdentifier)
                    continue;

                info += arg.TypeNode.EncodingType;
            }

            asString = info;
            return CLogFileProcessor.GenerateMD5Hash(info);
        }

        public void Insert(ICLogOutputModule module, CLogDecodedTraceLine traceLine)
        {
            string asString;
            Guid hash = GenerateUniquenessHash(module, traceLine, out asString);
            CLogTraceLineInformation_V2 info = _me.TraceInformation
                .Where(x => x.TraceID.Equals(traceLine.UniqueId)).FirstOrDefault();

            if (null == info)
            {
                info = new CLogTraceLineInformation_V2();
                info.Unsaved = true;
                info.PreviousFileMatch = traceLine;
                info.EncodingString = traceLine.TraceString;

                info.TraceID = traceLine.UniqueId;
                info.UniquenessHash = hash;
                _me.TraceInformation.Add(info);
            }

            if (info.UniquenessHash != hash)
            {
                throw new CLogEnterReadOnlyModeException("DuplicateID", CLogHandledException.ExceptionType.DuplicateId, traceLine.match);
            }
        }

        public void Remove(CLogTraceLineInformation_V2 trace)
        {
            _me.TraceInformation.Remove(trace);
        }
    }
}
