# About
These sorting scripts automatically alphabetically sort all the <lexeme> elements inside a lexicon by the text content of the first <grapheme> element.

# Prerequisites
In order for these scripts to work, you must have Python installed. Download and install Python here:
https://www.python.org/downloads/

# Simple Option
This is the simplest way of using the scripts that requires not even any commandline usage on your own.

Copy/move/drag and drop **both** the `sort_lexemes.bat` and `sort_lexemes.py` file into the same folder/directory that contains the `lexicon.pls` you want to sort.

Double click on the `sort_lexemes.bat` file

# Advanced Option
It's also possible to use either of these scripts from the commandline and not move them, but it requires just some basic commandline knowledge. Choose either the `.bat` script or the `.py` to use, the `.bat` script just calls the `.py` script.

From the Windows "Command Prompt" or the (more moddern) Windows "Terminal", in either "Command Prompt" mode or "Windows PowerShell" mode, copy and paste **one** of the following commands and replace the placeholder text.

## `sort_lexemes.bat` Usage
```
.\sort_lexemes.bat PATH_TO_EXISTING_LEXICON.pls PATH_TO_NEW_SORTED_LEXICON.pls
```

## `sort_lexemes.py` Usage
```
python .\sort_lexemes.py "PATH_TO_EXISTING_LEXICON.pls" "PATH_TO_NEW_SORTED_LEXICON.pls"
```