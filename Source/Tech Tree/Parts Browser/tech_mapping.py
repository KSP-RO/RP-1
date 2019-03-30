import csv
import json
import sys
from os import listdir
from os.path import isfile, join

class TechMapping:
    tech_map = {}
    unique_years = set()

    def get_tech_by_category_and_year(self, category, year):
        print(f'Getting tech for category: {category} and {year}')
        if category in self.tech_map:
            if year in self.tech_map[category]:
                return self.tech_map[category][year]
        return "not found"

    def __init__(self):
        """Load the parts from the json files."""
        self.load_tech_mapping()
        print(f'Initialized the Tech mappings.')
        
    column_index = {}

    def load_tech_mapping(self):
        with open('mappings/TechRequiredMappings.csv') as csv_file:
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
                    self.create_mapping(row)
            print(f'Loaded {line_count-1} tech mappings.')

    # This method creates the mappings given a row from the TechMapping CSV file.
    def create_mapping(self,row):
        category = self.get_value(row, "Category").upper()
        year = self.get_value(row, "Year")
        tech_required = self.get_value(row, "Tech")
        self.unique_years.add(year)
        
        if category in self.tech_map:
            if year in self.tech_map[category]:
                print("Error, duplicate tech mapping row: " + category + " - " + year)
            self.tech_map[category][year] = tech_required
        else:
            self.tech_map[category] = {year: tech_required}
                
    
    # protects against missing keys causing exceptions in original import
    def get_value(self,row,column_name):
        if column_name in self.column_index:
            return row[self.column_index[column_name]]
        else:
            return None

    def validate_current(self, parts):
        for part in parts:
            name = part['name'] if 'name' in part else ''
            mod = part['mod'] if 'mod' in part else ''
            tech_required = part['technology'] if 'technology' in part else ''
            category = part['category'] if 'category' in part else ''
            year = part['year'] if 'year' in part else ''
            if category in self.tech_map:
                if year in self.tech_map[category]:
                    expected_tech = self.tech_map[category][year]
                    if tech_required != expected_tech:
                        print(f"Tech mismatch for {name} in mod {mod}: {category} - {year} expected: {expected_tech} actual: {tech_required}")
                else:
                    print(f"Year {year} not found for category {category} for part {name} in mod {mod}")
            elif category != "":
                print(f"Category {category} not found for part {name} in mod {mod}")
                
            
            
    