//changes to science mechanics based on kerbalism's modules.
//patches are structured in this convoluted way for ease of patching new parts and mods.
//=================================================================================================================================================================

	Tested on KSP 1.6.1; no idea what, how, when, if, wether, etc. works on other game versions.
	This will break existing saves.

//=================================================================================================================================================================
//stock and supported mods are patched "properly" (pay attention to quotation marks)
//mods that use stock experiment module AND one of the stock experiments are affected, again "properly".
//unsupported ones retain their default behavior (instant science for the click of a button) and are unaffected by this patch bundle.

//=================================================================================================================================================================

//there's an option to add experiments to new parts, via groups. I added together a bunch of experiments that make sense to be together,
//such as atmospheric, surface, orbital, sensor, etc. (this is the reason why patch file structure is so convoluted)
//this enables a button on your RMB UI that allows you to select which experiments available from the group you want to install on your part in the editor.
//the stock groups are fairly lackluster, simply because stock has very few experiments. This was intended for mods that add a bunch of experiments and you want 
//a way of grouping them together in a somewhat sensible manner
//patching the parts yourself should be fairly straightforward, and take less than a couple of minutes per part.
//there's a Template.txt in PartPatches folder that shows exactly how to do it and explains what goes where.

//should you wish to change the group compositions, go to Groups, then mod by mod in ModSupport. it's a nightmare, would not recommend. 0/10

//=================================================================================================================================================================
//most of the data scales/data collection rates/ec/s were changed to my own taste, and I have my own version of "balance". still needs a bunch of testing.
//should you wish to fiddle around with stuff, relevant bits are at the top of the config files. strongly recommended you don't mess with anything else, as stuff is bound to break.
//=================================================================================================================================================================
//should you simply wish to slap a bunch of specific experiments into a part (say, a probe core) without the ability to select through configure,

@PART[whatever part name]:NEEDS[FeatureScience]:BEFORE[Kerbalism]
//if part is from a mod, add the mod's name to the NEEDS[] bit, e.g. @PART[MyPart]:NEEDS[FeatureScience,MyMod]:FOR[Kerbalism]
{
	MODULE
		{
			name = Experiment
			experiment_id = 			//<------------- experimentID from stock ScienceDefs.cfg or each individual mod's science defs (don't forget about the :NEEDS[MyMod] at the MODULE level if experiment is from a mod.)
		}
		//add this module for each experiment. everything else should be taken care of by other patches in this bundle.
}

//IF YOU WANT CUSTOM VALUES, OTHER THAN WHAT'S PROVIDED (faster sampling rate, less ec/s, whatever else DIFFERENT than default configured values for your experiments), you'll have to do it manually.
//there's documentation on each field for the Experiment module. Needs to run at :FOR[zzzKerbalism] to take effect.
