# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Include Files that need CLOG processing
include(${CMAKE_CLOG_GENERATE_FILE})

set(SOURCES
    simple.cpp
)

#
# Create a clog target for all the source files. This target is then used
# as a normal cmake target dependency
#
CLOG_GENERATE_TARGET(CLOGSAMPLE_LIB STATIC ${SOURCES})
file(MAKE_DIRECTORY ${CMAKE_CURRENT_BINARY_DIR}/inc)


if ("${CMAKE_CLOG_CONFIG_PROFILE}" STREQUAL "windows")
    add_executable(clogsample ${SOURCES} ${CMAKE_CURRENT_BINARY_DIR}/inc/clog_examples.rc)
else()
    add_executable(clogsample ${SOURCES})
endif()

target_link_libraries(clogsample CLOGSAMPLE_LIB)


if ("${CMAKE_CLOG_CONFIG_PROFILE}" STREQUAL "linux")
    include(FindLTTngUST)
    target_link_libraries(clogsample ${LTTNGUST_LIBRARIES})

    CLOG_GENERATE_TARGET(CLOGSAMPLE_DYNLIB DYNAMIC ${SOURCES})

    add_executable(clogsampledynamictp ${SOURCES})
    target_link_libraries(clogsampledynamictp CLOGSAMPLE_DYNLIB)

    add_library(clogsampletracepointprovider SHARED $<TARGET_OBJECTS:CLOGSAMPLE_DYNLIB.provider>)
    target_link_libraries(clogsampletracepointprovider ${LTTNGUST_LIBRARIES})
endif()


if ("${CMAKE_CLOG_CONFIG_PROFILE}" STREQUAL "windows")
    message(STATUS "BUILDING CLOG SAMPLE(Windows)")

    add_custom_command(
        OUTPUT ${CMAKE_CURRENT_BINARY_DIR}/inc/clog_examples.h
        OUTPUT ${CMAKE_CURRENT_BINARY_DIR}/inc/clog_examples.rc
        DEPENDS clog_examples.man
        COMMAND mc.exe -um -h ${CMAKE_CURRENT_BINARY_DIR}/inc -r ${CMAKE_CURRENT_BINARY_DIR}/inc ${CMAKE_CURRENT_SOURCE_DIR}/clog_examples.man)

    set_property(SOURCE simple.cpp APPEND PROPERTY OBJECT_DEPENDS ${CMAKE_CURRENT_BINARY_DIR}/inc/clog_examples.h)
    target_include_directories(clogsample PRIVATE ${CMAKE_CURRENT_BINARY_DIR}/inc)
    add_definitions(-DCLOG_ETW)

    configure_file(clog_examples.wprp clog_examples.wprp COPYONLY)
    configure_file(clog_examples.man clog_examples.man COPYONLY)
endif()
