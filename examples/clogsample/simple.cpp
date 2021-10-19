/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

   Simple CLOG test

--*/
#include <stdio.h>
#include "simple.cpp.clog.h"

int main(int argc, char* argv[])
{
    for(int i=0; i<10000; ++i)
    {
        if(i % 100)
            printf("%d\n", i);

    //printf("Hello world!\n");
    TraceInfo(LAUNCHED, "Hello world - we just started here is an int=%d", 20);

    char *buffer = new char[10];
    for(char i=0; i<10; ++i)
    {
        TraceInstanceInfo(INSTANCE_TEST, buffer, "1:%d 2:%s 3:%c 4:%u 5:%u 6:%u", 1, "2", '3', 4, 5, 6);
    }

    TraceInfo(DATABYTEARRAY, "%!BYTEARRAY!", CLOG_BYTEARRAY(5, "hello"));

    TraceInfo(DATACHAR, "This is a char: %c", 'a');
    TraceInfo(DATAINT, "This is an int: %d", 1234);
    delete [] buffer;
    }
    return 0;
}
