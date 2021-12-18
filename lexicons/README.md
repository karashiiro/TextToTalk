# Lexicons
This is a directory of maintained lexicons.

## Format
Each lexicon should have its own folder here, with a unique folder name. The folder name does not have to reflect the name of the lexicon itself.

Each lexicon file should be at most 4000 characters, to allow lexicons to be used with [Amazon Polly](https://docs.aws.amazon.com/general/latest/gr/pol.html#limits_polly).
Lexicons may be split into multiple files to work around this restriction. Also note that Amazon Polly only allows 5 lexicons to be used in a single request, so try to be
mindful of this. Please do not commit zipped lexicons.

Each set of lexicon files in a folder composes a single "lexicon". In order to keep track of these files, each lexicon should have a YAML metadata file
associated with it called `package.yml` that looks like this:
```yaml
name: Your lexicon name
author: Your name
description: Some description of what your lexicon is for.
files:
  - file0.pls
  - file1.pls
  - filen.pls
```
