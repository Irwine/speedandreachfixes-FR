using System;
using System.Linq;
using System.Threading.Tasks;
using Mutagen.Bethesda;
using Mutagen.Bethesda.FormKeys.SkyrimSE;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Synthesis;

namespace SpeedandReachFixes
{
    public class Program
    {
        private static Lazy<Settings> _settings = null!;
        private static Settings Settings => _settings.Value;
        
        public static Task<int> Main(string[] args)
        {
            return SynthesisPipeline.Instance
                .AddPatch<ISkyrimMod, ISkyrimModGetter>( RunPatch )
				.SetAutogeneratedSettings( "Settings", "settings.json", out _settings )
				.SetTypicalOpen(GameRelease.SkyrimSE, "SpeedAndReachFixes.esp")
                .Run(args);
        }
        
        public static void RunPatch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
			Console.WriteLine("\n\nInitialization successful, beginning patcher process...\n");

			// initialize the modified record counter, and add Game Setting changes to the patch.
			var count = Settings.GameSettings.AddGameSettingsToPatch( state );

			if ( count > 0 ) // if game settings were successfully added, write to log
				Console.WriteLine("Successfully modified " + count + " Game Setting(s).");

			// Apply attack angle modifier for all races, if the modifier isn't set to 0
			if ( !Settings.AttackStrikeAngleModifier.Equals( 0F ) )
				foreach ( var race in state.LoadOrder.PriorityOrder.Race().WinningOverrides().Where( race => race.HasKeyword( Skyrim.Keyword.ActorTypeNPC ) ) ) { // iterate through all races that have the ActorTypeNPC keyword.
					var last = count;
					foreach ( var attack in state.PatchMod.Races.GetOrAddAsOverride( race ).Attacks.Where( attack => attack.AttackData != null ) ) // iterate through all attacks that have attack data.
					{
						attack.AttackData!.StrikeAngle = Settings.GetModifiedStrikeAngle( attack.AttackData.StrikeAngle );
						++count;
					}
					Console.WriteLine("Modified " + (count - last) + " attacks for race: " + race.EditorID);
				}
            
			// Apply speed and reach fixes to all weapons.
            foreach (var weap in state.LoadOrder.PriorityOrder.WinningOverrides<IWeaponGetter>().Where(weap => weap.Data != null)) {
                if (Settings.ApplyChangesToWeapon(state.PatchMod.Weapons.GetOrAddAsOverride(weap))) {
                    Console.WriteLine("Successfully modified weapon: " + weap.EditorID);
                    ++count;
                } // Else, remove weapon from patch to prevent ITMs
                else state.PatchMod.Weapons.Remove(weap);
            }

            // Log the total number of records modified by the patcher.
            Console.WriteLine("\nFinished patching " + count + " records.\n");
        }
    }
}
