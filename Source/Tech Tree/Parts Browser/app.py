import os
import json

from flask import jsonify
from part_data import PartData
from ecm_data import ECMData
from tech_mapping import TechMapping
from flask import Flask, g
from flask import Blueprint, abort, g, render_template, redirect, request, url_for
from slugify import slugify
from tree_engine_cfg_generator import generate_engine_tree
from tree_parts_cfg_generator import generate_parts_tree
from ecm_engines_cfg_generator import generate_ecm_engines
from ecm_parts_cfg_generator import generate_ecm_parts
from identical_parts_cfg_generator import generate_identical_parts

part_data = PartData()
tech_mapping = TechMapping()
ecm_data = ECMData(part_data.parts)

tech_mapping.validate_current(part_data.parts)
server_port = os.getenv('FLASK_RUN_PORT', "5000")

def create_app(test_config=None):

    # create and configure the app
    app = Flask(__name__, instance_relative_config=True)
    app.config.from_mapping(
        SECRET_KEY='dev'
    )

    if test_config is None:
        # load the instance config, if it exists, when not testing
        app.config.from_pyfile('config.py', silent=True)
    else:
        # load the test config if passed in
        app.config.from_mapping(test_config)

    # ensure the instance folder exists
    try:
        os.makedirs(app.instance_path)
    except OSError:
        pass

    # a simple page that says hello
    @app.route('/hello')
    def hello():
        return 'Hello, World!'

    @app.route('/api/part_data')
    def all_part_data():
        return jsonify({"data": part_data.parts})
    @app.route('/api/ecm_data')
    def get_ecm_data():
        return jsonify(ecm_data.get_data())
    
    @app.route('/api/unique_values_for_column/<column_name>')
    def unique_values_for_column(column_name):
        sorted_values = list(part_data.unique_values_for_columns[column_name])
        sorted_values.sort()
        return jsonify({"data": sorted_values})
    @app.route('/api/module_tags')
    def module_tags():
        sorted_values = list(part_data.module_tags.keys())
        sorted_values.sort()
        return jsonify({"data": list(map(lambda x: {"tag": x}, sorted_values))})
    
    @app.route('/api/nuke_module_tag/<module_tag>')
    def nuke_module_tags(module_tag):
        for part in part_data.parts:
            if module_tag in part['module_tags']:
                part['module_tags'].remove(module_tag)
        return "TRUE"
        
    @app.route('/api/tech_mapping/<category>/<year>')
    def get_tech_mapping(category, year):
        return tech_mapping.get_tech_by_category_and_year(category, year)
    @app.route('/api/combo_options/<column_name>')
    def combo_options(column_name):
        if column_name != 'year':
            sorted_values = list(part_data.unique_values_for_columns[column_name])
            sorted_values.sort()
            return jsonify({"data": list(map(lambda x: {column_name: x}, sorted_values))})
        else:
            sorted_values = list(tech_mapping.unique_years)
            sorted_values.sort()
            return jsonify({"data": list(map(lambda x: {column_name: x}, sorted_values))})
    
    @app.route('/api/export_to_json')
    def export_to_json():
        for mod in part_data.unique_values_for_columns['mod']:
            parts_for_mod = list(filter(lambda x: x['mod'] == mod, part_data.parts))
            parts_for_mod.sort(key=lambda x: x['name'] if x['name'] is not None and len(x['name']) > 0 else x['title'] )
            for part in parts_for_mod:
                if 'module_tags' in part:
                    part['module_tags'] = list(sorted(part['module_tags']))
                else: 
                    part['module_tags'] = []
            text_file = open("data/" + make_safe_filename(mod)  + ".json", "w", newline='\n')
            text_file.write(json.dumps(parts_for_mod, indent=4, separators=(',', ': ')))
            text_file.close()
        return "true"
    @app.route('/api/generate_tree_engine_configs')
    def generate_tree_engine_configs():
        generate_engine_tree(part_data.parts)
        return "true"
    
    @app.route('/api/generate_tree_parts_configs')
    def generate_tree_parts_configs():
        generate_parts_tree(part_data.parts)
        return "true"
    
    @app.route('/api/generate_ecm_engines_configs')
    def generate_ecm_engines_configs():
        generate_ecm_engines(part_data.parts)
        return "true"
        
    @app.route('/api/generate_ecm_parts_configs')
    def generate_ecm_parts_configs():
        generate_ecm_parts(part_data.parts)
        return "true"
        
    @app.route('/api/generate_identical_parts_configs')
    def generate_identical_parts_configs():
        generate_identical_parts(part_data.parts)
        return "true"
        
    @app.route('/api/generate_all_configs')
    def generate_all_configs():
        generate_parts_tree(part_data.parts,part_data.module_tags)
        generate_engine_tree(part_data.parts)
        generate_identical_parts(part_data.parts)
        generate_ecm_parts(part_data.parts)
        generate_ecm_engines(part_data.parts)
        return "true"
    
    @app.route('/api/commit_changes',  methods=['POST'])
    def commit_changes():
        queued_changes = request.get_json()
        for row_id in queued_changes['queued_changes']:
            new_part = False
            part = None
            # if the part name changed, we need to use the old name to find it, else use the supplied name field
            if 'name' in queued_changes['queued_changes'][row_id]['changes'] and 'old' in queued_changes['queued_changes'][row_id]['changes']['name']:
                part = part_data.get_part_by_name(queued_changes['queued_changes'][row_id]['changes']['name']['old'])
            else: 
                part = part_data.get_part_by_name(queued_changes['queued_changes'][row_id]['name'])
            # if the part can't be found, we assume it's a new part
            if part is None:
                part = {}
                new_part = True
            # if the mod isn't set, that is almost certainly because we're 
            # adding to a new mod, need to set it from the new_mod field
            if 'mod' in queued_changes['queued_changes'][row_id]['changes'] and queued_changes['queued_changes'][row_id]['changes']['mod']['new'] == "":
                queued_changes['queued_changes'][row_id]['changes']['mod']['new'] = queued_changes['queued_changes'][row_id]['changes']['new_mod']['new']
            for field_name in queued_changes['queued_changes'][row_id]['changes']:
                if field_name not in ['mod_type', 'new_mod']:
                    part[field_name] = queued_changes['queued_changes'][row_id]['changes'][field_name]['new']
            if new_part:
                part_data.add_new_part(part)
        export_to_json()
        return "true"
    
    def commit_change_set(change_set):
        part = part_data.get_part_by_name(change_set['name']);
    
    app.register_blueprint(bp)
    app.run(port=server_port)    
    return app
    
def make_safe_filename(s):
    def safe_char(c):
        if c.isalnum():
            return c
        else:
            return "_"
    return "".join(safe_char(c) for c in s).rstrip("_")
    
bp = Blueprint("part", __name__, url_prefix="/")

@bp.route("/")
def index():
    """
    Render the homepage.
    """
    parts = []
    parts_final = []
    
    return render_template("browser/index.html", parts=parts_final)


@bp.route("/dashboard", methods=["GET", "POST"])
def dashboard():
    """
    Render the dashboard page.
    """
    if request.method == "GET":
        return render_template("browser/dashboard.html", parts=part_data.parts)

    return render_template("browser/dashboard.html", parts=part_data.parts)

@bp.route("/ecm-tree", methods=["GET", "POST"])
def ecm_tree():
    """
    Render the ecm tree view page.
    """
    if request.method == "GET":
        return render_template("browser/ecm-tree.html")

    return render_template("browser/ecm-tree.html")


if __name__ == "__main__":
    create_app()