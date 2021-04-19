cmake_minimum_required(VERSION 3.10)

project(CLog)

function(DOT_NET_BUILD)
    set(library ${ARGV0})
    set(project ${ARGV1})
    list(REMOVE_AT ARGV 0)
    list(REMOVE_AT ARGV 0)

    message(STATUS "BUILDING : ${library} coming from ${ARGV}")
    message(STATUS "PROJECT : ${project}")
    message(STATUS "dotnet publish ${project} --self-contained -f net5.0 -r win-x64")

    add_custom_command(
        OUTPUT
            "${CMAKE_CURRENT_BINARY_DIR}/${library}"
        COMMENT
            "BUILDING ${CMAKE_CURRENT_BINARY_DIR}/${library} from ${CMAKE_CURRENT_SOURCE_DIR}/clogutils.csproj"
        COMMAND
            dotnet build ${CMAKE_CURRENT_SOURCE_DIR}/clogutils.csproj -o ${CMAKE_CURRENT_BINARY_DIR}
        DEPENDS
            ${CMAKE_CURRENT_SOURCE_DIR}/clogutils.csproj
        DEPENDS
            ${CMAKE_CURRENT_SOURCE_DIR}/CLogFileProcessor.cs
        DEPENDS
            ${ARGV}
    )

    #add_custom_target(${library} DEPENDS ${project})
endfunction()


add_subdirectory(src/clog)
add_subdirectory(src/clogutils)