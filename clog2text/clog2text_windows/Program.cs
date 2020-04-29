﻿/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    Reads from Manifested ETW (via Microsoft.Windows.EventTracing aka 'DataLayer' ) and converts into CLOG property bag.  This bag is then sent into a generic CLOG function for output to STDOUT

    ETL file containing ETW data -> DataLayer -> clog2text_windows -> generic clog STDOUT

--*/

using clogutils;
using CommandLine;
using Microsoft.Windows.EventTracing;
using Microsoft.Windows.EventTracing.Events;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static clogutils.CLogConsoleTrace;

namespace clog2text_windows
{
    internal class Program
    {
        private static int Main(string[] cmdLineArgs)
        {
            ParserResult<CommandLineArguments> o = Parser.Default.ParseArguments<CommandLineArguments>(cmdLineArgs);

            return o.MapResult(
                options =>
                {
                    string sidecarJson = File.ReadAllText(options.SideCarFile);
                    CLogSidecar sidecar = CLogSidecar.FromJson(sidecarJson);


                    TextReader file = Console.In;

                    if (!File.Exists(options.ETLFile))
                    {
                        TraceLine(TraceType.Err, $"ETL File {options.ETLFile} doesnt exist");
                        return -1;
                    }

                    StreamWriter outputfile = null;
                    if (!String.IsNullOrEmpty(options.OutputFile))
                        outputfile = new StreamWriter(new FileStream(options.OutputFile, FileMode.Create));

                    try
                    {
                        TraceProcessorSettings traceSettings = new TraceProcessorSettings { AllowLostEvents = true, AllowTimeInversion = true };

                        using (ITraceProcessor etwfile = TraceProcessor.Create(options.ETLFile, traceSettings))
                        {
                            HashSet<Guid> ids = new HashSet<Guid>();

                            foreach (var m in sidecar.EventBundlesV2)
                            {
                                foreach (var prop in m.Value.ModuleProperites)
                                {
                                    if (prop.Key.Equals("MANIFESTED_ETW"))
                                    {
                                        ids.Add(new Guid(prop.Value["ETW_Provider"]));
                                    }
                                    else if (prop.Key.Equals("TRACELOGGING"))
                                    {
                                        ids.Add(new Guid(prop.Value["ETW_Provider"]));
                                    }
                                }
                            }

                            var events = etwfile.UseGenericEvents(ids.ToArray());
                            etwfile.Process();

                            foreach (var e in events.Result.Events)
                            {
                                string line = "";

                                try
                                {
                                    Dictionary<string, IClogEventArg> fixedUpArgs = new Dictionary<string, IClogEventArg>();
                                    string errorString = "ERROR";

                                    if (null == e.Fields)
                                    {
                                        continue;
                                    }

                                    Dictionary<string, IClogEventArg> args = new Dictionary<string, IClogEventArg>();

                                    foreach (var f in e.Fields)
                                    {
                                        args[f.Name] = new ManifestedETWEvent(f);
                                    }

                                    CLogDecodedTraceLine bundle = null;
                                    int eidAsInt = -1;

                                    foreach (var b in sidecar.EventBundlesV2)
                                    {
                                        Dictionary<string, string> keys;

                                        if (!e.IsTraceLogging)
                                        {
                                            if (!b.Value.ModuleProperites.TryGetValue("MANIFESTED_ETW", out keys))
                                            {
                                                continue;
                                            }

                                            string eid;

                                            if (!keys.TryGetValue("EventID", out eid))
                                            {
                                                continue;
                                            }

                                            eidAsInt = Convert.ToInt32(eid);

                                            if (eidAsInt == e.Id)
                                            {
                                                bundle = b.Value;
                                                errorString = "ERROR:" + eidAsInt;
                                                break;
                                            }
                                        }
                                        else
                                        {
                                            if (e.ActivityName.Equals(b.Key))
                                            {
                                                bundle = b.Value;
                                                errorString = "ERROR:" + b.Key;
                                                break;
                                            }
                                        }
                                    }

                                    if (null == bundle)
                                    {
                                        continue;
                                    }

                                    Dictionary<string, string> argMap;

                                    if (e.IsTraceLogging)
                                    {
                                        argMap = new Dictionary<string, string>();
                                        foreach (var arg in args)
                                        {
                                            argMap[arg.Key] = arg.Key;
                                        }
                                    }
                                    else
                                    {
                                        argMap = sidecar.GetTracelineMetadata(bundle, "MANIFESTED_ETW");
                                    }

                                    var types = CLogFileProcessor.BuildTypes(sidecar.ConfigFile, null, bundle.TraceString, null, out string clean);

                                    if (0 == types.Length)
                                    {
                                        errorString = bundle.TraceString;
                                        goto toPrint;
                                    }

                                    int argIndex = 0;

                                    foreach (var type in types)
                                    {
                                        var arg = bundle.splitArgs[argIndex];
                                        CLogEncodingCLogTypeSearch node = sidecar.ConfigFile.FindType(arg);

                                        switch (node.EncodingType)
                                        {
                                            case CLogEncodingType.Synthesized:
                                                continue;

                                            case CLogEncodingType.Skip:
                                                continue;
                                        }

                                        string lookupArgName = argMap[arg.VariableInfo.SuggestedTelemetryName];

                                        if (!args.ContainsKey(lookupArgName))
                                        {
                                            Console.WriteLine($"Argmap missing {lookupArgName}");
                                            throw new Exception("InvalidType : " + node.DefinationEncoding);
                                        }

                                        if (0 != node.DefinationEncoding.CompareTo(type.TypeNode.DefinationEncoding))
                                        {
                                            Console.WriteLine("Invalid Types in Traceline");
                                            throw new Exception("InvalidType : " + node.DefinationEncoding);
                                        }

                                        fixedUpArgs[arg.VariableInfo.SuggestedTelemetryName] = args[lookupArgName];
                                        ++argIndex;
                                    }

                                toPrint:
                                    DecodeAndTraceToConsole(outputfile, bundle, errorString, sidecar.ConfigFile, fixedUpArgs);
                                }
                                catch (Exception)
                                {
                                    Console.WriteLine($"Invalid TraceLine : {line}");
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        CLogConsoleTrace.TraceLine(TraceType.Err, "ERROR : " + e);
                        if (null != outputfile)
                        {
                            outputfile.WriteLine("ERROR : " + e);
                        }
                    }
                    finally
                    {
                        if (null != outputfile)
                        {
                            outputfile.Flush();
                            outputfile.Close();
                        }
                    }
                    return 0;
                }, err =>
                {
                    Console.WriteLine("Bad Args : " + err);
                    return -1;
                });
        }

        public class ManifestedETWEvent : IClogEventArg
        {
            private readonly IGenericEventField _event;

            public ManifestedETWEvent(IGenericEventField e)
            {
                _event = e;
            }

            public byte[] AsBinary
            {
                get
                {
                    switch (_event.Type)
                    {
                        case GenericEventFieldType.ByteList:
                            return _event.AsByteList.ToArray();
                        case GenericEventFieldType.Binary:
                            return _event.AsBinary.ToArray();
                        default:
                            throw new InvalidDataException("Invalid ETW Encoding : " + _event.Type);
                    }
                }
            }

            public ulong AsPointer
            {
                get
                {
                    switch (_event.Type)
                    {
                        case GenericEventFieldType.UInt64:
                            return _event.AsUInt64;
                        case GenericEventFieldType.Address:
                            return (ulong)_event.AsAddress.Value;
                        default:
                            throw new InvalidDataException("InvalidTypeForPointer:" + _event.Type);
                    }
                }
            }

            public string AsString
            {
                get { return _event.ToString(); }
            }

            public int AsInt32
            {
                get { return _event.AsInt32; }
            }

            public uint AsUInt32
            {
                get { return _event.AsUInt32; }
            }
        }
    }
}
