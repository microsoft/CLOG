# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

cmake_minimum_required(VERSION 3.10)

project(CLog)

set(CLOG_ARTIFACT_DIR ${PROJECT_BINARY_DIR}/artifacts)

function(DOT_NET_BUILD)
    set(DEPENDENCY_NAME ${ARGV0})
    set(DEST_BINARY ${ARGV1})
    set(PROJECT_PATH ${ARGV2})
    list(REMOVE_AT ARGV 0)
    list(REMOVE_AT ARGV 0)
    list(REMOVE_AT ARGV 0)

    if (WIN32)
        set(DOTNET_RUNTIME "win-x64")
    elseif (UNIX)
        set(DOTNET_RUNTIME "linux-x64")
    endif()


    message(STATUS "---------------------- GENERATING : ${DEPENDENCY_NAME} -------------------")
    message(STATUS "PROJECT : ${PROJECT_PATH}")
    message(STATUS "PRODUCES : ${DEST_BINARY}")
    message(STATUS "DOTNET_RUNTIME : ${DOTNET_RUNTIME}")
    message(STATUS "OUTPUTDIR : ${CLOG_ARTIFACT_DIR}/${DOTNET_RUNTIME}")

    foreach(arg IN LISTS ARGV)
        message(STATUS "FILE : ${CMAKE_CURRENT_BINARY_DIR}/${arg}")
        list(APPEND sFiles ${arg})
    endforeach()

    add_custom_command(
        OUTPUT
            ${DEST_BINARY}
        COMMENT
            "BUILDING ${DEST_BINARY} from "
        COMMAND
            dotnet publish ${PROJECT_PATH} --self-contained -o ${CLOG_ARTIFACT_DIR}/${DOTNET_RUNTIME} -f net5.0 -r ${DOTNET_RUNTIME}
        DEPENDS
            ${PROJECT_PATH}
        DEPENDS
            ${sFiles}
    )

    add_custom_target(${DEPENDENCY_NAME} ALL DEPENDS ${DEST_BINARY})
endfunction()


add_subdirectory(src/clog)
add_subdirectory(src/clogutils)
add_subdirectory(src/clog2text/clog2text_lttng)
add_subdirectory(src/clog2text/clog2text_windows)
