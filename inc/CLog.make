function(CLOG_ADD_SOURCEFILE)
    set(library ${ARGV0})
    list(REMOVE_AT ARGV 0)
     # message(STATUS "****************<<<<<<<   CLOG(${library}))    >>>>>>>>>>>>>>>*******************")
     # message(STATUS ">>>> CLOG_SOURCE_DIRECTORY = ${CLOG_SOURCE_DIRECTORY}")
     # message(STATUS ">>>> CMAKE_CURRENT_SOURCE_DIR = ${CMAKE_CURRENT_SOURCE_DIR}")
     # message(STATUS ">>>> CMAKE_CLOG_BINS_DIRECTORY = ${CMAKE_CLOG_BINS_DIRECTORY}")
     # message(STATUS ">>>> CMAKE_CLOG_SIDECAR_DIRECTORY = ${CMAKE_CLOG_SIDECAR_DIRECTORY}")
     # message(STATUS ">>>> CMAKE_CLOG_CONFIG_PROFILE = ${CMAKE_CLOG_CONFIG_PROFILE}")
     # message(STATUS ">>>> CLOG Library = ${library}")
     # message(STATUS ">>>> CMAKE_CXX_COMPILER_ID = ${CMAKE_CXX_COMPILER_ID}")

    add_custom_command(
        WORKING_DIRECTORY ${CLOG_SOURCE_DIRECTORY}
        COMMENT "Building CLOG and its support tooling"
        COMMENT "dotnet build ${CLOG_SOURCE_DIRECTORY}/clog.sln/clog_coreclr.sln -o ${CMAKE_CLOG_BINS_DIRECTORY}/${library}"
        OUTPUT ${CMAKE_CLOG_BINS_DIRECTORY}/${library}/clog.dll
        COMMAND dotnet build ${CLOG_SOURCE_DIRECTORY}/clog.sln/clog_coreclr.sln -o ${CMAKE_CLOG_BINS_DIRECTORY}/${library}
    )

    add_custom_command(
        WORKING_DIRECTORY ${CLOG_SOURCE_DIRECTORY}
        COMMENT "Building CLOG and its support tooling"
        COMMENT "msbuild -t:restore ${CLOG_SOURCE_DIRECTORY}/clog.sln/clog_windows.sln"
        COMMENT "msbuild ${CLOG_SOURCE_DIRECTORY}/clog.sln/clog_windows.sln /p:OutDir=${CMAKE_CLOG_BINS_DIRECTORY}/${library}/windows"
        OUTPUT ${CMAKE_CLOG_BINS_DIRECTORY}/${library}/windows/clog2text_windows.exe
        COMMAND msbuild -t:restore ${CLOG_SOURCE_DIRECTORY}/clog.sln/clog.sln
        COMMAND msbuild ${CLOG_SOURCE_DIRECTORY}/clog.sln/clog.sln /p:OutDir=${CMAKE_CLOG_BINS_DIRECTORY}/${library}/windows
    )

    foreach(arg IN LISTS ARGV)
        get_filename_component(RAW_FILENAME ${arg} NAME)
        set(ARG_CLOG_FILE ${CMAKE_CLOG_OUTPUT_DIRECTORY}/${library}/${RAW_FILENAME}.clog.h)
        set(ARG_CLOG_C_FILE ${CMAKE_CLOG_OUTPUT_DIRECTORY}/${library}/${library}_${RAW_FILENAME}.clog.h.c)

        # message(STATUS ">>>>>>> CLOG Source File = ${RAW_FILENAME}")

        if("${CMAKE_CXX_COMPILER_ID}" STREQUAL "MSVC")
            set_property(SOURCE ${arg}
                APPEND PROPERTY OBJECT_DEPENDS ${CMAKE_CLOG_BINS_DIRECTORY}/${library}/windows/clog2text_windows.exe
            )
        endif()

        add_custom_command(
            OUTPUT ${ARG_CLOG_FILE} ${ARG_CLOG_C_FILE}
            DEPENDS ${CMAKE_CLOG_BINS_DIRECTORY}/${library}/clog.dll
            DEPENDS ${CMAKE_CURRENT_SOURCE_DIR}/${arg}
            COMMENT "CLOG: ${CMAKE_CLOG_BINS_DIRECTORY}/${library}/clog --readOnly -p ${CMAKE_CLOG_CONFIG_PROFILE} --scopePrefix ${library} -c ${CMAKE_CLOG_CONFIG_FILE} -s ${CMAKE_CLOG_SIDECAR_DIRECTORY}/clog.sidecar -i ${CMAKE_CURRENT_SOURCE_DIR}/${arg} -o ${ARG_CLOG_FILE}"
            COMMAND ${CMAKE_CLOG_BINS_DIRECTORY}/${library}/clog --readOnly -p ${CMAKE_CLOG_CONFIG_PROFILE} --scopePrefix ${library} -c ${CMAKE_CLOG_CONFIG_FILE} -s ${CMAKE_CLOG_SIDECAR_DIRECTORY}/clog.sidecar -i ${CMAKE_CURRENT_SOURCE_DIR}/${arg} -o ${ARG_CLOG_FILE}
        )

        set_property(TARGET CLOG_GENERATED_FILES
            APPEND PROPERTY OBJECT_DEPENDS ${ARG_CLOG_FILE}
            APPEND PROPERTY OBJECT_DEPENDS ${CMAKE_CURRENT_SOURCE_DIR}/${arg}
        )

        set_property(SOURCE ${arg}
            APPEND PROPERTY OBJECT_DEPENDS ${ARG_CLOG_FILE}
        )

        list(APPEND clogfiles ${ARG_CLOG_C_FILE})
    endforeach()

    add_library(${library} STATIC ${clogfiles})
    target_include_directories(${library} PUBLIC ${CMAKE_CLOG_OUTPUT_DIRECTORY}/${library})
    # message(STATUS "^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^")
endfunction()
