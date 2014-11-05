#!/usr/bin/perl
use 5.010;
use strict;
use warnings;
use autodie;

use YAML::Tiny;

use Data::Dumper;

# YAML to ModuleManager converter
# CC-BY Paul Fenwick, 2014
# May also be used under the same terms as Perl itself.

# Don't have Perl? Try strawberry!
# http://strawberryperl.com/

# Editing this file? Make changes to the `yml2mm.lean` version, and
# compile the portable version with `fatten yml2mm.lean yml2mm`

my $DECORATION = "AFTER[RealismOverhaul]";

foreach my $file (@ARGV) {
    my $yaml = YAML::Tiny->read($file)->[0]; # The 0th YAML section.

    foreach my $tech (keys %$yaml) {
        foreach my $part (@{ $yaml->{$tech}} ) {
            say "\@PART[$part]:$DECORATION { %TechRequired = $tech }";
        }
    }
}
