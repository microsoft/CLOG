# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

cmake_minimum_required(VERSION 3.10)

project(CLog)

set(CLOG_ARTIFACT_DIR ${PROJECT_BINARY_DIR}/artifacts)

function(DOT_NET_BUILD)
    set(DEPENDENCY_NAME ${ARGV0})
    set(DEST_BINARY ${CLOG_ARTIFACT_DIR}/${ARGV1})
    set(PROJECT_PATH ${ARGV2})
    set(DOTNET_FRAMEWORK ${ARGV3})
    list(REMOVE_AT ARGV 0)
    list(REMOVE_AT ARGV 0)
    list(REMOVE_AT ARGV 0)
    list(REMOVE_AT ARGV 0)

    message(STATUS "---------------------- GENERATING : ${DEPENDENCY_NAME} -------------------")
    message(STATUS "PROJECT : ${PROJECT_PATH}")
    message(STATUS "PRODUCES : ${DEST_BINARY}")
    message(STATUS "DOTNET_RUNTIME : ${DOTNET_RUNTIME}")
    message(STATUS "OUTPUTDIR : ${CLOG_ARTIFACT_DIR}")
    message(STATUS "DOTNET_FRAMEWORK : ${DOTNET_FRAMEWORK}")

    foreach(arg IN LISTS ARGV)
        #message(STATUS "FILE : ${CMAKE_CURRENT_BINARY_DIR}/${arg}")
        list(APPEND sFiles ${arg})
    endforeach()

    add_custom_command(
        OUTPUT
            ${DEST_BINARY}
        COMMENT
            "build CLOG, if this fails with nuget packages inspect your nuget.config and make sure nuget.org is present (add from command line using : dotnet nuget add source https://api.nuget.org/v3/index.json -n nuget.org)"
        COMMAND
            dotnet publish ${PROJECT_PATH} -o ${CLOG_ARTIFACT_DIR} -f ${DOTNET_FRAMEWORK}
        DEPENDS
            ${PROJECT_PATH}
        DEPENDS
            ${sFiles}
    )

    add_custom_target(${DEPENDENCY_NAME} ALL DEPENDS ${DEST_BINARY})
endfunction()

function(DOT_NET_LIBRARY_BUILD)
    set(DOTNET_RUNTIME "netstandard20")
    set(DEPENDENCY_NAME ${ARGV0})
    set(DEST_BINARY ${CLOG_ARTIFACT_DIR}/${DOTNET_RUNTIME}/${ARGV1})
    set(PROJECT_PATH ${ARGV2})

    list(REMOVE_AT ARGV 0)
    list(REMOVE_AT ARGV 0)
    list(REMOVE_AT ARGV 0)

    #message(STATUS "---------------------- GENERATING : ${DEPENDENCY_NAME} -------------------")
    #message(STATUS "PROJECT : ${PROJECT_PATH}")
    #message(STATUS "PRODUCES : ${DEST_BINARY}")
    #message(STATUS "OUTPUTDIR : ${CLOG_ARTIFACT_DIR}/${DOTNET_RUNTIME}")

    foreach(arg IN LISTS ARGV)
        #message(STATUS "FILE : ${CMAKE_CURRENT_BINARY_DIR}/${arg}")
        list(APPEND sFiles ${arg})
    endforeach()

    add_custom_command(
        OUTPUT
            ${DEST_BINARY}
        COMMENT
            "BUILDING ${DEST_BINARY} from "
        COMMAND
            dotnet build ${PROJECT_PATH} -o ${CLOG_ARTIFACT_DIR}/${DOTNET_RUNTIME}
        DEPENDS
            ${PROJECT_PATH}
        DEPENDS
            ${sFiles}
    )

    add_custom_target(${DEPENDENCY_NAME} ALL DEPENDS ${DEST_BINARY})
endfunction()

add_subdirectory(src/clog)
add_subdirectory(src/clogutils)

if (NOT APPLE)
    add_subdirectory(src/clog2text/clog2text_lttng)
endif()

if (WIN32)
    add_subdirectory(src/clog2text/clog2text_windows)
endif()
