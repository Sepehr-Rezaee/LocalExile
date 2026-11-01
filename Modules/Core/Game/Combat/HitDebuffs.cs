using GameServer.Core.World;

// Intimidate and Unnerve from the player's hits (poedb Intimidate: "Intimidate Enemies for 4 seconds on Hit",
// "Intimidated enemies take 10% increased Attack Damage"; Grattus Signet: "Unnerved enemies take 10% increased Spell
// Damage"). Each lasts 4 s from the hit that inflicts it, a later one restarting it; EnemyDebuffsTick shows the buff.
static partial class Game
{
    internal const double HitDebuffSeconds = 4;
    internal const int HitDebuffTakenPct = 10;
    // BuffDefinitions intimidated and unnerved; neither is IsRecovery, so 0x1c1 carries no values.
    internal const ushort IntimidatedBuffRow = 733, UnnervedBuffRow = 1412;

    /// <summary>Whether a hit intimidates and unnerves: chance_to_intimidate_on_hit_% (and attacks_intimidate_on_hit_%
    /// for an attack), chance_to_unnerve_on_hit_% (and chance_to_unnerve_on_hit_with_spells_% for a spell), and Grattus
    /// Signet's crits: spells_inflict_intimidate_on_crit, attacks_inflict_unnerve_on_crit.</summary>
    internal static (bool Intimidate, bool Unnerve) HitDebuffRolls(Func<string, int> get, bool spell, bool critical, Random random)
    {
        bool intimidate = spell && critical && get("spells_inflict_intimidate_on_crit") != 0
            || RandomRolls.Chance(get("chance_to_intimidate_on_hit_%") + (spell ? 0 : get("attacks_intimidate_on_hit_%")), 100, random);
        bool unnerve = !spell && critical && get("attacks_inflict_unnerve_on_crit") != 0
            || RandomRolls.Chance(get("chance_to_unnerve_on_hit_%") + (spell ? get("chance_to_unnerve_on_hit_with_spells_%") : 0), 100, random);
        return (intimidate, unnerve);
    }

    // A hit that landed on a living monster, under the Monsters lock: Random.Shared, so a projectile's own draws stay in order.
    internal static void HitDebuffsOnHitLocked(SpawnedMonster m, Func<string, int> get, bool spell, bool critical, double now)
    {
        var (intimidate, unnerve) = HitDebuffRolls(get, spell, critical, Random.Shared);
        if (intimidate) m.IntimidatedUntil = Math.Max(m.IntimidatedUntil, now + HitDebuffSeconds);
        if (unnerve) m.UnnervedUntil = Math.Max(m.UnnervedUntil, now + HitDebuffSeconds);
    }

    /// <summary>The increased damage a hit takes from them at <paramref name="now"/>: Intimidate's on an attack, Unnerve's
    /// on a spell.</summary>
    internal static int HitDebuffTaken(SpawnedMonster m, bool spell, double now) =>
        (spell ? m.UnnervedUntil : m.IntimidatedUntil) > now ? HitDebuffTakenPct : 0;
}
