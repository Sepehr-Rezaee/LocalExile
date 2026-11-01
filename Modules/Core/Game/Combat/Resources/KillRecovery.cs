/// <summary>
/// Life, Mana and Energy Shield the player recovers when they kill an enemy: the flat "X Life gained on Kill" (base plus
/// per character level) and "Recover X% of Life on Kill" of the maximum pool. PoB (CalcOffence "LifeOnKill") floors the
/// sum and applies no recovery rate. The cannot-recover Life/ES flags stop their pool's share.
/// </summary>
readonly record struct KillRecovery(int Life, int LifePct, int Mana, int ManaPct, int EnergyShield, int EnergyShieldPct)
{
    public static KillRecovery From(Func<string, int> get, int level)
    {
        bool life = get("cannot_recover_life") == 0, shield = get("cannot_recover_energy_shield") == 0;
        return new(
            life ? get("base_life_gained_on_enemy_death") + get("life_gained_on_enemy_death_per_level") * level : 0,
            life ? get("recover_%_maximum_life_on_kill") : 0,
            get("base_mana_gained_on_enemy_death") + get("mana_gained_on_enemy_death_per_level") * level,
            get("recover_%_maximum_mana_on_kill") + get("maximum_mana_%_gained_on_kill"),
            shield ? get("base_energy_shield_gained_on_enemy_death") + get("energy_shield_gained_on_enemy_death_per_level") * level : 0,
            shield ? get("recover_energy_shield_%_on_kill") : 0);
    }
}

static partial class Game
{
    // A hostile monster the player killed (MonsterKilled, CommitProjectileKill): its "on kill" recovery, at once. The
    // amounts follow the last player stat snapshot (ConfigureLeech), so a pack's deaths do not each rebuild the stats.
    internal static void RecoverOnKill(ClientState conn)
    {
        lock (conn.Monsters)
        {
            var session = LeechOf(conn);
            var k = session.OnKill;
            if (conn.PlayerLife == 0 || k == default) return;
            static double Amount(int flat, int pct, uint maximum) => Math.Max(0, Math.Floor(flat + maximum * (double)pct / 100));
            uint life = conn.PlayerLife, mana = conn.PlayerMana;
            conn.PlayerLife = RecoveryPool.Advance(life, conn.PlayerMaxLife, Amount(k.Life, k.LifePct, conn.PlayerMaxLife),
                ref session.LifeInstantCarry, minimum: 1);
            conn.PlayerMana = RecoveryPool.Advance(mana, (uint)UnreservedMana(conn), Amount(k.Mana, k.ManaPct, conn.Pools.Mana),
                ref session.ManaInstantCarry);
            conn.EnergyShield.Gain(Amount(k.EnergyShield, k.EnergyShieldPct, conn.EnergyShield.Maximum));
            // Sent with the next vitals update, as instant leech is.
            session.InstantLifeChanged |= life != conn.PlayerLife;
            session.InstantManaChanged |= mana != conn.PlayerMana;
        }
    }
}
