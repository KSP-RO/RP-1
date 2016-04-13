# Do you like tech trees?
# Do you have make on your system?
# Just type 'make' and we'll build you one!

PROJ_NAME = $(shell basename `pwd`)

TREE_SRC := tree.yml
TREE := GameData/RP-0/Tree.cfg

AVC_SRC := RP-0.version.in
AVC := GameData/RP-0/RP-0.version

VERSION := $(shell git describe --tags)

ZIPFILE := $(PROJ_NAME)-$(VERSION).zip

all: $(TREE) $(AVC)

release: $(ZIPFILE)

$(TREE): $(TREE_SRC)
	bin/yml2mm

$(AVC): $(AVC_SRC)
	bin/makeversion

$(ZIPFILE): $(TREE) $(AVC)
	zip -r $(ZIPFILE) README.md LICENSE.md GameData
