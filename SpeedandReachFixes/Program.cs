using Mutagen.Bethesda;
using Mutagen.Bethesda.FormKeys.SkyrimSE;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Synthesis;
using Mutagen.Bethesda.Strings;
using System;
using System.Threading.Tasks;


namespace SpeedandReachFixes
{
    public static class Program
    {
        private static Lazy<Settings> _settings = null!;
        private static Settings Settings => _settings.Value;

        public static Task<int> Main(string[] args)
        {
            return SynthesisPipeline.Instance
                .AddPatch<ISkyrimMod, ISkyrimModGetter>(RunPatch)
                .SetAutogeneratedSettings("Settings", "settings.json", out _settings)
                .SetTypicalOpen(GameRelease.SkyrimSE, "SpeedAndReachFixes.esp")
                .Run(args);
        }

        public static void RunPatch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            Console.WriteLine("\n\nInitialization successful, beginning patcher process...\n");

            // initialize the modified record counter, and add Game Setting changes to the patch.
            var count = Settings.GameSettings.AddGameSettingsToPatch(state);

            if (count > 0) // if game settings were successfully added, write to log
                Console.WriteLine("Modified " + count + " Game Setting(s).");

            // Apply attack angle modifier for all races, if the modifier isn't set to 0
            if (!Settings.AttackStrikeAngleModifier.Equals(0F))
            {

                foreach (IRaceGetter race in state.LoadOrder.PriorityOrder.Race().WinningOverrides())
                { // iterate through all races that have the ActorTypeNPC keyword.
                    if (!race.HasKeyword(Skyrim.Keyword.ActorTypeNPC) || race.EditorID == null)
                        continue; // skip this race if it does not have the ActorTypeNPC keyword

                    var raceCopy = race.DeepCopy();
                    
                    if (raceCopy.Name != null && raceCopy.Name.TryLookup(Language.French, out string i18nRaceName)) {
                        raceCopy.Name = i18nRaceName;
                    }
                    if (raceCopy.Description != null && raceCopy.Description.TryLookup(Language.French, out string i18nRaceDescription)) {
                        raceCopy.Description = i18nRaceDescription;
                    }
                    var subrecordChanges = count;
                    foreach (var attack in raceCopy.Attacks)

                    {
                        if (attack.AttackData == null)
                            continue;
                        attack.AttackData.StrikeAngle = Settings.GetModifiedStrikeAngle(attack.AttackData.StrikeAngle);
                        ++count; // iterate counter by one for each modified attack
                    }
                    subrecordChanges = count - subrecordChanges;
                    if (subrecordChanges > 0)
                    {
                        state.PatchMod.Races.Set(raceCopy);
                        Console.WriteLine("Modified " + subrecordChanges + " attacks for race: " + race.EditorID);
                    }
                }
            }

            // Apply speed and reach fixes to all weapons.
            foreach (var weap in state.LoadOrder.PriorityOrder.WinningOverrides<IWeaponGetter>())
            {

                if (weap.Data == null || weap.EditorID == null)
                    continue;

                var weapon = weap.DeepCopy(); // copy weap record to temp
                
                if (weapon.Name != null && weapon.Name.TryLookup(Language.French, out string i18nWeaponName)) {
                    weapon.Name = i18nWeaponName;
                }
                
                if (Settings.ApplyChangesToWeapon(weapon))
                { // if temp record was modified
                    state.PatchMod.Weapons.Set(weapon); // set weap record to temp
                    Console.WriteLine("Successfully modified weapon: " + weap.EditorID);
                    ++count;
                }
            }

            // Log the total number of records modified by the patcher.
            Console.WriteLine("\nFinished patching " + count + " records.\n");
        }
    }
}
