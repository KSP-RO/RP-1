#!/usr/bin/perl -w
use 5.010;
use strict;
use warnings;
use autodie;
use FindBin qw($Bin);
use File::Slurp qw(read_file);
use Getopt::Std qw(getopts);

# Verbose switch handling
my %opts = ('v' => 0);
getopts('v', \%opts);

my $tree = $ARGV[0] or die "Usage: $0 tree.cfg\n";

my $block_re;

# We're about to write a recursive regexp here.
# Viewer discretion is advised.

$block_re = qr{
    \{                          # Blocks consist of :
        (?:                     # ... a non-capturing segment
            (?> [^{}]+ )        # ... of non-curlies, no backtracking
            |                   # ... OR
            (??{ $block_re })   # ... another embedded block
        )*                      # Any number of times
    \}                          # And then a close-curly.
}x;

# Okay, you can look back now. It's safe.

my $node_re  = qr{ NODE  \s* (?<node> $block_re ) }x;
my $parts_re = qr{ PARTS \s* (?<parts> $block_re ) }x;

my $raw_tree = read_file($tree);

# Here's where we do all the magic.

while ($raw_tree =~ m{$node_re}g) {
    my $node = $+{node};
    my ($techID) = $node =~ m{techID\s*=\s*(\S+)};

    next if not $techID;    # Not a tree node?

    warn "$techID...\n" if $opts{v};

    say "$techID:";

    if (not $node =~ m{$parts_re}) {
        warn "No PARTS found in $techID, skipping...";
        next;
    }

    my $parts = $+{parts};

    my @parts = $parts =~ m{name\s*=\s*(\S+)}g;

    say "    - $_" foreach @parts;

    say "";    # Blank line looks nice at the end.
}
