#!/bin/bash

testCases=(
    "--generate-settings"
    ""
)

caseNum=0
for case in "${testCases[@]}"; do
    cmdout=$(./bin/Debug/net6.0/qs "$case" 2>&1)
    out=$?
    if ((out)); then
        echo "$(tput setaf 2)Test case $caseNum complete$(tput sgr0)"
    else
        echo "$(tput setaf 1)Test case $caseNum failed:"
        echo "$cmdout"
    fi
    caseNum=$((caseNum + 1))
done
