namespace TextToTalk.LexiconUpdater
{
    public class LexiconPackageInfo
    {
        /// <summary>
        /// The lexicon package's name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The lexicon author's username.
        /// </summary>
        public string Author { get; set; }

        /// <summary>
        /// The lexicon package's description.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// The files that are part of the lexicon package.
        /// </summary>
        public string[] Files { get; set; }
    }
}
