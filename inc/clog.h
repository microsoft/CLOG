#pragma once 

#ifndef LOG_ADDR_LEN
#define LOG_ADDR_LEN(x) sizeof(x)
#endif

#define CLOG_BYTEARRAY_HELPER(length, pointer) pointer, length
#define CLOG_BYTEARRAY(length, pointer) CLOG_BYTEARRAY_HELPER(length, pointer)
#define CLOG_PTR const void *
#define CLOG_INT32 const int
#define CLOG_UINT32 const unsigned int
#define CLOG_INT64 const long long
#define CLOG_UINT64 unsigned long long

#ifndef CLOG_H
#define CLOG_H 1

#ifdef __cplusplus
extern "C" {
#endif

    
#define EXPAND(x) x
#define SELECT_ARGN_MACRO(_1, _2, _3, _4, _5, _6, _7, _8, _9, _10, _11, _12, _13, _14, _15, _16, _17, _18, _19, _20, _21, _22, _23, _24, _25, N, ...) N
#define CLOG_ARGN_SELECTOR(...) \
    EXPAND(SELECT_ARGN_MACRO( \
        __VA_ARGS__, \
        CLOG_26_ARGS_TRACE,\
        CLOG_25_ARGS_TRACE,\
        CLOG_24_ARGS_TRACE,\
        CLOG_23_ARGS_TRACE,\
        CLOG_22_ARGS_TRACE,\
        CLOG_21_ARGS_TRACE,\
        CLOG_20_ARGS_TRACE,\
        CLOG_19_ARGS_TRACE,\
        CLOG_18_ARGS_TRACE,\
        CLOG_17_ARGS_TRACE,\
        CLOG_16_ARGS_TRACE, \
        CLOG_15_ARGS_TRACE, \
        CLOG_14_ARGS_TRACE, \
        CLOG_13_ARGS_TRACE, \
        CLOG_12_ARGS_TRACE, \
        CLOG_11_ARGS_TRACE, \
        CLOG_10_ARGS_TRACE, \
        CLOG_9_ARGS_TRACE, \
        CLOG_8_ARGS_TRACE, \
        CLOG_7_ARGS_TRACE, \
        CLOG_6_ARGS_TRACE, \
        CLOG_5_ARGS_TRACE, \
        CLOG_4_ARGS_TRACE, \
        CLOG_3_ARGS_TRACE, \
        CLOG_2_ARGS_TRACE, \
        0))

#define CLOG_CAT_HELPER(x, y) x ## y
#define CLOG_CAT(x, y) CLOG_CAT_HELPER(x, y)  

#ifdef __cplusplus
}
#endif

#endif