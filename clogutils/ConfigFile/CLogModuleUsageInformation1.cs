/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    A stream manages the send and receive queues for application data. This file
    contains the initialization and cleanup functionality for the stream.

--*/

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace clogutils.ConfigFile
{
    [JsonObject(MemberSerialization.OptIn)]
    public class CLogModuleUsageInformation
    {
        [JsonProperty] private List<CLogTraceLineInformation> TraceInformation { get; set; } = new List<CLogTraceLineInformation>();

        public bool IsUnique(ICLogOutputModule module, CLogDecodedTraceLine traceLine, out CLogTraceLineInformation existingTraceInformation)
        {
            existingTraceInformation = TraceInformation
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

        public Guid GenerateUniquenessHash(ICLogOutputModule module, CLogDecodedTraceLine traceLine, out string asString)
        {
            string info = traceLine.macro.MacroName + "|" + traceLine.UniqueId + "|" +
                          traceLine.TraceString + "|";

            foreach (var arg in traceLine.splitArgs)
            {
                info += traceLine.configFile.FindType(arg).EncodingType;
            }

            asString = info;
            return CLogFileProcessor.GenerateMD5Hash(info);
        }


        public void Insert(ICLogOutputModule module, CLogDecodedTraceLine traceLine)
        {
            string asString;
            Guid hash = GenerateUniquenessHash(module, traceLine, out asString);
            CLogTraceLineInformation info = TraceInformation
                .Where(x => x.TraceID.Equals(traceLine.UniqueId)).FirstOrDefault();

            if (null == info)
            {
                info = new CLogTraceLineInformation();
                info.Unsaved = true;
                info.PreviousFileMatch = traceLine;

                info.TraceID = traceLine.UniqueId;
                info.UniquenessHash = hash;
                TraceInformation.Add(info);
                traceLine.configFile.IsDirty = true;
            }

            if (info.UniquenessHash != hash)
            {
                throw new CLogEnterReadOnlyModeException("DuplicateID", traceLine.match);
            }
        }

        public void Remove(CLogConfigurationFile config, CLogTraceLineInformation trace)
        {
            TraceInformation.Remove(trace);
            config.IsDirty = true;
        }
    }
}
