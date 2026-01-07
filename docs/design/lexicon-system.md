# Lexicon System

Lexicons customize word pronunciation by mapping written forms (graphemes) to pronunciations (phonemes) or replacement text (aliases).

```
Aether → /ˈiːθər/ (IPA phoneme)
Y'shtola → Yshtola (alias)
Miqo'te → Mee-koh-tay (alias)
```

The lexicon manager scans text for known graphemes and wraps them in SSML phoneme tags:

```xml
<speak>
  The <phoneme ph="ˈiːθər">Aether</phoneme> flows through all things.
</speak>
```

## Backend Support

SSML support varies by backend. System, Polly, Azure, and Google support full SSML with phoneme tags. ElevenLabs, OpenAI, and Kokoro only support plain text, so aliases work while phonemes do not. WebSocket passes through whatever it receives, allowing the receiving application to handle it as it chooses.

## Grapheme Matching

The matcher sorts graphemes longest-first to handle overlapping terms, for example, "Shadowbringers" matches before "Shadow" to avoid partial replacements. It tracks which parts of the text are already inside phoneme tags to avoid double-wrapping, which matters when text already contains SSML or when one lexeme's replacement contains another lexeme's grapheme.

## Community Lexicons

The `lexicons/` folder contains community-maintained pronunciation packages. Each package has a `package.yml` (metadata: name, author, files) and one or more `.pls` files (lexicon data in W3C PLS format). Users enable/disable packages in settings. The plugin loads enabled lexicons at startup and merges them into a single lookup table.

## Lexicon Updates

The `TextToTalk.Lexicons.Updater` library handles downloading and updating lexicon packages from the GitHub repository. `LexiconRepository` fetches the list of available packages by querying the GitHub tree API for `package.yml` files under `lexicons/`.

Each `LexiconPackage` downloads files on-demand from `raw.githubusercontent.com` and caches them locally. Update detection uses HTTP ETags - a HEAD request checks if the remote ETag differs from the cached one in `update.json`. If so, the file is re-downloaded. This avoids unnecessary downloads while keeping lexicons current.
