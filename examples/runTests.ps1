mkdir log
mkdir log/lttng
lttng create clogsample -o=log/lttng

lttng enable-event --userspace CLOG_*

#lttng add-context --userspace --type=vpid --type=vtid
lttng start

cd ./build/clogsample
$env:LD_PRELOAD="$PWD/libclogsampletracepointprovider.so"

./clogsampledynamictp
lttng list --userspace | grep CLOG
./clogsample
lttng list --userspace | grep CLOG

lttng stop clogsample
babeltrace --names all ./log/lttng/* > log/clog.babel
lttng destroy clogsample
cd ../..