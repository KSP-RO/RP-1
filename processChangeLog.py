import argparse, sys

class DefaultHelpParser(argparse.ArgumentParser):
	def error(self, message):
		sys.stderr.write('error: %s\n' % message)
		self.print_help()
		sys.exit(2)

HELP_DESC = "Process and prepend changelog file"
parser = DefaultHelpParser(description=HELP_DESC)
parser.add_argument('tag', metavar='tag', type=str, nargs=1,
				   help='tag of release (e.g. 0.4.6.0')
parser.add_argument('body', metavar='body', type=str, nargs=1,
				   help='body of new changelog')
parser.add_argument('path', metavar='path', type=str, nargs=1,
				   help='path to changelog.cfg')

args = parser.parse_args()

if not args.tag or len(args.tag) < 1:
	print("ERROR: git tag must be specified and must be in the format major.minor.patch.build-configuration.e.g. 0.4.6.0")
	sys.exit(2)

if not args.body or len(args.body) < 1:
	print("ERROR: changelog body must be specified")
	sys.exit(2)

if not args.path or len(args.path) < 1:
	print("ERROR: output path must be specified")
	sys.exit(2)

version = args.tag[0]
body = args.body[0]
path = args.path[0]

if version.startswith('v'):
	version = version.split('v')[1]

def create_subChange_from_item(item):
	# Remove "by @author in https..."
	actual_item = item.split(" by @")[0]
	output = ""
	nr_tabs = 3
	#	subChange = "actual_item"
	output += "\n"+nr_tabs*"\t"+"subchange = " + actual_item
	return output

def split_text(text):
	return text.split("\n")

def create_change_from_category(category, text):
	output = ""
	nr_tabs = 2
	#	CHANGE
	#	{
	#		change = "category"
	#		...
	#	}
	output += "\n"+nr_tabs*"\t"+"CHANGE\n"+nr_tabs*"\t"+"{\n"+(nr_tabs+1)*"\t"+"change = " + category

	for row in split_text(text):
		output += create_subChange_from_item(row)

	output += "\n"+nr_tabs*"\t"+"}"
	return output

def split_body(body):
	category = ""
	items = ""
	for line in body.split("\n"):
		if line.startswith("## "):
			if category != "":
				yield (category, items)
			category = line.split("## ")[1]
			items = ""
		elif line.startswith("* "):
			item = line.split("* ")[1]
			if items != "":
				items += "\n"
			items += item
	yield (category, items)

def create_version(version, body):
	output = ""
	nr_tabs = 1
	#	VERSION
	#	{
	#		version = "version"
	#		versionKSP = 1.12.3
	#		...
	#	}
	HEADER = "\n"+nr_tabs*"\t"+"VERSION\n"+nr_tabs*"\t"+"{"
	version_text = "\n"+(nr_tabs+1)*"\t"+"version = " + version + "\n" + (nr_tabs+1)*"\t" + "versionKSP = 1.12.3"
	output += HEADER + version_text

	for (category, items) in split_body(body):
		output += create_change_from_category(category, items)

	output += "\n"+nr_tabs*"\t"+"}"
	return output

def compare_versions(new, old):
	"""
	returns true if new version is greater than old version
	"""
	for i in range(4):
		if int(new.split(".")[i]) > int(old.split(".")[i]):
			return True
		if int(new.split(".")[i]) < int(old.split(".")[i]):
			return False
	return False

def check_previous_version(version, path):
	"""
	finds last version in the changelog and returns true if new version is greater than last version
	"""
	with open(path, "r") as f:
		for row in f.readlines():
			if "version = " in row:
				old_version = row.split("version = ")[1]
				return compare_versions(version, old_version)
		return True

if not check_previous_version(version, path):
	print("ERROR: version was not greater than last version")
	sys.exit(2)

def insert_new_version(output, path):
	output_list = [e+"\n" for e in output.split("\n") if e]
	with open(path, "r") as f:
		text = f.readlines()
		index = -1
		for row in text:
			index += 1
			if "VERSION" in row:
				break
		before = text[:index]
		after = text[index:]
		new_text = before + output_list + after
	with open(path, "w") as f:
		f.writelines(new_text)

output = create_version(version, body)
insert_new_version(output, path)
