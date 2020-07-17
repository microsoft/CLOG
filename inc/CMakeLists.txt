# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

add_custom_command(
    WORKING_DIRECTORY ${CLOG_SOURCE_DIRECTORY}
    COMMENT "Building CLOG and its support tooling"
    COMMENT "dotnet build ${CLOG_SOURCE_DIRECTORY}/clog.sln/clog.sln -o ${CMAKE_CLOG_BINS_DIRECTORY}"
    OUTPUT ${CMAKE_CLOG_BINS_DIRECTORY}/clog.dll
    COMMAND dotnet build ${CLOG_SOURCE_DIRECTORY}/clog.sln/clog.sln -o ${CMAKE_CLOG_BINS_DIRECTORY}
)

add_custom_target(
    CLOG
    DEPENDS ${CMAKE_CLOG_BINS_DIRECTORY}/clog.dll
)
