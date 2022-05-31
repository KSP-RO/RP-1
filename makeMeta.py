import os, argparse, sys, json, glob, re

class DefaultHelpParser(argparse.ArgumentParser):
    def error(self, message):
        sys.stderr.write('error: %s\n' % message)
        self.print_help()
        sys.exit(2)

HELP_DESC = "Creates neccesary metadata files"
parser = DefaultHelpParser(description=HELP_DESC)
parser.add_argument('tag', metavar='tag', type=str, nargs=1,
                   help='tag of release (e.g. 0.4.6.0')

args = parser.parse_args()

if not args.tag or len(args.tag) < 1:
    print("ERROR: git tag must be specified and must be in the format major.minor.patch.build-configuration.e.g. 0.4.6.0")
    sys.exit(2)

version = args.tag[0]

if version.startswith('v'):
    version = version.split('v')[1]

major = int(version.split(".")[0])
minor = int(version.split(".")[1])
patch = int(version.split(".")[2])
build = int(version.split(".")[3])
# create AVC .version file
avc = {
	"NAME" : "Realistic Progression One",
	"URL" : "https://raw.githubusercontent.com/KSP-RO/RP-0/master/GameData/RP-0/RP-1.version",
	"DOWNLOAD" : "https://github.com/KSP-RO/RP-0/releases/download/{}/RP-1-{}.zip".format(args.tag[0],args.tag[0]),
	"HOMEPAGE"  : "https://github.com/KSP-RO/RP-0/",
	"VERSION" :
	{
		"MAJOR" : major,
		"MINOR" : minor,
		"PATCH" : patch,
		"BUILD" : build
	},
	"KSP_VERSION" : {
		"MAJOR": "1",
		"MINOR": "12",
		"PATCH": "3"
	},
	"KSP_VERSION_MIN": {
		"MAJOR": "1",
		"MINOR": "12",
		"PATCH": "0"
	},
	"KSP_VERSION_MAX": {
		"MAJOR": "1",
		"MINOR": "12",
		"PATCH": "99"
	}
}
with open("RP-1.version", "w") as f:
	f.write(json.dumps(avc, indent=4))

# Replace old version tag in readme
new_string = "https://github.com/KSP-RO/RP-0/compare/v"+version+"...master"
new_readme = []
with open("README.md", "r") as f:
	for line in f.readlines():
		replaced = re.sub(r'https://github.com/KSP-RO/RP-0/compare/v[\d|.]*...master', new_string, line)
		new_readme.append(replaced)

with open("README.md", "w") as f:
	f.writelines(new_readme)
