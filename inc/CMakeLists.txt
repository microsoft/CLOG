# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

add_custom_command(
    WORKING_DIRECTORY ${CLOG_SOURCE_DIRECTORY}
    COMMENT "Building CLOG and its support tooling"
    COMMENT "dotnet build ${CLOG_SOURCE_DIRECTORY}/clog.sln/clog_coreclr.sln -o ${CMAKE_CLOG_BINS_DIRECTORY}"
    OUTPUT ${CMAKE_CLOG_BINS_DIRECTORY}/clog.dll
    COMMAND dotnet build ${CLOG_SOURCE_DIRECTORY}/clog.sln/clog_coreclr.sln -o ${CMAKE_CLOG_BINS_DIRECTORY}
)

if ("${CMAKE_CXX_COMPILER_ID}" STREQUAL "MSVC")
    add_custom_command(
        WORKING_DIRECTORY ${CLOG_SOURCE_DIRECTORY}
        COMMENT "Building CLOG and its support tooling"
        COMMENT "msbuild -t:restore ${CLOG_SOURCE_DIRECTORY}/clog.sln/clog_windows.sln"
        COMMENT "msbuild ${CLOG_SOURCE_DIRECTORY}/clog.sln/clog_windows.sln /p:OutDir=${CMAKE_CLOG_BINS_DIRECTORY}/windows"
        OUTPUT ${CMAKE_CLOG_BINS_DIRECTORY}/windows/clog2text_windows.exe
        COMMAND msbuild -t:restore ${CLOG_SOURCE_DIRECTORY}/clog.sln/clog.sln
        COMMAND msbuild ${CLOG_SOURCE_DIRECTORY}/clog.sln/clog.sln /p:OutDir=${CMAKE_CLOG_BINS_DIRECTORY}/windows
    )

    add_custom_target(
        CLOG
        DEPENDS ${CMAKE_CLOG_BINS_DIRECTORY}/clog.dll
        DEPENDS ${CMAKE_CLOG_BINS_DIRECTORY}/windows/clog2text_windows.exe
    )

else()
    add_custom_target(
        CLOG
        DEPENDS ${CMAKE_CLOG_BINS_DIRECTORY}/clog.dll
    )
endif()
