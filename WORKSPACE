
# https://github.com/bazelbuild/bazel-skylib/releases


load("@rules_dotnet//dotnet:defs.bzl", "nuget_repo")

load("@rules_dotnet//dotnet:paket.paket2bazel_dependencies.bzl", "paket2bazel_dependencies")
paket2bazel_dependencies()

load(":paket.example_deps.bzl", "example_deps")
example_deps()

