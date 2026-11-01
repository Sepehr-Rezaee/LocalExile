using GameServer.Core.World;

// Intimidate and Unnerve from the player's hits (poedb Intimidate: "Intimidate Enemies for 4 seconds on Hit",
// "Intimidated enemies take 10% increased Attack Damage"; Grattus Signet: "Unnerved enemies take 10% increased Spell
// Damage"), Blind (poedb: 4 s base) and Exposure. Each lasts from the hit that inflicts it, a later one restarting it;
// EnemyDebuffsTick shows the buff.
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

    /// <summary>Whether a hit blinds: global_chance_to_blind_on_hit_% (the Blind support's 10%), attacks_chance_to_blind_on_hit_%
    /// for an attack and %_chance_to_blind_on_critical_strike on a critical strike, one roll for their sum.</summary>
    internal static bool BlindRoll(Func<string, int> get, bool spell, bool critical, Random random) =>
        RandomRolls.Chance(get("global_chance_to_blind_on_hit_%") + (spell ? 0 : get("attacks_chance_to_blind_on_hit_%"))
            + (critical ? get("%_chance_to_blind_on_critical_strike") : 0), 100, random);

    /// <summary>A Blind's duration: 4 s base (poedb Blind), with blind_duration_+%.</summary>
    internal static double BlindSeconds(Func<string, int> get) =>
        HitDebuffSeconds * Math.Max(0, 1 + get("blind_duration_+%") / 100.0);

    // A hit that landed on a living monster, under the Monsters lock: Random.Shared, so a projectile's own draws stay in order.
    internal static void HitDebuffsOnHitLocked(SpawnedMonster m, Func<string, int> get, bool spell, bool critical, double now)
    {
        var (intimidate, unnerve) = HitDebuffRolls(get, spell, critical, Random.Shared);
        if (intimidate) m.IntimidatedUntil = Math.Max(m.IntimidatedUntil, now + HitDebuffSeconds);
        if (unnerve) m.UnnervedUntil = Math.Max(m.UnnervedUntil, now + HitDebuffSeconds);
        if (BlindRoll(get, spell, critical, Random.Shared))
            m.BlindedUntil = Math.Max(m.BlindedUntil, now + BlindSeconds(get));
    }

    // Exposure (poedb Exposure): an unspecified value "defaults to -10% to the affected resistance", a duration "defaults to
    // 4 seconds", and "only the highest Exposure of the element applies". BuffDefinitions Fire, Cold and Lightning
    // Exposure (reduced_*_resistance_from_skill); none is IsRecovery, so 0x1c1 carries no values.
    internal const int DefaultExposure = -10;
    internal const double ExposureSeconds = 4;
    internal static readonly DamageType[] ExposureTypes = [DamageType.Fire, DamageType.Cold, DamageType.Lightning];
    internal static readonly ushort[] ExposureBuffRows = [1268, 1269, 1270];

    /// <summary>The Exposure of one element a hit inflicts, 0 for none. {element}_exposure_on_hit_magnitude always
    /// inflicts its own; inflict_all_exposure_on_hit, base_inflict_{element}_exposure_on_hit_%_chance and, for a hit with
    /// that damage, chance_to_inflict_{element}_exposure_on_hit_with_{element}_damage_% inflict the default -10%. The
    /// stronger one counts, plus the extra stats, and at least exposure_you_inflict_has_minimum_resistance_%.</summary>
    internal static int ExposureRoll(Func<string, int> get, DamageType type, bool withType, Random random)
    {
        string e = type.ToString().ToLowerInvariant();
        bool rolled = get("inflict_all_exposure_on_hit") != 0 || RandomRolls.Chance(get($"base_inflict_{e}_exposure_on_hit_%_chance")
            + (withType ? get($"chance_to_inflict_{e}_exposure_on_hit_with_{e}_damage_%") : 0), 100, random);
        int magnitude = Math.Min(rolled ? DefaultExposure : 0, get($"{e}_exposure_on_hit_magnitude"));
        if (magnitude >= 0 || get("cannot_inflict_exposure") != 0) return 0;
        magnitude += get($"{e}_exposure_you_inflict_applies_extra_{e}_resistance_+%")
            + get("exposure_you_inflict_applies_extra_%_to_affected_resistance");
        int minimum = get("exposure_you_inflict_has_minimum_resistance_%");
        return Math.Min(0, minimum < 0 ? Math.Min(magnitude, minimum) : magnitude);
    }

    // A hit that landed on a living monster, under the Monsters lock: each element's Exposure for 4 s, a later one of the
    // same magnitude restarting it.
    internal static void ExposureOnHitLocked(SpawnedMonster m, Func<string, int> get, DamagePacket mitigated, double now, Random random)
    {
        if (m.Mods.Effects["immune_to_exposure"] > 0) return;
        for (int i = 0; i < ExposureTypes.Length; i++)
        {
            var type = ExposureTypes[i];
            if (ExposureRoll(get, type, mitigated[type].Max > 0, random) is not (< 0 and var magnitude)) continue;
            double until = now + ExposureSeconds;
            m.Exposures[(type, magnitude)] = Math.Max(m.Exposures.GetValueOrDefault((type, magnitude), double.NegativeInfinity), until);
            m.ExposureUntil[i] = Math.Max(m.ExposureUntil[i], until);
        }
    }

    /// <summary>The resistance its Exposure of this type applies at <paramref name="now"/>: the highest running one, 0
    /// without a time.</summary>
    internal static int MonsterExposure(SpawnedMonster m, DamageType type, double now)
    {
        int strongest = 0;
        foreach (var ((t, magnitude), until) in m.Exposures)
            if (t == type && until > now) strongest = Math.Min(strongest, magnitude);
        return strongest;
    }

    // Its Exposure of this type ended (EnemyDebuffsTick: it ran out, or it or the player died).
    internal static void EndExposureLocked(SpawnedMonster m, DamageType type)
    {
        if (m.Exposures.Count == 0) return;
        foreach (var key in m.Exposures.Keys.Where(k => k.Type == type).ToArray()) m.Exposures.Remove(key);
    }

    /// <summary>The increased damage a hit takes from them at <paramref name="now"/>: Intimidate's on an attack, Unnerve's
    /// on a spell.</summary>
    internal static int HitDebuffTaken(SpawnedMonster m, bool spell, double now) =>
        (spell ? m.UnnervedUntil : m.IntimidatedUntil) > now ? HitDebuffTakenPct : 0;
}
