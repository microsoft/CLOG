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

add_executable (clogsample ${SOURCES})
target_link_libraries(clogsample CLOGSAMPLE_LIB)
