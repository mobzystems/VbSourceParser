# VbSourceParser

This console application 'parses' VB.NET source code to detect strings and comments.
It's not really a parser, more of an extractor: it extracts strings and comments
from VB.NET source files.

Its primary intention is to facilitate searching in strings only.

Usage:

  VbSourceParser [-l] [-s] [-c] filename [filename...]

e.g.:

Some of none of the options:

-l: long output - show file names and line numbers
-s: output strings
-c: output comments

followed by at least one file name. -c and/or -s must be specified.

The output is of the form:

filename:line number: result

where result is either the string ("" or $"") or a comment (' xxx or REM xxx)

filename and line number are only present if -l is specified.
