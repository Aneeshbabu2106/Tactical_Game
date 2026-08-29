using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
///     A <see cref="RuleTile" /> with an extensible neighbor vocabulary. Project-agnostic: it adds
///     only Null / NotNull and the machinery a derived tile needs to add its own ids safely.
/// </summary>
/// <remarks>
///     <para>
///         Neighbor ids are stored in the tile asset as raw integers, so two classes claiming the
///         same id do not conflict at compile time — the authored rule simply changes meaning. To
///         extend the vocabulary:
///     </para>
///     <list type="number">
///         <item>Derive a Neighbor class from <see cref="Neighbor" />.</item>
///         <item>Number ids from <see cref="FirstDerivedNeighbor" /> upward, never reusing a value.</item>
///         <item>Override <see cref="NeighborVocabulary" /> to return that class.</item>
///         <item>Override <see cref="RuleMatch" />, handling only your own ids and delegating the rest to base.</item>
///     </list>
///     <para>
///         Overlaps are reported rather than trusted — see <see cref="FindDuplicateNeighborIds" />,
///         which runs on asset validation and once on editor load.
///     </para>
/// </remarks>
[CreateAssetMenu(
    fileName = "ExtendedRuleTile",
    menuName = "Tacticore/Rule Tiles/Extended"
)]
public class ExtendedRuleTile : RuleTile<ExtendedRuleTile.Neighbor>
{
    /// <summary>
    ///     Lowest id a derived vocabulary may claim. Everything below is reserved: 1 and 2 by
    ///     RuleTile itself (This / NotThis), 3 and 4 by <see cref="Neighbor" />.
    /// </summary>
    public const int FirstDerivedNeighbor = 5;

    private static readonly Dictionary<Type, HashSet<int>> DefinedCache = new();

    public class Neighbor : RuleTile.TilingRule.Neighbor
    {
        public const int Null = 3;
        public const int NotNull = 4;
    }

    /// <summary>
    ///     The Neighbor class holding every id this tile understands. Override alongside
    ///     <see cref="RuleMatch" /> so validation knows which ids are legal on this tile's assets.
    /// </summary>
    public virtual Type NeighborVocabulary => typeof(Neighbor);

    public override bool RuleMatch(int neighbor, TileBase tile)
    {
        switch (neighbor)
        {
            case Neighbor.Null:
                return tile == null;

            case Neighbor.NotNull:
                return tile != null;
        }

        return base.RuleMatch(neighbor, tile);
    }

    /// <summary>
    ///     Every id defined by <paramref name="vocabulary" /> and the classes it inherits from.
    /// </summary>
    public static HashSet<int> GetDefinedNeighbors(Type vocabulary)
    {
        if (DefinedCache.TryGetValue(vocabulary, out var cached))
        {
            return cached;
        }

        var defined = new HashSet<int>();

        foreach (var field in GetNeighborConstants(vocabulary))
        {
            defined.Add((int)field.GetRawConstantValue());
        }

        DefinedCache[vocabulary] = defined;
        return defined;
    }

    /// <summary>
    ///     Ids claimed by more than one constant anywhere in the vocabulary's inheritance chain.
    ///     This is the failure that cannot be caught by the compiler: the derived class wins at
    ///     runtime and every rule authored against the base meaning silently stops matching.
    /// </summary>
    public static List<string> FindDuplicateNeighborIds(Type vocabulary)
    {
        var names = new Dictionary<int, List<string>>();

        foreach (var field in GetNeighborConstants(vocabulary))
        {
            var value = (int)field.GetRawConstantValue();

            if (!names.TryGetValue(value, out var claimants))
            {
                names[value] = claimants = new List<string>();
            }

            claimants.Add($"{field.DeclaringType?.Name}.{field.Name}");
        }

        var duplicates = new List<string>();

        foreach (var pair in names)
        {
            if (pair.Value.Count > 1)
            {
                duplicates.Add($"id {pair.Key} is claimed by {string.Join(" and ", pair.Value)}");
            }
        }

        return duplicates;
    }

    private static IEnumerable<FieldInfo> GetNeighborConstants(Type vocabulary)
    {
        var fields = vocabulary.GetFields(
            BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

        foreach (var field in fields)
        {
            if (field.IsLiteral && !field.IsInitOnly && field.FieldType == typeof(int))
            {
                yield return field;
            }
        }
    }

#if UNITY_EDITOR
    /// <summary>
    ///     Catches rules holding an id this tile can no longer resolve — which is what a renumbered
    ///     or removed neighbor type leaves behind.
    /// </summary>
    private void OnValidate()
    {
        var defined = GetDefinedNeighbors(NeighborVocabulary);

        foreach (var rule in m_TilingRules)
        {
            foreach (var value in rule.m_Neighbors)
            {
                if (!defined.Contains(value))
                {
                    Debug.LogError(
                        $"{name}: rule {rule.m_Id} holds neighbor id {value}, which is not defined by "
                        + $"{NeighborVocabulary.Name}. Re-pick it in the inspector.",
                        this);
                }
            }
        }
    }
#endif
}
