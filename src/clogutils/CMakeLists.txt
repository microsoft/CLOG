cmake_minimum_required(VERSION 3.10)


set(SOURCES
    CLogConsoleTrace.cs
    CLogCustomTraceEmittorFactory.cs
    CLogDecodedTraceLine.cs
    CLogEncodingCLogTypeSearch.cs
    CLogEncodingType.cs
    CLogEnterReadOnlyModeException.cs
    CLogErrors.cs
    CLogFileProcessor.cs
    CLogFullyDecodedMacroEmitter.cs
    CLogHandledException.cs
    CLogLineMatch.cs
    CLogSidecar.cs
    CLogTypeNotFoundException.cs
    CLogTypeSearchNode.cs
    ICLogOutputModule.cs)


DOT_NET_BUILD(clogutils.dll clogutils.csproj ${SOURCES})
add_custom_target(${library} DEPENDS ${project})