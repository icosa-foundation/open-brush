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
import os
import uuid
import sys

properties_dir = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), 'Brushes\\ExportedProperties')

def list(args):
  for file in os.listdir(properties_dir):
    print(os.path.splitext(file)[0])
    
def create(args):
  properties_file = os.path.join(properties_dir, args.brush + ".txt")
  if (not os.path.exists(properties_file)):
    print(f"Error - '{args.brush}' is not a valid base brush - use userbrush list to list valid brushes.")
    exit
  dst_folder = os.path.join(os.path.join(os.path.join(os.path.expanduser('~'), 'Documents\\Open Brush\\Brushes'), args.name))
  dst_cfg = os.path.join(dst_folder, "Brush.cfg")
  if (os.path.exists(dst_cfg)):
    answer = str(input("Brush already exists! Are you sure you want to overwrite it? (Y/N)"))
    if (answer != 'y' and answer !='Y'):
      sys.exit()
  os.makedirs(dst_folder, exist_ok = True)
  src = open(properties_file, 'r')
  dst = open(dst_cfg, 'w')
  for line in src:
    stripline = line.strip()
    if (stripline.endswith('{') or stripline.endswith('}') or stripline.endswith('},')):
      dst.write(line)
    elif (stripline.startswith('"VariantOf"')):
      dst.write(line)
    elif (stripline.startswith('"GUID"')):
      dst.write(f'  "GUID": "{uuid.uuid4()}",\n')
    elif (stripline.startswith('"Name"')):
      dst.write(f'  "Name": "{args.name}",\n')
    elif (stripline.startswith('"Description"')):
      dst.write(f'  "Description": "{args.name}",\n')
    else:
      dst.write(f'//{line[2:]}')
  dst.close()
  src.close()
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