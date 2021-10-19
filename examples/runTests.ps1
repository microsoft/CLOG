rm -r -f ./log
mkdir log
mkdir log/lttng

docker

lttng create clogsample -o=log/lttng
if(!$?)
{
    Write-Host "LTTNG Create failed"
    Exit
}

lttng enable-event --userspace CLOG_*
if(!$?)
{
    Write-Host "LTTNG enable-event failed"
    Exit
}

lttng add-context --userspace --type=vpid --type=vtid
if(!$?)
{
    Write-Host "LTTNG add-context failed"
    Exit
}
lttng start
if(!$?)
{
    Write-Host "LTTNG start failed"
    Exit
}

$env:LD_PRELOAD="$PWD/libclogsampletracepointprovider.so"
./clogsampledynamictp
lttng list --userspace | grep CLOG
./clogsample
lttng list --userspace | grep CLOG

lttng stop clogsample
lttng destroy clogsample

babeltrace --names all ./log/lttng/* > log/clog.babel

../buildclog/artifacts/clog2text_lttng -i ./log/clog.babel -s ../../clog.sidecar
