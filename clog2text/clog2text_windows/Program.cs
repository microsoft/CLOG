/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    Reads from Manifested ETW (via Microsoft.Windows.EventTracing aka 'DataLayer' ) and converts into CLOG property bag.  This bag is then sent into a generic CLOG function for output to STDOUT

    ETL file containing ETW data -> DataLayer -> clog2text_windows -> generic clog STDOUT

--*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using clogutils;
using clogutils.ConfigFile;
using CommandLine;
using Microsoft.Windows.EventTracing;
using Microsoft.Windows.EventTracing.Events;
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
                    CLogSidecar textManifest = CLogSidecar.FromJson(sidecarJson);
                    CLogConfigurationFile config = new CLogConfigurationFile();
                    config.TypeEncoders = textManifest.TypeEncoder;


                    TextReader file = Console.In;

                    if (!File.Exists(options.ETLFile))
                    {
                        TraceLine(TraceType.Err, $"ETL File {options.ETLFile} doesnt exist");
                        return -1;
                    }

                    TraceProcessorSettings traceSettings = new TraceProcessorSettings {AllowLostEvents = true, AllowTimeInversion = true};

                    using (ITraceProcessor etwfile = TraceProcessor.Create(options.ETLFile, traceSettings))
                    {
                        HashSet<Guid> ids = new HashSet<Guid>();

                        foreach (var m in textManifest.EventBundlesV2)
                        {
                            ids.Add(new Guid(m.Value.macro.CustomSettings["ETW_Provider"]));
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

                                foreach (var b in textManifest.EventBundlesV2)
                                {
                                    Dictionary<string, string> keys;

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

                                if (null == bundle)
                                {
                                    continue;
                                }

                                Dictionary<string, string> argMap = textManifest.GetTracelineMetadata(bundle, "MANIFESTED_ETW");
                                var types = CLogFileProcessor.BuildTypes(config, null, bundle.TraceString, null, out string clean);

                                if (0 == types.Length)
                                {
                                    errorString = bundle.TraceString;
                                    goto toPrint;
                                }

                                int argIndex = 0;

                                foreach (var type in types)
                                {
                                    var arg = bundle.splitArgs[argIndex];
                                    CLogEncodingCLogTypeSearch node = config.FindType(arg, null);

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
                                DecodeAndTraceToConsole(bundle, errorString, config, fixedUpArgs);
                            }
                            catch (Exception)
                            {
                                Console.WriteLine($"Invalid TraceLine : {line}");
                            }
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
                get { return _event.AsBinary.ToArray(); }
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
