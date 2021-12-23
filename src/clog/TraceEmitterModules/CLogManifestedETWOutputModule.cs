/*++

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
using System.Xml;
using clogutils;
using clogutils.MacroDefinations;

namespace clog.TraceEmitterModules
{
    public class CLogManifestedETWOutputModule : ICLogOutputModule
    {
        private bool _inited = false;
        private readonly Dictionary<Guid, ManifestInformation> _providerCache = new Dictionary<Guid, ManifestInformation>();

        public XmlDocument doc = new XmlDocument();
        
        public string xmlFileName;
        private static string _ModuleName = "MANIFESTED_ETW";
        private bool _dirty = false;
        private bool _readOnlyMode = false;

        public CLogManifestedETWOutputModule(bool inReadOnlyMode)
        {
            _readOnlyMode = inReadOnlyMode;
        }

        public string ModuleName
        {
            get { return _ModuleName; }
        }

        public bool ManditoryModule
        {
            get { return false; }
        }

        public void FinishedProcessing(CLogOutputInfo outputInfo, StringBuilder header, StringBuilder sourceFile)
        {
        }

        public void InitHeader(StringBuilder header)
        {
        }

        private string MapCLOGStringToManifestString(CLogDecodedTraceLine decodedTraceLine)
        {
            return decodedTraceLine.CleanedString;
        }

        private void SetAttribute(XmlElement e, string attribute, string newValue)
        {
            if (e.HasAttribute(attribute))
            {
                if (e.GetAttribute(attribute).Equals(newValue))
                    return;
            }

            _dirty = true;
            e.SetAttribute(attribute, newValue);
        }

        public void TraceLineDiscovered(string sourceFile, CLogOutputInfo outputInfo, CLogDecodedTraceLine decodedTraceLine, CLogSidecar sidecar, StringBuilder macroPrefix, StringBuilder inline, StringBuilder function)
        {
            string hash = decodedTraceLine.UniqueId;
            CLogExportModuleDefination moduleSettings = decodedTraceLine.GetMacroConfigurationProfile().FindExportModule(_ModuleName);

            if (!_inited)
            {
                if (!moduleSettings.CustomSettings.ContainsKey("ETWManifestFile"))
                    throw new CLogEnterReadOnlyModeException("ETWManifestFileNotSpecified", CLogHandledException.ExceptionType.MustSpecifiyETWManifest, decodedTraceLine.match);

                xmlFileName = moduleSettings.CustomSettings["ETWManifestFile"];
                xmlFileName = Path.Combine(Path.GetDirectoryName(decodedTraceLine.macro.ConfigFileWithMacroDefination), xmlFileName);

                Init();
            }

            if (!moduleSettings.CustomSettings.ContainsKey("ETW_Provider"))
            {
                Console.WriteLine($"The 'CustomSettings' dictionary for macro {decodedTraceLine.macro.MacroName} does not contain a GUID for the EtwProvider");
                Console.WriteLine("    Please add an entry and rerun");
                Console.WriteLine("");
                Console.WriteLine($"Configuration File  : {decodedTraceLine.configFile.FilePath}");
                Console.WriteLine("");
                Console.WriteLine("");
                Console.WriteLine("");
                throw new CLogEnterReadOnlyModeException("ETW_Provider:NotSpecified", CLogHandledException.ExceptionType.MustSpecifyETWProvider, decodedTraceLine.match);
            }

            Guid providerId = new Guid(moduleSettings.CustomSettings["ETW_Provider"]);

            ManifestInformation manifest = FindProviderCache(providerId);
            string eventNamePrefix;
            if (!moduleSettings.CustomSettings.TryGetValue("EventNamePrefix", out eventNamePrefix))
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
                throw new CLogEnterReadOnlyModeException("ManifestedETWProviderNotFoundInManifest", CLogHandledException.ExceptionType.ManifestedETWProviderNotFound, decodedTraceLine.match);
            }

            //
            //  See if our event already exists - if it does we do not want to add it a second time
            //
            List<XmlElement> toRemove = new List<XmlElement>();
            XmlElement newEvent = null;

            if(null == manifest.events)
                throw new CLogEnterReadOnlyModeException("ManifestedETW does not contain 'events' node", CLogHandledException.ExceptionType.ManifestedETWFileDoesntContainEvents, decodedTraceLine.match);


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

            //
            //  Add the event if it doesnt already exist
            //
            if (null == newEvent)
            {
                newEvent = doc.CreateElement("event", manifest.events.NamespaceURI);
                manifest.events.AppendChild(newEvent);
                _dirty = true;
                CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Tip, $"Adding event {eventNamePrefix + hash} to ETW manifest {xmlFileName}");


                //
                // Set the string
                //
                bool setString = true;
                if (moduleSettings.CustomSettings.ContainsKey("EmitString") && moduleSettings.CustomSettings["EmitString"].Equals("0"))
                    setString = false;

                if(setString)
                {
                    string stringName = "CLOG." + hash;
                    string manifestString = MapCLOGStringToManifestString(decodedTraceLine);

                    var stringEntry = doc.CreateElement("string", manifest.stringTable.NamespaceURI);
                    manifest.stringTable.AppendChild(stringEntry);
                    _dirty = true;
                    SetAttribute(stringEntry, "id", stringName);
                    SetAttribute(stringEntry, "value", manifestString);

                    SetAttribute(newEvent, "message", "$(string." + stringName + ")");
                }
            }

            int hashUInt;
            string eventAsString;

            decodedTraceLine.macro.DecodeUniqueId(decodedTraceLine.match, hash, out eventAsString, out hashUInt);

            uint eventId;
            if (!newEvent.HasAttribute("value"))
            {
                eventId = FindUnusedEventId(providerId, decodedTraceLine.match);
                SetAttribute(newEvent, "value", eventId.ToString());
            }
            else
            {
                eventId = Convert.ToUInt32(newEvent.GetAttribute("value"));
            }

            //
            // Store the eventID for future decode as well as every configuration setting attached to this module
            //
            decodedTraceLine.AddConfigFileProperty(ModuleName, "EventID", eventId.ToString());
            foreach (var setting in moduleSettings.CustomSettings)
            {
                decodedTraceLine.AddConfigFileProperty(ModuleName, setting.Key, setting.Value);
            }


            SetAttribute(newEvent, "symbol", eventNamePrefix + hash);

            string oldTemplate = null;
            if (newEvent.HasAttribute("template"))
                oldTemplate = newEvent.GetAttribute("template");
            string templateId = DiscoverOrCreateTemplate(decodedTraceLine, sidecar, providerId, oldTemplate, eventId);
            SetAttribute(newEvent, "template", templateId);

            if (moduleSettings.CustomSettings.ContainsKey("Level"))
                SetAttribute(newEvent, "level", moduleSettings.CustomSettings["Level"]);
            else
                CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Wrn, $"Manifested ETW Level not specified;  if you desire a Level, add 'Level' to CustomSettings in {decodedTraceLine.configFile.FilePath}");

            if (moduleSettings.CustomSettings.ContainsKey("Keywords"))
                SetAttribute(newEvent, "keywords", moduleSettings.CustomSettings["Keywords"]);
            else
                CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Wrn, $"Manifested ETW Keywords not specified;  if you desire a Keyword, add 'Keywords' to CustomSettings in {decodedTraceLine.configFile.FilePath}");




            //
            // Construct the function signature
            //
            string traceLine = $"EventWrite{eventNamePrefix + hash}(";
            bool haveMultipleArgs = false;

            foreach (var a in decodedTraceLine.splitArgs)
            {
                CLogFileProcessor.CLogVariableBundle arg = a;

                if (!arg.TypeNode.IsEncodableArg)
                    continue;

                CLogEncodingCLogTypeSearch node = decodedTraceLine.configFile.FindType(arg, decodedTraceLine);

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
            Save(decodedTraceLine.match);
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
                throw new CLogEnterReadOnlyModeException("Output Manifest Missing", CLogHandledException.ExceptionType.ETWManifestNotFound, null);
            }

            try
            {

                doc.PreserveWhitespace = true;
                doc.Load(xmlFileName);

                XmlElement assembly = doc["assembly"];
                XmlElement stringTable = null;

                if (null == assembly)
                {
                    assembly = doc["instrumentationManifest"];
                }

                var instrumentation = assembly["instrumentation"];
                var rootEvents = instrumentation["events"];

                var stringEvents = assembly["localization"];
                foreach (var culture in stringEvents.ChildNodes)
                {
                    if (!(culture is XmlElement))
                    {
                        continue;
                    }

                    XmlElement pe = (XmlElement)culture;
                    if (pe.Name == "resources")
                    {
                        if (!pe.HasAttribute("culture"))
                        {
                            continue;
                        }

                        string attr = pe.GetAttribute("culture");

                        if (!attr.Equals("en-US"))
                            continue;

                        stringTable = pe["stringTable"];
                    }
                }

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
                            _providerCache[id] = new ManifestInformation(doc, pe, stringTable);
                        }
                    }
                }

                _inited = true;

            }
            catch(Exception)
            {
                CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, $"Error processing ETW manifest {xmlFileName}");
                throw;
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

        private uint FindUnusedEventId(Guid providerId, CLogLineMatch sourceLine)
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

            throw new CLogEnterReadOnlyModeException("OutOfUniqueIds", CLogHandledException.ExceptionType.ETWOutOfUniqueIDs, sourceLine);
        }

        private void Save(CLogLineMatch match)
        {
            if (!_dirty)
                return;

            if (_readOnlyMode)
            {
                throw new CLogEnterReadOnlyModeException("WontWriteWhileInReadonlyMode:ETWManifest", CLogHandledException.ExceptionType.WontWriteInReadOnlyMode, match);
            }

            //
            // Devs prefer XML that is readable in their editor - make an attempt to format their XML in a way that diffs okay
            //
            StringBuilder stringBuilder = new StringBuilder();
            System.Xml.Linq.XElement element = System.Xml.Linq.XElement.Parse(doc.InnerXml);

            XmlWriterSettings settings = new XmlWriterSettings();
            settings.OmitXmlDeclaration = true;
            settings.Indent = true;
            settings.NewLineOnAttributes = true;

            using (var xmlWriter = XmlWriter.Create(stringBuilder, settings))
            {
                element.Save(xmlWriter);
            }
            File.WriteAllText(xmlFileName, stringBuilder.ToString());

            _dirty = false;
        }

        private List<TemplateNode> ConstructTemplateArgs(CLogDecodedTraceLine traceLine)
        {
            List<TemplateNode> listOfTemplateArgs = new List<TemplateNode>();

            foreach (var a2 in traceLine.splitArgs)
            {
                CLogFileProcessor.CLogVariableBundle arg = a2;

                if (!arg.TypeNode.IsEncodableArg)
                    continue;

                CLogEncodingCLogTypeSearch node = traceLine.configFile.FindType(arg, traceLine);
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
                        templateNode.Hash = "i8_";
                        break;

                    case CLogEncodingType.ByteArray:
                        {
                            templateNode.Type = "win:UInt8";
                            templateNode.Hash = "ui8_";
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

                // Only apply a template ID if it's not empty - otherwise choose the default
                if (!templateId.Equals("templateId_"))
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

                if (listofArgsAsSpecifiedBySourceFile.Count <= argIdx)
                {
                    if (traceLine.configFile.DeveloperMode)
                    {
                        CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, "Template Argument Type Mismatch - overwriting due to developer mode");
                        _dirty = true;
                        return DiscoverOrCreateTemplate(traceLine, sidecar, providerId, null, eventId);
                    }

                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, "Template Argument Type Mismatch - manifested ETW template and CLOG string differ");
                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, $"         Event ID : {eventId}");
                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, $"   Event Provider : {providerId}");
                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, $"        Event UID : {traceLine.UniqueId}");

                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Tip, "Recommended Course of action:");
                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Tip, $"  1. (best) from within the manifest, delete the template ({templateId}) from your event ({eventId})");
                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Tip, $"  2. cleanup your template to be in this format");
                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Tip, $"  3. set the environment variable CLOG_DEVELOPMENT_MODE=1");
                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Tip, $"      cmd.exe:   set CLOG_DEVELOPMENT_MODE=1");
                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Tip, $"      PowerShell: $env:CLOG_DEVELOPMENT_MODE=1");
                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Tip, $"      BASH: export CLOG_DEVELOPMENT_MODE=1");

                    throw new CLogEnterReadOnlyModeException("ETWManifestTypeMismatch", CLogHandledException.ExceptionType.ETWTypeMismatch, traceLine.match);
                }

                TemplateNode templateReference = listofArgsAsSpecifiedBySourceFile[argIdx];

                argLookup[templateReference.ArgBundle.VariableInfo.SuggestedTelemetryName] = name;

                if (templateReference.Type != inType)
                {
                    if (traceLine.configFile.DeveloperMode)
                    {
                        CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, "Template Argument Type Mismatch - overwriting due to developer mode");
                        _dirty = true;
                        return DiscoverOrCreateTemplate(traceLine, sidecar, providerId, null, eventId);
                    }

                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, "Template Argument Type Mismatch: ");
                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, $"               Event ID : {eventId}");
                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, $"         Event Provider : {providerId}");
                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, $"              Event UID : {traceLine.UniqueId}");

                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, "");
                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, "");
                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, $"      Mismatch Arg Name : {name}");
                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, $"    CLOG specified Type : {templateReference.Type}");
                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, $"      ETW Manifest Type : {inType}");
                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, "");
                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, "");
                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, "Source Line:");
                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, "");
                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, CLogConsoleTrace.GetFileLine(traceLine.match));
                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, traceLine.match.MatchedRegExX.ToString());

                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, "");
                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, "");

                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Tip, "Recommended Course of action:");
                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Tip, $"  1. (best) from within the manifest, delete the template ({templateId}) from your event ({eventId})");
                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Tip, $"  2. cleanup your template to be in this format");
                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Tip, $"  3. set the environment variable CLOG_DEVELOPMENT_MODE=1");
                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Tip, $"      cmd.exe:   set CLOG_DEVELOPMENT_MODE=1");
                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Tip, $"      PowerShell: $env:CLOG_DEVELOPMENT_MODE=1");
                    CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Tip, $"      BASH: export CLOG_DEVELOPMENT_MODE=1");


                    foreach (var t in listofArgsAsSpecifiedBySourceFile)
                    {
                        if (string.IsNullOrEmpty(t.LengthOfSelf))
                            CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Tip, $"          name={t.Name} inType={t.Type}");
                        else
                            CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Tip, $"          name={t.Name} inType={t.Type}  length={t.LengthOfSelf}");
                    }

                    throw new CLogEnterReadOnlyModeException("ETWManifestTypeMismatch", CLogHandledException.ExceptionType.ETWTypeMismatch, traceLine.match);
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
            public readonly XmlElement templates;
            public readonly XmlElement stringTable;

            public ManifestInformation(XmlDocument doc, XmlElement provider, XmlElement strngTable)
            {
                events = provider["events"];
                templates = provider["templates"];
                stringTable = strngTable;
            }
        }
    }
}
