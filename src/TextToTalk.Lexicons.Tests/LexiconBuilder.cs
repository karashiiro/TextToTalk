using System.Collections.Generic;
using System.Linq;

namespace TextToTalk.Lexicons.Tests
{
    public class LexiconBuilder
    {
        private readonly IList<Lexeme> lexemes;

        public LexiconBuilder()
        {
            this.lexemes = new List<Lexeme>();
        }

        public LexiconBuilder WithLexeme(Lexeme lexeme)
        {
            this.lexemes.Add(lexeme);
            return this;
        }

        public string Build()
        {
            return @$"<?xml version=""1.0"" encoding=""UTF-8""?>
<lexicon version=""1.0"" 
      xmlns=""http://www.w3.org/2005/01/pronunciation-lexicon""
      xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" 
      xsi:schemaLocation=""http://www.w3.org/2005/01/pronunciation-lexicon 
        http://www.w3.org/TR/2007/CR-pronunciation-lexicon-20071212/pls.xsd""
      alphabet=""ipa"" 
      xml:lang=""en"">
    {this.lexemes.Aggregate("", (agg, next) => {
        agg += next.Graphemes.Aggregate("", (aggGrapheme, nextGrapheme) => aggGrapheme + $"<grapheme>{nextGrapheme}</grapheme>\n");

        agg += $"<phoneme>{next.Phoneme}</phoneme>\n";

        if (!string.IsNullOrEmpty(next.Alias)) {
            agg += $"<alias>{next.Alias}</alias>\n";
        }

        return agg;
    })}
</lexicon>";
        }
    }
}