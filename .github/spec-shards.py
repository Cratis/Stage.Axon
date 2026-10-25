#!/usr/bin/env python3
# Copyright (c) Cratis. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

"""Reject empty test runs; extracted from Stage's shard guard."""

import argparse
import xml.etree.ElementTree as ET


def require_executed(path):
    namespace = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
    root = ET.parse(path).getroot()
    counter = root.find("t:ResultSummary/t:Counters", namespace)
    results = root.findall("t:Results/t:UnitTestResult", namespace)
    executed = int(counter.get("executed", "0")) if counter is not None else 0
    if executed <= 0 or not any(result.get("outcome") == "Passed" for result in results):
        raise SystemExit(f"{path}: the filtered spec run must execute tests, not discover nothing or only skip.")
    print(f"{path}: {executed} tests executed")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=["require-executed"])
    parser.add_argument("path")
    args = parser.parse_args()
    require_executed(args.path)


if __name__ == "__main__":
    main()
