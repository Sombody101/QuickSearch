#!/bin/bash

# Helps speed up the process of compiling apps for all runtimes

targets=(
    "linux-x64"
    "linux-musl-x64"
    "linux-arm"
    "linux-arm64"
    "win-x64"
    "win-x86"
    "win-arm"
    "win-arm64"
)

targetPath="$HOME/cs/qs/PrecompiledBinaries/PackedWithRuntime"

echo Starting runtime-packed builds
[[ ! -d "$targetPath" ]] && mkdir "$targetPath"

for target in "${targets[@]}"; do
    dotnet publish -r "$target" -p:PublishSingleFile=true --self-contained true --output "$targetPath" -p:AssemblyName="qs-$target"
    [[ -f "$targetPath/qs-$target.pdb" ]] && rm "$targetPath/qs-$target.pdb"
done

targetPath="$HOME/cs/qs/PrecompiledBinaries/DependantOnRuntime"

echo Starting dependant builds
[[ ! -d "$targetPath" ]] && mkdir "$targetPath"

for target in "${targets[@]}"; do
    dotnet publish -r "$target" -p:PublishSingleFile=true --self-contained false --output "$targetPath" -p:AssemblyName="qs-$target"
    [[ -f "$targetPath/qs-$target.pdb" ]] && rm "$targetPath/qs-$target.pdb"
done
