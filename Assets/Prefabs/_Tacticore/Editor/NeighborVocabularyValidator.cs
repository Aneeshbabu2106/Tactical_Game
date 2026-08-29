using UnityEditor;
using UnityEngine;

/// <summary>
///     Fails loudly on the one mistake the compiler cannot catch: two neighbor constants claiming
///     the same id. The derived class wins at runtime, so every rule authored against the other
///     meaning stops matching silently — which is exactly how doors stopped respecting their rules.
/// </summary>
public static class NeighborVocabularyValidator
{
    [InitializeOnLoadMethod]
    private static void ValidateOnLoad()
    {
        Validate(typeof(ExtendedRuleTile.Neighbor));

        foreach (var vocabulary in TypeCache.GetTypesDerivedFrom<ExtendedRuleTile.Neighbor>())
        {
            Validate(vocabulary);
        }
    }

    [MenuItem("Tacticore/Validate Neighbor Vocabularies")]
    private static void ValidateFromMenu()
    {
        ValidateOnLoad();
        Debug.Log("Neighbor vocabulary check complete — see errors above, if any.");
    }

    private static void Validate(System.Type vocabulary)
    {
        foreach (var duplicate in ExtendedRuleTile.FindDuplicateNeighborIds(vocabulary))
        {
            Debug.LogError(
                $"Neighbor id collision in {vocabulary.Name}: {duplicate}. "
                + "Renumber one of them from ExtendedRuleTile.FirstDerivedNeighbor upward — "
                + "rules already authored against the losing constant now match nothing.");
        }
    }
}
