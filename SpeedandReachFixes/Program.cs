using System;
using System.Linq;
using System.Threading.Tasks;
using Mutagen.Bethesda;
using Mutagen.Bethesda.FormKeys.SkyrimSE;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Synthesis;
using Noggog;

namespace SpeedandReachFixes
{
    public class Program
    {
        private static Lazy<Settings> _settings = null!;
        private static Settings Settings => _settings.Value;

        public static Task<int> Main(string[] args)
        {
            return SynthesisPipeline.Instance
                .AddPatch<ISkyrimMod, ISkyrimModGetter>(RunPatch)
                .SetTypicalOpen(GameRelease.SkyrimSE, "SpeedAndReachFixes.esp")
                .SetAutogeneratedSettings("Settings", "settings.json", out _settings)
                .Run(args);
        }

        public static void RunPatch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            var count = 0;
            state.PatchMod.GameSettings.Add(new GameSettingFloat(state.PatchMod.GetNextFormKey(), state.PatchMod.SkyrimRelease)
            {
                EditorID = "fObjectHitWeaponReach",
                Data = 81
            });

            state.PatchMod.GameSettings.Add(new GameSettingFloat(state.PatchMod.GetNextFormKey(), state.PatchMod.SkyrimRelease)
            {
                EditorID = "fObjectHitTwoHandReach",
                Data = 135
            });

            state.PatchMod.GameSettings.Add(new GameSettingFloat(state.PatchMod.GetNextFormKey(), state.PatchMod.SkyrimRelease)
            {
                EditorID = "fObjectHitH2HReach",
                Data = 61
            });

            foreach (var gmst in state.LoadOrder.PriorityOrder.GameSetting().WinningOverrides())
            {
                if (gmst.EditorID?.Contains("fCombatDistance") == true)
                {
                    var modifiedGmst = state.PatchMod.GameSettings.GetOrAddAsOverride(gmst);
                    ((GameSettingFloat)modifiedGmst).Data = 141;
                    ++count;
                }

                if (gmst.EditorID?.Contains("fCombatBashReach") == true)
                {
                    var modifiedGmst = state.PatchMod.GameSettings.GetOrAddAsOverride(gmst);
                    ((GameSettingFloat)modifiedGmst).Data = 61;
                    ++count;
                }
            }
            Console.WriteLine("Done adjusting Game Settings");

            if (Settings.WeaponSwingAngleChanges)
            {
                foreach (var race in state.LoadOrder.PriorityOrder.Race().WinningOverrides())
                {
                    if (!race.HasKeyword(Skyrim.Keyword.ActorTypeNPC)) continue;

                    var modifiedRace = state.PatchMod.Races.GetOrAddAsOverride(race);

                    foreach (var attack in modifiedRace.Attacks.Where(attack => attack.AttackData != null))
                    {
                        attack.AttackData!.StrikeAngle += 7;
                        ++count;
                    }
                }
                Console.WriteLine("Applied Race Weapon Swing Angle Changes");
            }

            count += (from weap in state.LoadOrder.PriorityOrder.WinningOverrides<IWeaponGetter>() where weap.Data != null select state.PatchMod.Weapons.GetOrAddAsOverride(weap) into weapon select AdjustWeaponStats(weapon) ? 1 : 0).Sum();
            Console.WriteLine("Process is Complete.\nModified [" + count + "] records.");
        }

        private static bool AdjustWeaponStats(Weapon weapon)
        {
            if (weapon.Data == null) return false;
            var stats = Settings.GetHighestPriorityStats(weapon);
            bool changedSpeed = false, changedReach = false;
            weapon.Data.Speed = stats.GetSpeed(weapon.Data.Speed, out changedSpeed);
            weapon.Data.Reach = weapon.EditorID?.ContainsInsensitive("GiantClub") == true ? 1.3F : stats.GetReach(weapon.Data.Reach, out changedReach);
            
            if (changedSpeed || changedReach) // no changes made
                Console.WriteLine("Finished Processing: " + weapon.EditorID);
            if (changedSpeed)
                Console.WriteLine("\tSpeed = " + weapon.Data.Speed.ToString("F"));
            if (changedReach)
                Console.WriteLine("\tReach = " + weapon.Data.Reach.ToString("F"));
            
            // Revert any changes to giant clubs as they may cause issues with the AI
            if (weapon.EditorID?.ContainsInsensitive("GiantClub") == true)
                weapon.Data.Reach = 1.3F;
            return changedSpeed || changedReach;
        }
    }
}
