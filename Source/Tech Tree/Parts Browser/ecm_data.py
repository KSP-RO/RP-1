import csv
import json
import sys
from os import listdir
from os.path import isfile, join
import os

output_dir = os.getenv('PB_OUTPUT_DIR', "../../../GameData/RP-0/Tree/")

class ECMNodeEncoder(json.JSONEncoder):
     def default(self, obj):
         if isinstance(obj, ECMNode):
             return vars(obj)
         # Let the base class default method raise the TypeError
         return json.JSONEncoder.default(self, obj)

class ECMNode:
    
    def __init__(self, id, cost=0):
        self.id = id
        self.cost = cost
        self.children = []
        self.parents = []
        
    def add_parent(self, parent_node):
        self.parents.append(parent_node)
        
    def add_child(self, child_node):
        self.children.append(child_node)
    
class ECMData:
    ecms = {}
    top_level_ids = []
    total_parents = 0
    
    def __init__(self, parts):
        """Load the parts from the json files."""
        self.load_ecm_data(parts)
        print(f'Initialized the ecm data.')
        print(f'Total parents added: {self.total_parents}.')
        
        for ecm_id in self.ecms:
            if len(self.ecms[ecm_id].parents) == 0 and len(self.ecms[ecm_id].children) > 0:
                self.top_level_ids.append(ecm_id)
                

        self.top_level_ids = sorted(self.top_level_ids, key=lambda s: s.lower())
        
    def walk_dag(self, pid, children):
        json_children = []
        for child_node in children:
            json_node = {"id": pid + child_node.id, "text": child_node.id + "(" + str(child_node.cost) + ")", "children": self.walk_dag(child_node.id, child_node.children)}
            json_children.append(json_node)
        return json_children

    def get_data(self):
        json_data = []
        for id in self.top_level_ids:
            ecm_node = self.ecms[id]
            json_node = {"id": id, "text": id + "(" + str(ecm_node.cost) + ")", "children":[]}
            try:
                json_node["children"] = self.walk_dag(id, ecm_node.children)            
            except RecursionError:
                print(f"Recursion overflow on id: {id}")
            json_data.append(json_node)
        return json_data
        
    def load_ecm_data(self, parts):
        with open(output_dir + 'EntryCostModifiers.cfg', newline="\n") as ecm_file:
            line_count = 0
            for line in ecm_file:
                line_count += 1
                if "//	OLD	DATA BELOW" in line: 
                    break
                # remove whitespaces
                line = line.replace("\t", "").replace("\n", "")
                # remove comments
                line = line[0:line.index("//")].strip() if "//" in line else line.strip()
                line = line[0:line.index("#")].strip() if "#" in line else line.strip()
                
                if len(line.split("=")) > 1:
                    self.create_mapping(line.split("="))
            print(f'Loaded {line_count} ecm mappings from EntryCostModifiers.cfg')
        part_ecm_mappings = 0
        for part in parts:
            if 'entry_cost_mods' in part and len(part['entry_cost_mods']) > 0:
                part_ecm = part['entry_cost_mods']
                part_ecm = part_ecm[0:part_ecm.index("//")].strip() if "//" in part_ecm else part_ecm.strip()
                part_ecm = part_ecm[0:part_ecm.index("#")].strip() if "#" in part_ecm else part_ecm.strip()
                self.create_mapping([part['name'].replace('_','-').replace('.','-').replace('?',' '), part['entry_cost_mods']])
                part_ecm_mappings += 1
        print(f'Loaded {part_ecm_mappings} ecm mappings from the parts')

            
    # This method creates the tree nodes based on the ecms ingested
    def create_mapping(self,split_line):
        id = split_line[0].replace('\t','').replace('\n','').strip()
        ecms_ids = split_line[1].split(',')
        # remove comments
        ecms_ids = [x[0:x.index("//")].strip() if "//" in x else x.strip() for x in ecms_ids]
        ecms_ids = [x[0:x.index("#")].strip() if "#" in x else x.strip() for x in ecms_ids]
        ecms_ids = [w.replace('\t', '').replace('\n', '').strip() for w in ecms_ids]
        cost = ecms_ids[0].strip() if ecms_ids[0].strip().isdigit() else ""
        if id not in self.ecms:
            self.ecms[id] = ECMNode(id, cost)
        else:
            self.ecms[id].cost = cost
                    
        for ecm_id in ecms_ids:
            if not ecm_id.strip().isdigit() and len(ecm_id.strip()) > 0:
                if ecm_id not in self.ecms:
                    self.ecms[ecm_id] = ECMNode(ecm_id)
                self.ecms[ecm_id].add_child(self.ecms[id])
                self.ecms[id].add_parent(self.ecms[ecm_id])
                self.total_parents += 1
            
            
            
        
                
    
    
            
    