/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

   Simple CLOG test

--*/
#include <stdio.h>
#include "simple.cpp.clog.h"

#if defined(CLOG_ETW)
//wevtutil.exe im .\clog_examples.man
//wpr -start clog_examples.wprp -filemode -instancename clog_example
//wpr -stop clog_example.etl -instancename clog_example
//https://docs.microsoft.com/en-us/windows/win32/wes/defining-events
#include <windows.h>
#include <evntprov.h>
#include <evntcons.h>
#include "clog_examples.h"
#include <TraceLoggingProvider.h>  // The native TraceLogging API


// Forward-declare the clog_hTrace variable that you will use for tracing in this component
TRACELOGGING_DECLARE_PROVIDER( clog_hTrace );

// {7EBE92EB-B7AE-4720-B842-FA8798950838}
TRACELOGGING_DEFINE_PROVIDER(
    clog_hTrace,
    "CLOGSample-TraceLogging",
    (0x7ebe92eb, 0xb7ae, 0x4720, 0xb8, 0x42, 0xfa, 0x87, 0x98, 0x95, 0x8, 0x38));
#endif

int main(int argc, char* argv[])
{
    #ifdef CLOG_ETW
        EventRegisterCLOG_SAMPLE_MANIFEST();
        TraceLoggingRegister(clog_hTrace);
    #endif

    //
    // Note this is not a 'real' pointer - as would normally be used in this situation
    //    we're using this constant so that we run the same way each time
    //    and our output can be compared using simple text comparisons
    //
    char *buffer = (char *)0xAABBCCDD00110011;
    TraceInstanceInfo(INSTANCE_TEST, buffer, "1. 1:%d 2:%s 3:%c 4:%u 5:%hd 6:%lld - you should see 1 2 3 4 5 6", 1, "2", '3', 4, 5, (long long int)6);

    TraceInfo(DATA_STRING, "2. I am string %s=hello", "hello");
    TraceInfo(DATABYTEARRAY, "3. This is a byte array with a custom decoder = %!BYTEARRAY!", CLOG_BYTEARRAY(5, (const unsigned char *)"hello"));

    TraceInfo(DATACHAR, "4. This is a char: %c; it should equal a", 'a');
    TraceInfo(DATA_NAMED_INT, "5. This is a named int: %{myInt,d};  it should be 1234", 1234);

    TraceInfo(DATAINT5, "6. This is an int: %d;  it should be 1234", 1234);

    TraceError(INT_ERROR, "7. this is an error %d", 123456);
    TraceError(INT_ERROR_2, "8. this is an error %d with a string %s", 80, "ouch!");

    TraceError(NO_ARGS, "9. This trace has no args");


    #ifdef CLOG_ETW
        EventUnregisterCLOG_SAMPLE_MANIFEST();
        TraceLoggingUnregister(clog_hTrace);
    #endif
    return 0;
}
