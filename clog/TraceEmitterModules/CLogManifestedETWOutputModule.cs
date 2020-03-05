﻿/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    A stream manages the send and receive queues for application data. This file
    contains the initialization and cleanup functionality for the stream.

--*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using clogutils;

namespace clog.TraceEmitterModules
{
    public class CLogManifestedETWOutputModule : ICLogOutputModule
    {
        private readonly bool _inited = false;
        private readonly Dictionary<Guid, ManifestInformation> _providerCache = new Dictionary<Guid, ManifestInformation>();

        public XmlDocument doc = new XmlDocument();
        public string xmlFileName;

        public CLogManifestedETWOutputModule()
        {
        }

        public string ModuleName
        {
            get { return "MANIFESTED_ETW"; }
        }

        public bool ManditoryModule
        {
            get { return false; }
        }

        public void FinishedProcessing(StringBuilder header, StringBuilder sourceFile)
        {
        }

        public void InitHeader(StringBuilder header)
        {
        }

        public void TraceLineDiscovered(string sourceFile, CLogDecodedTraceLine decodedTraceLine, CLogSidecar sidecar, StringBuilder macroPrefix, StringBuilder inline, StringBuilder function)
        {
            string hash = decodedTraceLine.UniqueId;

            if (!_inited)
            {
                if(!decodedTraceLine.macro.CustomSettings.ContainsKey("ETWManifestFile"))
                    throw new CLogEnterReadOnlyModeException("ETWManifestFileNotSpecified", decodedTraceLine.match);

               xmlFileName = decodedTraceLine.macro.CustomSettings["ETWManifestFile"];
               xmlFileName = Path.Combine(Path.GetDirectoryName(decodedTraceLine.macro.ConfigFileWithMacroDefination), xmlFileName);

               Init();
            }

            if (!decodedTraceLine.macro.CustomSettings.ContainsKey("ETW_Provider"))
            {
                Console.WriteLine($"The 'CustomSettings' dictionary for macro {decodedTraceLine.macro.MacroName} does not contain a GUID for the EtwProvider");
                Console.WriteLine("    Please add an entry and rerun");
                Console.WriteLine("");
                Console.WriteLine($"Configuration File  : {decodedTraceLine.configFile.FilePath}");
                Console.WriteLine("");
                Console.WriteLine("");
                Console.WriteLine("");
                throw new CLogEnterReadOnlyModeException("ETW_Provider:NotSpecified", decodedTraceLine.match);
            }

            Guid providerId = new Guid(decodedTraceLine.macro.CustomSettings["ETW_Provider"]);

            ManifestInformation manifest = FindProviderCache(providerId);
            string eventNamePrefix;
            if (!decodedTraceLine.macro.CustomSettings.TryGetValue("EventNamePrefix", out eventNamePrefix))
                eventNamePrefix = string.Empty;

            if (null == manifest)
            {
                Console.WriteLine($"Unable to locate ETW provider {providerId} in CLOG macro {decodedTraceLine.macro.MacroName}");
                Console.WriteLine("    CLOG will not create this provider within the manifest;  it will only add to an existing provider");
                Console.WriteLine("    please consult the MSDN documentation for an ETW manifest for instructions");
                Console.WriteLine("");

                Console.WriteLine($"Macro:  {providerId} is defined in {decodedTraceLine.configFile.FilePath}");
                Console.WriteLine($"ETW Manifest : is set as {xmlFileName}");
                Console.WriteLine("");
                Console.WriteLine("");
                Console.WriteLine("");
                throw new CLogEnterReadOnlyModeException("ManifestedETWProviderNotFoundInManifest", decodedTraceLine.match);
            }

            //
            // Only allow a hash one time for now....
            //
            if (manifest.knownHashes.Contains(hash))
            {
                return;
            }

            manifest.knownHashes.Add(hash);

            //
            //  Insert our event
            //
            List<XmlElement> toRemove = new List<XmlElement>();
            XmlElement newEvent = null;

            foreach (var p in manifest.events.ChildNodes)
            {
                if (!(p is XmlElement))
                {
                    continue;
                }

                XmlElement pe = (XmlElement)p;

                if (pe.Name == "event")
                {
                    if (!pe.HasAttribute("symbol"))
                    {
                        continue;
                    }

                    string symbol = pe.GetAttribute("symbol");

                    if (0 == symbol.CompareTo(eventNamePrefix + hash))
                    {
                        toRemove.Add(pe);
                        newEvent = pe;
                        break;
                    }
                }
            }

            foreach (var e in toRemove)
            {
                // manifest.events.RemoveChild(e);
            }

            toRemove.Clear();


            //
            //  Add the event
            //

            if (null == newEvent)
            {
                newEvent = doc.CreateElement("event", manifest.events.NamespaceURI);
                manifest.events.AppendChild(newEvent);
            }

            int hashUInt;
            string eventAsString;

            decodedTraceLine.macro.DecodeUniqueId(decodedTraceLine.match, hash, out eventAsString, out hashUInt);


            uint eventId;
            if (!newEvent.HasAttribute("value"))
            {
                eventId = FindUnusedEventId(providerId, decodedTraceLine.match);
                newEvent.SetAttribute("value", eventId.ToString());
            }
            else
            {
                eventId = Convert.ToUInt32(newEvent.GetAttribute("value"));
            }

            // Store the eventID for future decode 
            decodedTraceLine.AddConfigFileProperty(ModuleName, "EventID", eventId.ToString());

            newEvent.SetAttribute("symbol", eventNamePrefix + hash);

            //if ()
            {
                string oldTemplate = null;
                if (newEvent.HasAttribute("template"))
                    oldTemplate = newEvent.GetAttribute("template");
                string templateId = DiscoverOrCreateTemplate(decodedTraceLine, sidecar, providerId, oldTemplate, eventId);
                newEvent.SetAttribute("template", templateId);
            }


            //
            // Construct the function signature
            //
            string traceLine = $"EventWrite{eventNamePrefix + hash}(";
            bool haveMultipleArgs = false;

            foreach (var a in decodedTraceLine.splitArgs)
            {
                CLogFileProcessor.CLogVariableBundle arg = a;
                CLogEncodingCLogTypeSearch node = decodedTraceLine.configFile.FindType(arg);

                switch (node.EncodingType)
                {
                    case CLogEncodingType.Synthesized:
                        continue;

                    case CLogEncodingType.Skip:
                        continue;
                }

                if (haveMultipleArgs)
                {
                    traceLine += ", ";
                }

                haveMultipleArgs = true;

                switch (node.EncodingType)
                {
                    case CLogEncodingType.ByteArray:
                        traceLine += $"{arg.MacroVariableName}_len, {arg.MacroVariableName}";
                        continue;

                    default:
                        traceLine += $"{arg.MacroVariableName}";
                        break;
                }
            }

            traceLine += "); \\";
            inline.AppendLine(traceLine);
            Save();
        }

        private void Init()
        {
            if (_inited)
            {
                return;
            }

            if (!File.Exists(xmlFileName))
            {
                Console.WriteLine($"ETW Manifest {xmlFileName} doesnt exist");
                throw new CLogEnterReadOnlyModeException("Output Manifest Missing", null);
            }

            doc.PreserveWhitespace = true;
            doc.Load(xmlFileName);

            XmlElement assembly = doc["assembly"];

            if (null == assembly)
            {
                assembly = doc["instrumentationManifest"];
            }

            var instrumentation = assembly["instrumentation"];
            var rootEvents = instrumentation["events"];

            foreach (var p in rootEvents.ChildNodes)
            {
                if (!(p is XmlElement))
                {
                    continue;
                }

                XmlElement pe = (XmlElement)p;

                if (pe.Name == "provider")
                {
                    if (!pe.HasAttribute("guid"))
                    {
                        continue;
                    }

                    string attr = pe.GetAttribute("guid");
                    Guid id = new Guid(attr);

                    if (!_providerCache.ContainsKey(id))
                    {
                        _providerCache[id] = new ManifestInformation(doc, pe);
                    }
                }
            }
        }

        private ManifestInformation FindProviderCache(Guid providerId)
        {
            ManifestInformation ret;

            if (!_providerCache.TryGetValue(providerId, out ret))
            {
                return null;
            }

            return ret;
        }

        private uint FindUnusedEventId(Guid providerId, Match sourceLine)
        {
            ManifestInformation manifest = FindProviderCache(providerId);
            List<uint> usedEvents = new List<uint>();

            foreach (var e in manifest.events.ChildNodes)
            {
                if (!(e is XmlElement))
                {
                    continue;
                }

                XmlElement ee = (XmlElement)e;

                if (!ee.HasAttribute("value"))
                {
                    continue;
                }

                uint value = Convert.ToUInt32(ee.GetAttribute("value"));
                usedEvents.Add(value);
            }

            for (uint i = 1; i < 16 * 1024 - 1; ++i)
            {
                if (!usedEvents.Contains(i))
                {
                    return i;
                }
            }

            throw new CLogEnterReadOnlyModeException("OutOfUniqueIds", sourceLine);
        }

        private void Save()
        {
            doc.Save(xmlFileName);
        }

        private List<TemplateNode> ConstructTemplateArgs(CLogDecodedTraceLine traceLine)
        {
            List<TemplateNode> listOfTemplateArgs = new List<TemplateNode>();

            foreach (var a2 in traceLine.splitArgs)
            {
                CLogFileProcessor.CLogVariableBundle arg = a2;
                CLogEncodingCLogTypeSearch node = traceLine.configFile.FindType(arg);
                TemplateNode templateNode = new TemplateNode();
                templateNode.ArgBundle = a2;
                templateNode.Name = a2.VariableInfo.SuggestedTelemetryName;

                switch (node.EncodingType)
                {
                    case CLogEncodingType.Int32:
                        templateNode.Type = "win:Int32";
                        templateNode.Hash = "i32_";
                        break;

                    case CLogEncodingType.UInt32:
                        templateNode.Type = "win:UInt32";
                        templateNode.Hash = "ui32_";
                        break;

                    case CLogEncodingType.Int64:
                        templateNode.Type = "win:Int64";
                        templateNode.Hash = "i64_";
                        break;

                    case CLogEncodingType.UInt64:
                        templateNode.Type = "win:UInt64";
                        templateNode.Hash = "ui64_";
                        break;

                    case CLogEncodingType.ANSI_String:
                        templateNode.Type = "win:AnsiString";
                        templateNode.Hash = "sz_";
                        break;

                    case CLogEncodingType.UNICODE_String:
                        templateNode.Type = "win:UnicodeString";
                        templateNode.Hash = "usz_";
                        break;

                    case CLogEncodingType.GUID:
                        templateNode.Type = "win:GUID";
                        templateNode.Hash = "g_";
                        break;

                    case CLogEncodingType.Pointer:
                        templateNode.Type = "win:Pointer";
                        templateNode.Hash = "ptr_";
                        break;

                    case CLogEncodingType.UInt16:
                        templateNode.Type = "win:UInt16";
                        templateNode.Hash = "ui16_";
                        break;

                    case CLogEncodingType.Int16:
                        templateNode.Type = "win:Int16";
                        templateNode.Hash = "i16_";
                        break;

                    case CLogEncodingType.UInt8:
                        templateNode.Type = "win:UInt8";
                        templateNode.Hash = "ui8_";
                        break;

                    case CLogEncodingType.Int8:
                        templateNode.Type = "win:Int8";
                        templateNode.Hash = "_i8";
                        break;

                    case CLogEncodingType.ByteArray:
                    {
                        templateNode.Type = "win:UInt8";
                        templateNode.Hash = "ui_8";
                        templateNode.Name += "_len";
                        listOfTemplateArgs.Add(templateNode);


                        templateNode = new TemplateNode();
                        templateNode.ArgBundle = a2;
                        templateNode.Name = a2.VariableInfo.SuggestedTelemetryName;
                        templateNode.LengthOfSelf = arg.VariableInfo.SuggestedTelemetryName + "_len";
                        templateNode.Type = "win:Binary";
                        templateNode.Hash = "binary_";
                    }
                        break;

                    case CLogEncodingType.Synthesized:
                        continue;

                    case CLogEncodingType.Skip:
                        continue;

                    default:
                        throw new Exception("Unknown Type: " + node);
                }

                listOfTemplateArgs.Add(templateNode);
            }

            return listOfTemplateArgs;
        }

        private string DiscoverOrCreateTemplate(CLogDecodedTraceLine traceLine, CLogSidecar sidecar, Guid providerId, string existingTemplateName, uint eventId)
        {
            string hash = "";

            //
            // Construct a list of the desired types - we'll use this to see if we can find a preexisting suitable template
            //
            List<TemplateNode> listofArgsAsSpecifiedBySourceFile = ConstructTemplateArgs(traceLine);
            string templateId = existingTemplateName;

            if (string.IsNullOrEmpty(existingTemplateName))
            {
                templateId = "template_" + hash;

                foreach (TemplateNode arg in listofArgsAsSpecifiedBySourceFile)
                {
                    templateId += arg.Hash;
                }
            }

            //
            // See if the template already exists;  for example from a different file
            //
            ManifestInformation manifest = FindProviderCache(providerId);
            XmlElement template = null;
            foreach (var p in manifest.templates.ChildNodes)
            {
                if (!(p is XmlElement))
                {
                    continue;
                }

                XmlElement pe = (XmlElement)p;

                if (pe.Name == "template")
                {
                    if (!pe.HasAttribute("tid"))
                    {
                        continue;
                    }

                    string tid = pe.GetAttribute("tid");

                    if (0 == tid.CompareTo(templateId))
                    {
                        template = pe;
                        break;
                    }
                }
            }

            //
            // If we dont have an existing template, add one
            //
            if (null == template)
            {
                template = doc.CreateElement("template", manifest.events.NamespaceURI);

                foreach (var arg in listofArgsAsSpecifiedBySourceFile)
                {
                    var dataNode = doc.CreateElement("data", manifest.events.NamespaceURI);
                    dataNode.SetAttribute("name", arg.Name);

                    if (!string.IsNullOrEmpty(arg.LengthOfSelf))
                        dataNode.SetAttribute("length", arg.LengthOfSelf);
                    dataNode.SetAttribute("inType", arg.Type);

                    template.AppendChild(dataNode);
                }

                template.SetAttribute("tid", templateId);
                manifest.templates.AppendChild(template);
            }

            //
            //
            int argIdx = 0;
            Dictionary<string, string> argLookup = sidecar.GetTracelineMetadata(traceLine, ModuleName);
            if (null == argLookup)
                argLookup = new Dictionary<string, string>();


            foreach (var a in template.ChildNodes)
            {
                if (!(a is XmlElement))
                    continue;

                XmlElement pe = (XmlElement)a;

                if (pe.Name != "data")
                    continue;

                string inType = pe.GetAttribute("inType");
                string name = pe.GetAttribute("name");
                TemplateNode templateReference = listofArgsAsSpecifiedBySourceFile[argIdx];

                argLookup[templateReference.ArgBundle.VariableInfo.SuggestedTelemetryName] = name;

                if (templateReference.Type != inType)
                {
                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, "Template Argument Type Mismatch: ");
                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, $"         Event ID : {eventId}");
                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, $"   Event Provider : {providerId}");
                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, $"        Event UID : {traceLine.UniqueId}");

                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, "");
                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, "");
                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, $"    Mismatch Name : {name}");
                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, $"    Expected Type : {templateReference.Type}");
                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, $"      Actual Type : {inType}");
                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, "Template Argument Type Mismatch: ");
                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, "");
                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, "");

                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Tip, "Recommended Course of action:");
                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Tip, $"  1. (best) from within the manifest, delete the template ({templateId}) from your event ({eventId})");
                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Tip, "  2. cleanup your template to be in this format");
                    foreach (var t in listofArgsAsSpecifiedBySourceFile)
                    {
                        if (string.IsNullOrEmpty(t.LengthOfSelf))
                            CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Tip, $"          name={t.Name} inType={t.Type}");
                        else
                            CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Tip, $"          name={t.Name} inType={t.Type}  length={t.LengthOfSelf}");
                    }

                    throw new CLogEnterReadOnlyModeException("ETWManifestTypeMismatch", traceLine.match);
                }


                ++argIdx;
            }


            //
            // Store our metadata into the side car
            //
            sidecar.SetTracelineMetadata(traceLine, ModuleName, argLookup);
            return templateId;
        }

        private class TemplateNode
        {
            public string Name { get; set; }
            public string Type { get; set; }

            public string LengthOfSelf { get; set; }
            public string Hash { get; set; }
            public CLogFileProcessor.CLogVariableBundle ArgBundle { get; set; }
        }

        private class ManifestInformation
        {
            public readonly XmlElement events;
            public readonly HashSet<string> knownHashes = new HashSet<string>();
            public readonly XmlElement templates;

            public ManifestInformation(XmlDocument doc, XmlElement provider)
            {
                events = provider["events"];
                templates = provider["templates"];
            }
        }
    }
}