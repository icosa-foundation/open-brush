#!/usr/bin/env python

# Copyright 2020 The Tilt Brush Authors
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#      http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.

import argparse
import csv
import itertools
import json


def iter_words_and_categories(filename):
    with open(filename) as inf:
        reader = csv.reader(inf)
        it = iter(reader)
        try:
            next(it)  # Skip first row
        except StopIteration:
            # This should never happen; this code is to meet PEP479 by returning instead of raising
            return
        for row in it:
            if len(row) == 2 and row[0] != "" and row[1] != "":
                yield row


def main():
    parser = argparse.ArgumentParser("Converts google docs .csv to tiltasaurus.json")
    parser.add_argument(
        "-i", dest="input", required=True, help="Name of input .csv file"
    )
    args = parser.parse_args()
    data = list(iter_words_and_categories(args.input))
    data.sort(
        key=lambda word_category1: (
            word_category1[1].lower(),
            word_category1[0].lower(),
        )
    )

    categories = []
    for _, group in itertools.groupby(
        data, key=lambda word_category: word_category[1].lower()
    ):
        group = list(group)
        category = {
            "Name": group[0][1],
            "Words": sorted(set(pair[0] for pair in group)),
        }
        categories.append(category)

    with open("tiltasaurus.json", "w") as outf:
        outf.write(json.dumps({"Categories": categories}, indent=2))
    print("Wrote tiltasaurus.json")


main()
