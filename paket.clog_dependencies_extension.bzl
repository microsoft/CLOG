"Generated"

load(":paket.clog_dependencies.bzl", _clog_dependencies = "clog_dependencies")

def _clog_dependencies_impl(_ctx):
    _clog_dependencies()

clog_dependencies_extension = module_extension(
    implementation = _clog_dependencies_impl,
)
