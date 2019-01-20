import csv
import json
import sys
from os import listdir
from os.path import isfile, join

class PartData:
    parts = []
    unique_values_for_columns = {
        "mod": set(),
        "ro": set(),
        "rp0": set(),
        "category": set(),
        "technology": set(),
        "orphan": set(),
        "upgrade": set(),
        "rp0_conf": set(),
        "spacecraft": set(),
        "engine_config": set(),
        "era": set(),
        "year": set()
        }

    module_tags = {}
            
    column_index = {}
    
    def load_module_tags(self):
        with open('module_tags.csv') as csv_file:
            csv_reader = csv.reader(csv_file, delimiter=',')
            line_count = 0
            for row in csv_reader:
                line_count += 1
                self.module_tags[row[0]] = row[1]
            print(f"Loaded {line_count} module tags.")
            
    def get_part_by_name(self, name):
        print(f'Getting part with name: {name}')
        for part in self.parts:
            if part["name"] == name:
                return part
        return None

    def add_new_part(self, new_part):
        print(f'Adding new part with name: {new_part["name"]}')
        # add the part to the data
        self.parts.append(new_part)
        # reindex the columns
        self.index_columns()
        return None
    
    def __init__(self):
        """Load the parts from the json files."""
        self.load_module_tags()
        self.load_parts_json()
        print(f'Initialized the database.')
        self.index_columns()
        print(f'Indexed the columns.')
        
        
    # loads the parts from the json files in the data directory 
    #(if running from flask or python, we use data/ for json file location, 
    # if we're running from the bundled application, we use ../data/
    def load_parts_json(self):
        directory = 'data/'
        onlyfiles = [f for f in listdir(directory) if isfile(join(directory, f))]
        for file_name in onlyfiles:
            if file_name.endswith('.json'):
                f = open(join(directory, file_name))
                data = json.load(f)
                f.close()
                self.parts.extend(data)
                print(f'Loaded {len(data)} parts from {file_name}')
        self.parts.sort(key=lambda x: x['name'] if x['name'].lower() is not None and len(x['name']) > 0 else x['title'].lower())
        
        # find any duplicate part names and report them
        unique_names = {}
        duplicates = {}
        for part in self.parts:
            name = part['name'] if part['mod'] != 'Engine Config' else part['title']
            if name in unique_names:
                if name not in duplicates:
                    duplicates[name] = [unique_names[name]]
                duplicates[name].append(part['mod'])
            unique_names[name] = part['mod']
            
        for name in duplicates:
            if len(name) > 0:
                print(f"Found duplicate parts with name '{name}' in mods: {str(duplicates[name])}")
            

            
    # gets a list of unique values for each column we specified above, for filtering in the datatable.
    def index_columns(self):
        for part in self.parts:
            for key in part:
                if key in self.unique_values_for_columns:
                    self.unique_values_for_columns[key].add(part[key])
                    
    # This method was used to load the data originall and port to json, no longer used, but kept for posterity.
    # DEPRECATED
    def load_parts_csv(self):
        with open('original_sheet_data/PartsSheet.csv') as csv_file:
            csv_reader = csv.reader(csv_file, delimiter=',')
            line_count = 0
            for row in csv_reader:
                if line_count == 0:
                    print(f'Column names are {", ".join(row)}')
                    # store a map of column name to index for pulling into the part object
                    for entry in enumerate(row):
                        self.column_index[entry[1]] = entry[0]
                    line_count += 1
                else:
                    line_count += 1
                    self.parts.append(self.create_part(row))
            print(f'Loaded {len(self.parts)} parts.')

    # This method created the parts given a row from the original spreadsheet CSV file.
    # DEPRECATED
    def create_part(self,row):
        part = {
            "name": self.get_value(row, "Name"),
            "title": self.get_value(row, "Title"),
            "description": self.get_value(row, "Description"),
            "mod": self.get_value(row, "MOD"),
            "cost": self.get_value(row, "Cost"),
            "entry_cost": self.get_value(row, "EntryCost"),
            "category": self.get_value(row, "CATEGORY"),
            "info": self.get_value(row, "INFO"),
            "year": self.get_value(row, "Year"),
            "technology": self.get_value(row, "Tech"),
            "era": self.get_value(row, "ERA"),
            "ro": self.get_boolean(row, "RO"),
            "rp0": self.get_boolean(row, "RP-0"),
            "orphan": self.get_boolean(row, "ORPHAN"),
            "rp0_conf": self.get_boolean(row, "RP0conf"),
            "spacecraft": self.get_value(row, "SPACECRAFT"),
            "engine_config": self.get_value(row, "ENGINE CONFIG"),
            "upgrade": self.get_boolean(row, "UPGRADE"),
            "entry_cost_mods": self.get_value(row, "ECMS"),
            "identical_part_name": self.get_value(row, "IdentPart"),
            "module_tags": []
        }

        # start => unlockParts
        if part['technology'] == 'start':
            part['technology'] = 'unlockParts'
        
        for module_tag in list(self.module_tags.keys()):
            if self.get_tag(row, module_tag):
                part["module_tags"].append(module_tag)
        
        return part;
    
    # protects against missing keys causing exceptions in original import
    # DEPRECATED
    def get_value(self,row,column_name):
        if column_name in self.column_index:
            return row[self.column_index[column_name]]
        else:
            return None
            
    # protects against missing keys causing exceptions in original import
    # DEPRECATED
    def get_boolean(self,row,column_name):
        value = self.get_value(row, column_name)
        if value is not None and value.lower() in ["yes","true","x"]:
            return True
        else: 
            return False
            
    # protects against missing keys causing exceptions in original import
    # DEPRECATED
    def get_tag(self,row,column_name):
        value = self.get_value(row, column_name)
        if value is not None and len(value.strip()) > 0:
            return True
        else: 
            return None
            
            
    