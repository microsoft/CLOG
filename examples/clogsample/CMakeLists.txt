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

add_executable(clogsample ${SOURCES})
target_link_libraries(clogsample CLOGSAMPLE_LIB)

if (NOT WIN32 AND NOT APPLE)

    include(FindLTTngUST)
    target_link_libraries(clogsample ${LTTNGUST_LIBRARIES})

    CLOG_GENERATE_TARGET(CLOGSAMPLE_DYNLIB DYNAMIC ${SOURCES})

    add_executable(clogsampledynamictp ${SOURCES})
    target_link_libraries(clogsampledynamictp CLOGSAMPLE_DYNLIB)

    add_library(clogsampletracepointprovider SHARED $<TARGET_OBJECTS:CLOGSAMPLE_DYNLIB.provider>)
    target_link_libraries(clogsampletracepointprovider ${LTTNGUST_LIBRARIES})

endif()
