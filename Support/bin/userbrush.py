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
import json
import os
import uuid
import sys

properties_dir = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), 'Brushes\\ExportedProperties')

def list(args):
  for file in os.listdir(properties_dir):
    print(os.path.splitext(file)[0])
    
def create(args):
  properties_filename = os.path.join(properties_dir, args.brush + ".txt")
  if (not os.path.exists(properties_filename)):
    print(f"Error - '{args.brush}' is not a valid base brush - use userbrush list to list valid brushes.")
    exit
  dst_folder = os.path.join(os.path.join(os.path.join(os.path.expanduser('~'), 'Documents\\Open Brush\\Brushes'), args.name))
  dst_cfg = os.path.join(dst_folder, "Brush.cfg")
  if (os.path.exists(dst_cfg)):
    answer = str(input("Brush already exists! Are you sure you want to overwrite it? (Y/N)"))
    if (answer != 'y' and answer !='Y'):
      sys.exit()
  os.makedirs(dst_folder, exist_ok = True)
  with open(properties_filename, "r") as properties_file:
    src_brush_data = json.load(properties_file)
    brush_data = {
      "VariantOf" : src_brush_data["GUID"],
      "GUID" : str(uuid.uuid4()),
      "Name" : args.name,
      "Description" : args.name,
      "CopyRestrictions" : "EmbedAndShare",
      "Author" : "",
      "Comments" : "",
      "Instructions_1" : "- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -",
      "Instructions_2" : f"The 'Original' section below contains the original properties and values valid for the {args.brush} base brush.",
      "Instructions_3" : f"You can copy them out of the 'Original' block and put them above to override the values in your {args.name} brush.",
      "Instructions_4" : "- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -",
      "Original" : src_brush_data
    }

    with open(dst_cfg, "w") as config_file:
      json.dump(brush_data, config_file, indent=4)
  
      print (f'Created brush config at {dst_cfg}.')
  

def main():
  parser = argparse.ArgumentParser()
  parser.set_defaults(func=lambda x: parser.parse_args(['--help']))
  subparser_name = None
  subparsers = parser.add_subparsers(help='command help', dest='subparser_name')
  parser_list = subparsers.add_parser('list', help='List available base brushes')
  parser_list.set_defaults(func=list)
  parser_create = subparsers.add_parser('create', help='Create a new user brush')
  parser_create.add_argument('name', help='Name of the new brush')
  parser_create.add_argument('brush', help='Brush to base the new one off')
  parser_create.set_defaults(func=create)
  args = parser.parse_args()
  
  args.func(args)


if __name__ == '__main__':
  main()