# Lexicons
This is a directory of maintained lexicons.

## Format
Each lexicon should have its own folder here, with a unique folder name. The folder name does not have to reflect the name of the lexicon itself.

Please do not commit zipped lexicons.

Each set of lexicon files in a folder composes a single "lexicon". In order to keep track of these files, each lexicon should have a YAML metadata file
associated with it called `package.yml` that looks like this:
```yaml
name: Your lexicon name
author: Your name
disabled: true
description: Some description of what your lexicon is for.
files:
  - file0.pls
  - file1.pls
  - filen.pls
```

`disabled` is an optional field that may be set to true in order to temporarily hide the package from users (e.g. because it is broken).

## Building phonemes using the International Phonetic Alphabet (IPA)
Here are some free tools you can use to build phonomes.
- https://console.aws.amazon.com/polly/home/SynthesizeSpeech - For testing how Amazon Polly would pronounce a word be default without needing to load the game.
- https://www.macmillandictionary.com/ - For getting sample IPAs from existing words
- https://ipa.typeit.org/full/ - For building or tweaking IPAs
- http://ipa-reader.xyz/ - For testing your IPAs
