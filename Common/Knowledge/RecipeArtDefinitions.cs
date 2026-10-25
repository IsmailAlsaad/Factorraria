namespace Factorraria.Common.Knowledge
{
    /// <summary>
    /// THIS IS THE FILE YOU EDIT TO REGISTER RECIPE BOOK ART.
    ///
    /// An art entry is a PNG that a parchment unlocks instead of a recipe. Two steps:
    ///   1. Register the PNG here, by its mod texture path (no extension):
    ///         RecipeArtRegistry.Register("Factorraria/Content/RecipeArt/ExampleNote");
    ///      The file name (ExampleNote) is the entry's label in the book list and its unique id.
    ///   2. Put it in a scroll pool in ScrollPoolDefinitions, the same way a recipe goes in:
    ///         ScrollPools.Define("Default").AddArt("ExampleNote", weight: 1f);
    ///
    /// The book shows the PNG centered on the whole right page (330 x 402 px), shrunk to fit if bigger, never enlarged.
    /// Unlocked art is saved and synced like an unlocked recipe. Art registered but in no pool is logged as a warning
    /// (Logs\client.log, search "[ScrollPools]"), because nothing could ever unlock it.
    /// To test: /recipes list shows unlocked keys, "Art|ExampleNote" among them.
    /// </summary>
    public static class RecipeArtDefinitions
    {
        public static void Register()
        {
             RecipeArtRegistry.Register("Factorraria/Content/RecipeArt/Placeholder");
        }
    }
}