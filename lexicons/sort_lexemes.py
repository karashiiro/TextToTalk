import xml.etree.ElementTree as ET

def sort_lexemes(input_file, output_file):
    tree = ET.parse(input_file)
    root = tree.getroot()
    
    # Namespace used in the XML file
    namespace = {'pls': 'http://www.w3.org/2005/01/pronunciation-lexicon'}
    
    # Find all lexeme elements
    lexemes = root.findall('pls:lexeme', namespace)
    
    # Sort lexemes by the first grapheme value
    def get_grapheme(lex):
        grapheme = lex.find('pls:grapheme', namespace)
        return grapheme.text if grapheme is not None else ""
    sorted_lexemes = sorted(lexemes, key=lambda lex: get_grapheme(lex).lower())

    # Remove `ns0:` prefix from all tags recursively
    for elem in root.iter():
        elem.tag = elem.tag.replace('{http://www.w3.org/2005/01/pronunciation-lexicon}', '')

    # Add `xmlns="http://www.w3.org/2005/01/pronunciation-lexicon"` back into the lexicon element
    root.set('xmlns', 'http://www.w3.org/2005/01/pronunciation-lexicon')

    # Remove existing lexeme elements
    for lex in lexemes:
        root.remove(lex)
    
    # Append sorted lexeme elements
    for lex in sorted_lexemes:
        root.append(lex)

    # Write the sorted XML to the output file
    tree.write(output_file, encoding='UTF-8', xml_declaration=True)
    
if __name__ == "__main__":
    input_file = "lexicon.pls"
    output_file = "lexicon.pls"  # Warning: This will overwrite the input file

    # Set input_file and output_file to the arguments passed to the script
    import sys
    if len(sys.argv) > 1:
        input_file = sys.argv[1]
    if len(sys.argv) > 2:
        output_file = sys.argv[2]
    
    sort_lexemes(input_file, output_file)
    print(f"Sorting completed successfully. Output saved to {output_file}.")
