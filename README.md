# Revisions
## 0.1.0 ALPHA
* Initial drop
* Released for purposes of starting conversations
* We expecting substantial (breaking) changes in short order;  please do not take a dependence until verson 1.0.0
* We're using semver [as a guiding light](https://semver.org/spec/v2.0.0.html)


# Introduction
Within the tracing/telemetry space there are many API choices and (even if it's possible) it’s very difficult to choose correctly. The Microsoft QUIC project is in need of the ability to emit logging events cross platform, even in high performance data paths.

Without introducing any new APIs for bit encoding, and exclusively using legacy encoders and tooling (ETW on Windows, LTTNG on linux) CLOG provides:

* durable ID's, which are required for telemetry pipelines to trigger collections upon failure
* manifested args, with type safety - necessary to make the decisions for triggering
* clear, human readable text in a STDOUT log format
* ability to use existing tooling;  like WPA

# CLOG makes these claims:

1. Code and tool assets need to be decoupled from bit encoders to preserve the code/tools assets as bit encoders are modified (as would happen as code moves between OS’s or projects)
API abstractions not a sufficient solution to our problems, due to performance. Worse, API’s tend to create permanent couplings between code and tools due to those couplings.
Clog argues a tool (and not a library) best solves the range of problems we face

2. Event description(manifests), for purposes of tooling needs to be decoupled from description for human presentation (text files). For example the [Windows Performance Analyzer (WPA)](https://docs.microsoft.com/en-us/windows-hardware/test/wpt/windows-performance-analyzer) have different needs as compared to a human reading a log.


# CLOG holds these claims by:

* Using a “printf like” syntax for event description
    ```cpp
    TraceLogWarning(
        DroppedPacket,
        "[conn][%p] DROP packet[%I] Dst=%!IPADDR! Src=%!IPADDR! Reason=%s.",
        Owner,
        Packet->PacketNumberSet ? UINT64_MAX : Packet->PacketNumber,
        CLOG_BYTEARRAY(LOG_ADDR_LEN(Datagram->Tuple->LocalAddress), (uint8_t*)&Datagram->Tuple->LocalAddress),
        CLOG_BYTEARRAY(LOG_ADDR_LEN(Datagram->Tuple->RemoteAddress), (uint8_t*)&Datagram->Tuple->RemoteAddress),
        Reason);
    ```


* Selecting a bit encoder based on build time tool parameters, not API configuration. in this way we're not adding yet another API surface within any OS


    ```json
    {
          "MacroName": "TraceLogWarning",
          "EncodedPrefix": null,
          "EncodedArgNumber": 1,
          "CustomSettings": {
            "WPPArgNumber": "0",
            "ETWManifestFile": "Component.man",
            "ETW_Provider": "440d0192-e016-49e9-8f9a-639f5b275ab0"
          },
          "CLogExportModules": [
            "MANIFESTED_ETW",
            "TRACELOGGING",
            "DTRACE"
          ]
    }
    ```


* Deferring the choice of event descriptions (manifest), for tooling. This maximizes utility of existing tooling (like WPA). The author chooses, at build time, how their events are manifested based on their needs – the decision can be reevaluated in the future without making changes to code or tools
Requiring a “side car” (external manifest) for event presentation to a human (eg : "[conn][%p] DROP packet[%I] Dst=%!IPADDR! Src=%!IPADDR! Reason=%s." )


# Tools and Modules
*Note the .exe extension is provided for clarity,  clog is cross platform and on Linux the .exe will not be present*

## Work flow

1. at build time, your c/cpp file will be read and processed using a config file.  clog.exe produces .c file and .h file that contains OS specific code
2. Using your favorite OS collection mechanism, collect traces using the appropriate tools
3. For debugging, convert your recorded traces into something human readable using one of the clog2text_{windows,lttng,etc} tools


## Modules

* clog.exe
    
    Build time tool to generate source code based on regex parsing of C/C++ code.  This is the main tool for clog.

* clog2text_lttng.exe

    LINUX ONLY : Combines CLOG events that are stored with LTTNG into human readable text

* clog2text_windows.exe

    WINDOWS ONLY : Combines CLOG events that are stored with ETW into human readable text 
    
* clogutils.dll

    Utility library used between clog.exe and other clog tools.

# Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.opensource.microsoft.com.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
