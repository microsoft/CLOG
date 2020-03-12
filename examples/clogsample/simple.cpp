#include <stdio.h>

#include "simple.cpp.clog"

int main(int argc, char* argv[])
{
    printf("Hello world!");
    TraceInfo(Launched, "Hello world - we just started %d", 80);
    return 0;
}