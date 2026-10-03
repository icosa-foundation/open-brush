#!/bin/env python

# Copyright 2021 The Open Brush Authors
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
import collections.abc
import json
import os
import uuid
import sys
from pprint import pprint

properties_dir = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                              os.path.join('Brushes', 'ExportedProperties'))


def list_brushes(args):
    # pylint: disable=unused-argument
    for file in os.listdir(properties_dir):
        print(os.path.splitext(file)[0])


def recursive_update(d, u):
    for k, v in u.items():
        if isinstance(v, collections.abc.Mapping):
            d[k] = recursive_update(d.get(k, {}), v)
        else:
            d[k] = v
    return d


def analyze_brushes(args):
    master_dict = {}
    prop_dict = {}
    brush_dict = {}
    for file in os.listdir(properties_dir):
        properties_filename = os.path.join(properties_dir, file)
        with open(properties_filename, "r") as properties_file:
            src_brush_data = json.load(properties_file)
            recursive_update(master_dict, src_brush_data)
    # pprint(master_dict)

    for file in os.listdir(properties_dir):
        properties_filename = os.path.join(properties_dir, file)
        with open(properties_filename, "r") as properties_file:
            has_anything = False
            src_brush_data = json.load(properties_file)
            name = src_brush_data["Name"]
            for section_key in master_dict['Material']:
                section = master_dict['Material'][section_key]
                if isinstance(section, collections.abc.Mapping):
                    for prop in section:
                        for subsection in ['ColorProperties', 'FloatProperties', 'TextureProperties',
                                           'VectorProperties']:
                            result = (prop in src_brush_data['Material'][subsection])
                            if result:
                                has_anything = True
                                # print("{} has {}/{}".format(name, section_key, prop))

                                if prop not in prop_dict:
                                    prop_dict[prop] = []
                                prop_dict[prop].append(name)

                                if name not in brush_dict:
                                    brush_dict[name] = []
                                brush_dict[name].append(prop)
            print(name, src_brush_data['Material']['Shader'], sep=",")

            # if not has_anything:
            #     print("{} has no props".format(name))
        # print()
        #
        # for k, v in prop_dict.items():
        #     print(k + ',' + ','.join(v))
        # print()
        #
        # for k, v in brush_dict.items():
        #     print(k + ',' + ','.join(v))
        # print()
        #
        # bool_dict = {}
        # for brush in brush_dict.keys():
        #     for prop, brushes in prop_dict.items():
        #         bool_dict[(brush, prop)] = brush in brushes
        # for k, v in brush_dict.items():
        #     print(k + ',' + ','.join(v))
        # print()
        #
        # print("", end=",")
        # for prop in prop_dict.keys():
        #     print(prop, end=",")
        # print()
        # for brush in brush_dict.keys():
        #     print (brush, end=',')
        #     for prop in prop_dict.keys():
        #         print(bool_dict[brush, prop] if "Y" else "", end=",")
        #     print()
        # print()
        #
        # print("", end=",")
        # for brush in brush_dict.keys():
        #     print(brush, end=",")
        # print()
        # for prop in prop_dict.keys():
        #     print (prop, end=',')
        #     for brush in brush_dict.keys():
        #         print(bool_dict[brush, prop] if "Y" else "", end=",")
        #     print()
        # print()

def create(args):
    properties_filename = os.path.join(properties_dir, args.brush + ".txt")
    if not os.path.exists(properties_filename):
        print(f"Error - '{args.brush}' is not a valid base brush - use userbrush list to list valid brushes.")
        sys.exit()
    guid = str(uuid.uuid4())
    dst_folder = os.path.join(os.path.expanduser('~'), 'Documents', 'Open Brush', 'Brushes', args.name + "_" + guid)
    dst_cfg = os.path.join(dst_folder, "Brush.cfg")
    os.makedirs(dst_folder, exist_ok=True)
    with open(properties_filename, "r") as properties_file:
        src_brush_data = json.load(properties_file)
        brush_data = src_brush_data.copy()
        brush_data["VariantOf"] = src_brush_data["GUID"]
        brush_data["GUID"] = guid
        brush_data["Name"] = args.name
        brush_data["Description"] = args.name
        brush_data["Author"] = ""
        brush_data["ButtonIcon"] = ""
        brush_data["Comments"] = ""
        brush_data["Original_Base_Brush_Values"] = src_brush_data

        with open(dst_cfg, "w") as config_file:
            json.dump(brush_data, config_file, indent=4)

            print(f'Created brush config at {dst_cfg}.')


def main():
    parser = argparse.ArgumentParser()
    parser.set_defaults(func=lambda x: parser.parse_args(['--help']))
    subparsers = parser.add_subparsers(help='command help', dest='subparser_name')
    parser_list = subparsers.add_parser('list', help='List available base brushes')
    parser_list.set_defaults(func=list_brushes)
    parser_analyze = subparsers.add_parser('analyze', help='Analyze available base brushes')
    parser_analyze.set_defaults(func=analyze_brushes)
    parser_create = subparsers.add_parser('create', help='Create a new user brush')
    parser_create.add_argument('name', help='Name of the new brush')
    parser_create.add_argument('brush', help='Brush to base the new one off')
    parser_create.set_defaults(func=create)
    args = parser.parse_args()

    args.func(args)


if __name__ == '__main__':
    main()
