# Do you like tech trees?
# Do you have make on your system?
# Just type 'make' and we'll build you one!

PROJ_NAME = $(shell basename `pwd`)

TREE_SRC := tree.yml
TREE := GameData/RP-0/Tree.cfg
VERSION := $(shell git describe --tags)

ZIPFILE := $(PROJ_NAME)-$(VERSION).zip

all: $(TREE)

release: $(ZIPFILE)

$(TREE): $(TREE_SRC)
	bin/yml2mm

$(ZIPFILE): $(TREE)
	zip -r $(ZIPFILE) README.md LICENSE.md GameData
