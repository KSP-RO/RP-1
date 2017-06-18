# Do you like tech trees?
# Do you have make on your system?
# Just type 'make' and we'll build you one!

PROJ_NAME = $(shell basename `pwd`)

AVC := GameData/RP-0/RP-0.version

VERSION := $(shell git describe --tags)

ZIPFILE := $(PROJ_NAME)-$(VERSION).zip

all: $(AVC)

release: $(ZIPFILE)


# Always rebuild AVC files, because it depends upon
# git version info, which Make can't comprehend.
$(AVC): FORCE
	bin/makeversion

$(ZIPFILE): $(AVC)
	zip -r $(ZIPFILE) README.md LICENSE.md GameData

# This is a magic target that forces anything that
# depends upon it to run.
FORCE:
