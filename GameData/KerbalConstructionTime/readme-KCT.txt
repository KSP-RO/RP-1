About:
Kerbal Construction Time is a plugin which, at its core, is designed to make vessels take time to build rather than being able to constantly launch new vessels one after another. This core feature is expanded upon to create a larger system for managing your space center. Build your vessels, upgrade your facilities, research new technologies, and make decisions where you never had to before (Do I upgrade the VAB further or start upgrading the SPH? Do I want to build single vessels quickly, or multiple vessels at once? Is getting new tech faster worth building ships slower? Do I use these old parts to be ready for the transfer window, or do I use new parts with better stats?) When combined with other realism based addons, especially life support addons, the experience is even better. No longer can you send out a rescue mission without consequence, making planning that much more important.

KCT has even more features than what is mentioned here. Check out the forum page for a (mostly) full list, or the Getting Started Guide for a detailed explanation of all of the features.

Forum page: http://forum.kerbalspaceprogram.com/threads/92377-0-24-2-Kerbal-Construction-Time-Release-v1-0-1-%288-31-14%29
Source code: https://github.com/magico13/KCT
Issue Tracker: https://github.com/magico13/KCT/issues


Installation instructions:
Merge the GameData folder with the GameData folder in your KSP install. For Steam users, this is generally C:\Program Files (x86)\Steam\SteamApps\common\Kerbal Space Program.
When updating from an older version, generally only the KerbalConstructionTime.dll needs to be replaced, though to be safe you should delete the entire KerbalConstructionTime folder.

Submitting bug reports:
Submit bug reports to the github issue tracker or the forum page listed above. Please include the output_log.txt file from the KSP_Data folder (or KSP_x64_Data if using 64 bit) from a play-session where the bug occured with your bug report or else I probably can't help you. Please try to make sure that KCT is actually causing the issue by removing all other mods and testing, then by using all other mods except KCT. If the problem occurs without KCT, it's probably not KCT. If the problem occurs with JUST KCT, then it is very likely a KCT problem.
If at all possible, please include directions about how to replicate the issue and screenshots or video as appropriate!

Issue tracker: https://github.com/magico13/KCT/issues

Changelog:
v1.4.0.68 (2018-03-24)
 - Update to KSP 1.4.1
 - Removed part inventory, optionally use ScrapYard mod instead.
 - Build list and editor UI are now clamped to the screen such that the center of the window cannot go off-screen.
 - Build list will remember if it was visible in the space center scene and will try to resume that state
 - Build list is now slightly wider, 500 pixels instead of 400
 - Ships recovered into storage will no longer end up completely in the ceiling
 - Crew selector will try to use the crew layout set in the editor, unless kerbals are missing
 - Build rates are cached for use in other scenes where they can't be calculated correctly do to missing building level information
 - Support for CrewRandR added. Crew that are grounded are not allowed to be on flights.
 - New method of handling KSC upgrades by overriding the upgrade button on the UI. Less prone to errors since it doesn't have to downgrade buildings.
 - Round level checks when getting building levels. Fixes various issues, especially with Custom Barn Kit.
 - Added variables to some formulas for max building level (LM for launchsite max and ELM for editor max)
 - Fixed issues with support for Editor Time modlet.
 - Fixed bug where build rate 2 was showing up when it shouldn't be when changing presets.
 - Numerous additions added by NathanKell, some of which I don't really know what they do:
    - Fake-unlock tech nodes that are being researched when entering R&D
    - Support external checking of crew availability per part
    - Show rollout costs in the build UI
    - Add support for Global_Variables
    - Allow limiting the number of "rush build" clicks
    - Support tracking the number of stages, number of staging parts, and cost of staging parts
 - Update the rollout time/costs when switching launchpad
 - Refresh parts when duplicating vessels (ScrapYard specific). Fixes issue where inventory parts were being duplicated.
 - Integrate with the Making History Expansion's multiple launchsite feature. Can choose launchsite from BL+ window as before or from editor when building.
 - Moved settings into PluginData folder.
 - Some trickery to make sure the editor window doesn't get shrunk, as is happening for some as yet to be determined reason.
 - Various refactoring and removal of unused code to improve stability, maintainability, and performance.

v1.3.4.0 (06/25/16)
 - Update to KSP 1.1.3
 
v 1.3.3.7 (05/09/16)
 - Update for KSP 1.1.2
 - Now requires MagiCore for math and time parsing
 - Added Part Variables and Module Variables to alter the EffectivePart and ProceduralPart formulas for specific parts/modules
 - Several bug fixes
 
v1.3.3.0-pre (04/22/16)
 - Pre-release update for KSP v1.1
 
v1.3.2.0 (03/07/16)
 - Hotfix for potentially game breaking bug regarding build sizes in the SPH
 
v1.3.1.0 (01/13/16)
 - Fix for errors caused when recovering craft to storage with missing symmetry counterparts
 - Fix for launchpads being stuck at level zero when adding to new save
 - Fix for lingering recon/rollout when associated craft doesn't exist
 - Always stop warp when the "warp to" item finishes

v1.3.0.0 (01/06/16)
Notable Additions and Changes:
 - Multiple launchpads added. Build additional pads to launch ships more often. Each pad has its own rollout and reconditioning timers, along with upgrade and damage states. Can be renamed as well.
 - All time input fields now support using y, d, h, m, and s for years, days, hours, minutes, and seconds. Can mix and match as needed.
 - You are now warned when a ship contains invalid parts instead of the game breaking. You have the option to ignore it for now or delete the vessels. If you delete them, you get the funds back. All the offending ships and parts are logged to a file in the save folder.
 - Improved KSCSwitcher support. Now properly sets the default KSC, each KSC can have its own launchpads, and upgrade points can be shared between them.
 - Rollout and Launch buttons now colored green if pad is OK, yellow if it is being reconditioned, and red if it is destroyed.
 - Limited to one rollout or rollback at a time per launchpad. (can't rollout when something is rolling back)
 - Wheels, Mystery Goo, and the Science Jr. are now reset properly on recover to storage (might need to delete the KCT_ModuleTemplates.cfg file)
 - Modules to be reset on recover to storage can now have a "parts = part1,part2,part3" line to limit that module to being reset only for those parts
 - Warp To functionality improved. Now much, much faster to warp down.
 - Tech Node Research cancellation must now be confirmed.
 - Delay for moving a vessel to orbit in simulations is now configurable.
 - Simulations should (hopefully) no longer mess up orbits when they time out and you purchase more time.
 
Preset Updates:
 - Added several "Crew Variables" to several formulas. Check the Wiki page on Variables for more info.
 - New formula: NewLaunchPadCostFormula (pretty self-explanatory)
 - New option: SharedUpgradePool. When True, all KSCS share a single upgrade pool instead of each having their own. Default is False
 
Warnings:
 - The launchpad changes are potentially save breaking if it doesn't update to the new system correctly. That bug should be fixed, however.
 - KCT now "upgrades" buildings A LOT. Any mod listening to the OnKSCFacilityUpgrading or OnKSCFacilityUpgraded events will likely be INCOMPATIBLE. Please notify me if you find any such mods and I'll see what I can do.

Other:
 - New artwork thanks to a friend of mine!
 
v1.2.3.0 (11/09/15)
 - Update to KSP 1.0.5
 
v1.2.2.0 (8/25/15)
 - Fixed several issues with Tech node rates
 - Fixed an issue with automatic updating of Presets
 - Switched to using Planetarium.fetch.Home rather than searching for "Kerbin" or "Earth"
 - Fixed some display issues with science per day/year
 - Made the Upgrades window ever so slightly wider, plus a few other minor tweaks
 - Should now fill tanks of Procedural Parts correctly
 - Fixed bug where resetting upgrades wasn't incrementing the counter
 - Rates should be more correct after saving Presets now (w/o requiring scene change)
 
v1.2.1.0 (8/21/15)
 - Some tweaks to the UpFree Preset so that VAB upgrades affect the build rates and tech nodes research quicker
 - Re-added automatic update checking for development builds (only)
 - When pressing the launch button in the editor, the launch/sim window now appears at the mouse as a "drop-down"-like menu. To switch back, change WindowMode to 0 in the KCT_Config.txt file.
 - Some potential fixes for the weird fluctuations people are seeing in Upgrade totals
 - The vessel launch dialog no longer opens when clicking the runway/launchpad, but now the Build List will open to the appropriate tab automatically
 - Added support for my EditorTime modlet

v1.2.0.0 (7/30/15)
WARNING: This update is semi-save breaking. If you're using custom configs you MUST update them to the new Presets system.
 - Presets! Easily create, share, and switch between different settings. Mod authors can include Presets in their releases (see RP-0), players can save their most commonly used configurations, and config modders can create any number of new gameplay styles and easily share them.
 - Numerous new settings and formulas.
 - Clear out the part inventory in exchange for upgrade points.
 - Kerbal Konstructs support. Different launch sites now have their own rollout and reconditioning queues.
 - Rollout times are displayed when hovering over the rollout button.
 - Simulations can now be performed without recovering craft at the launch site and even if the vessel is too big for the launch site.
 - You can now build vessels that are too big to launch, but can't launch them until you upgrade the facilities.
 - Kinda crappy half-finished attempt to disable RemoteTech during simulations. Doesn't work properly if there's an antenna on the craft. I'll get this fixed for a hotfix later on.
 - Included several "stock" Presets: default, 7 days (every launch requires 7 days of down time), Up Free (doesn't use the KCT upgrade system), and simOnly (disables build times and just uses simulations)
 - Included Rodhern's Low-Tech Preset, which starts out easier than Default. http://forum.kerbalspaceprogram.com/threads/69310-Kerbal-Construction-Time-StageRecovery-Dev-Thread?p=2086346&viewfull=1#post2086346
 - Removed built in update checker. With KSP-AVC, CKAN, Kerbal Stuff and others, it isn't needed anymore.
 
v1.1.8.0 (6/18/15)
 - Fixed several GUI issues
 - Main GUI now uses buttons (actually Toggles) rather than a Toolbar, meaning warping won't close it now
 - Moved KSC upgrades into the Tech tab
 - Added ability to disable TestFlight part failures during simulations
 - Quicksaves are now made automatically when recovering vessels to the inventory
 - Fixed issues with RSS and simulations now that RSS uses Kopernicus
 
v1.1.7.0 (5/16/15)
 - Fixed issues when loading tourists
 - Supports Regex's KSCSwitcher now
 - Fixed issue with creating a new save after loading one where KCT is disabled
 - Fixed bug with facility upgrades failing when loading Space Center from Flight scene
 - Updated StageRecovery, Toolbar, and KAC wrappers.

v1.1.6.0 (4/29/15)
 - Update for KSP 1.0
 - KSC Upgrade time halved by default
 - KSC building upgrades now wait until you reenter the Space Center scene to complete
 - Removed chute based part recovery, now requires StageRecovery for that
 - Removed April Fool's joke references
 - Possible fix for random explosions that happen when switching vessels during simulations
 - Improved functioning of the KCT toolbar button+GUI
 - Windows that are meant to be centered are going to be centered, $DEITY damnit!
 - Build List is (again) movable when using Blizzy's toolbar
 
v1.1.5.0 (3/24/15)
New Features:
 - The Most Requested Feature: Recovering directly to storage. Be warned that it likely has bugs, especially with mod parts. Requires manual refuelling of ships.
 - KSC Upgrades now have time requirements associated with them.
 - Several new formulae have been exposed for editing, including the entire BP calculation formula.
 - Rush builds 10% by spending 20% of the total vessel cost.
Interface Changes:
 - Moved buttons in the build list to before the vessel name
 - You can Alt+Click the arrow buttons to move a vessel to top/bottom of the list
 - Lines in storage are colored according to their current status
 - Vessels that are rolled out can be launched from the Editor (should save a scene change for KK users)
 - Crew Select GUI now displays Kerbal class and level
 - Crew Select GUI options now persist
 - Simulation time limit selection now persists per save. Defaults to infinite time if "free simulations" is active.
 - Added support for CrewQ mod (enneract)
Bug Fixes:
 - Added a popup that occurs when KCT doesn't load save data properly.
 - Fixed issues with editing a ship past VAB/launchpad limits.
 - Possibly fixed issues with costs being incorrectly calculated in some instances
 - Made the launch tooltip not get stuck when pressing the launch button (enneract)
 - Fixed several issues with upgrade point purchases/resetting.
 - Fix loading a save after a crash during a simulation.
 - Throttled editor recalculations to reduce lag, especially with procedural parts (enneract)
 - Fix for Real Fuels boiling off during construction
Miscellaneous:
 - "icons" folder renamed to "Icons"
 - Added min(x,y), max(x,y), sign(x), abs(x), l(x), and L(x) functions to math parser (l=natural log, L=log base 10)
 - Removed Herobrine

v1.1.2.0 (12/26/14)
 - Fixed issue when toolbar mod is used instead of Stock AppLauncher
 - Fixed issue when recovering parts that have no modules on them
 - Fixed rollback that's happening after launch when rolling out new vessel

v1.1.1.0 (12/23/14)
 - Fixed tech tree issues with TechManager trees
 - Fixed several issues with rollout and rollback, but added new ones it seems

v1.1.0.0 (12/22/14)
 - Update for KSP 0.90
 - Build List got a redesign
 - Added vessel rollout
 - Added support for RSS
 - Added basic support for Procedural Parts
 - Small change to file structure (added plugins folder, renamed .dll to remove underscores)
 - Fixed several bugs
 - Check the GitHub commit log for more. This took a long time.

v1.0.3.0 (10/17/14)
 - Update for KSP 0.25
 - Added ability to simulate at any time (currently requires the exact UT)
 - Made Build List viewable in the editor
 - Added confirmation dialog when scrapping vessels
 - Bug fix to ensure effectiveCosts can't be less than 0
 - Unified GUI appearance throughout scenes
 - Workaround for starting in air when launching from Tracking Station
 - Added basic Kerbal Alarm Clock support. If enabled, alarms will be automatically made for the next thing that is set to complete. Updates when the Build List is open
 - Fixed some bugs with tech nodes, some of which may have been present before 0.25
 - Halved reconditoning times by default (50 tons instead of 25) and made simulations much less expensive

v1.0.2.0 (9/3/14)
 - Hopefully fixed bug where Launch button stops being controlled by KCT with 64 bit KSP after vessel recovery
 - Fixed bug where SPH list estimates used the VAB rates
 - Added some framework for the open Betas. Forced debug messages on when built in Debug mode and changed the updater address to search for newer betas
 
v1.0.1.0 (8/31/14)
 - Made it so you don't have to be able to afford the vessel to simulate it.
 - Fixed issue where you couldn't simulate in orbit with build times disabled.
 - Made build list window not randomly appear when build times disabled.
 - Hopefully fixed issue that nerdextreme on reddit had where something weird was happening with the toolbar.
 
v1.0.0.0 (8/29/14)
 - RELEASE!!!!!!! AFTER 7 MONTHS!
 - TweakScale integration. Tweaked parts are tracked separately from the normal sized parts. Parts are listed as Name,Size (ie, Sepratron I,0.5)
 - Fixed issue with resource densities being incorrectly determined
 - Disabled debug messages by default (turn them on in the settings before posting a bug report)
 - Fixed issues with KJR and simulations causing explosions
 - Purchasing upgrades starts at 4 science or 16000 funds and doubles each time until 512 science or 1024000 funds, respectively.
 - Added a message whenever parts are scrapped with StageRecovery integration
 - Saved position and visibility of the Build List, Editor, and Time Left windows.
 - Made it so parts can be pulled from the inventory while editing.
 - Can purchase additional time at the ends of simulations at an increasing rate.
 - Overrode the Launch button so it's not disabled anymore.
 
PR7.2.3 (8/16/14)
 - Fix for calculating resource mass when the resource reference is null.

PR7.2.2 (8/16/14)
 - Fix for simulations thinking the vessel cost twice as much as it actually does.
 
PR7.2.1 (8/15/14)
 - Fixed a bug where runway items could trigger launchpad reconditioning.
 - Bugfix so that default crews would be assigned after recovering an existing flight on launch.
 
PR7.2 (8/15/14)
 - Fixed two bugs in recovery code. First one was incorrectly calculating Vt, the second was with negative refunds (technically another mods fault, probably TweakScale)
 - Added LaunchPad reconditioning. While it's active you can't launch.
   - Functions like ship times with a BP of (ship mass * ReconditioningEffect * OverallMultiplier). Default is 86400 BP per 25 tons (24 hours/25 tons at rate=1BP/s)
   - Rate is the sum of all VAB build rates
   - Only for LaunchPad, not for Runway
   - Can be disabled per game
 - Averted bug with trying to simulate on the verge of funds (you must be able to afford the ship and the simulation, not just the simulation)
 - The refunds given for launching (to balance KSP taking funds) is based on ship parts directly, not what it cost to build the ship originally (aka, price changes won't break anything)
 - Fixed a bug where you couldn't simulate for free if you couldn't afford the simulation
 - Changes to Crew Selection GUI: Can randomize filling, Auto-hire when filling, and default crew has returned (fills first crewable part)

PR7.1 (8/2/14)
 - Fixed several small bugs (lockup when disabling for a save, reverting to editor from simulation doesn't put ship into orbit next simulation, some issues with simulation time limit window)
 - Added ability to purchase upgrades for 250,000 funds
 - Simulations have been reworked
    - Orbits changed to use kilometers rather than meters
    - Costs funds dependent on how far the parent planet is from kerbin, whether the body is a moon, whether the body and/or it's parent planet has an atmosphere
    - Costs also affected by time limit of simulation
    - Costs can be disabled in settings
 - Moved several settings from Global to Game and added a menu for setting the default settings.
 - Updated to latest StageRecovery API. Parts can be damaged beyond repair if they land too hard (funds still recovered)
 - Added "Warp To" setting to all build lists and tech list. Pressing will warp time to that item's completion. If Stop Warp On Complete is active, then warp will still stop upon any item's completion.
  
PR7.0.5 (7/24/14)
 - Set compatibility checker to work with any version of 0.24 b/c of 0.24.1
 - Auto-updater set to not check for updates until after the user has gotten a chance to disable them.
 - Added .version file to support KSP-AVC auto-update checker
 
PR7.0.4 (7/23/14)
 - Editing of vessels is re-enabled.
 - Recovery messages or all messages can be disabled in the settings.
 - Build List and Editor windows retain their visibility within a game session (hide the editor window, press 'new' and it will stay hidden)
 - The current state of the auto-updater is included in the first run dialog.

PR7.0.3 (7/22/14)
 - Changed Tech Unlock time notification to use appropriate time units.
 - Added warning message to first run window about time pausing when hovering over Build List.
 - Added support for StageRecovery API. If SR is installed, KCT will let it handle recovery code and only receives the part list.
 
PR7.0.2 (7/20/14)
 - Fixed a bug with recovery code that would cause a failure if the same part was used multiple times on the recovered craft.
 - Added mouseover lock to BL+ window. General lock to launchpad clearing window.

PR7.0.1 (7/19/14)
 - Disabled recovery messages when DebRefund or StageRecovery are installed.
 - Made the events register even if KCT is disabled for a save. This way funds can still be recovered.
 - Altered update checker to allow checking for absolute latest version, or just the latest version for the specific KSP version.
 
PR7 (7/19/14)
 - Compatibility update for 0.24
 - Removed MCE support since it doesn't do funds anymore
 - Fixed issue with First Start window appearing when it isn't the first start
 - Dropped stages are "recovered" at 75% of what they normally would because of distance from KSC with parachutes. 100% of normal if a probe core is attached.
 - Updated to newest Real Chutes.
 - Added Locks when mousing over the editor window or the build list window. BL window lock prevents entering buildings but pauses game!
 - Basic support for the stock toolbar. The icon goes there and when ships complete or stages are recovered you are notified through the message system.
 - Integration with DebRefund. If it is installed, all funds recovery is handled by it. Otherwise, KCT's funds recovery is used.
 - Known Issues: Editing is currently broken, hovering over the build list pauses the game, you can't simulate a vessel that costs more than you have.

PR6b (7/3/14)
 - Can view/edit ships that are being built/have finished building
 - Automatic update check (specific to KSP version, so 0.23.5 right now. 0.24 updates won't be displayed). Default is based on user's progress tracking setting.
 - Possible fixes and workarounds for some bugs (specifically failure when saving data)
 - Default build rates reduced to 0.1BP/s from 1.0BP/s (18 upgrades worth, each). 15 upgrades have been given at start. Existing saves will have their spent upgrades reset and 15 more upgrades. (This does make initial build rates MUCH lower to start with! 36 upgrades are needed to reach 1.0 VAB and 1.0 SPH)
 - Estimated times for completion in the build list are now based on the highest build rate.
 - The build list, settings window, and part inventory now use a toolbar instead of a bunch of buttons. This looks a lot nicer, but is a bit bigger.
 - Parts taken from the inventory are now considered when recalculating build times after a ship finishes
 - When scrapping partially completed ships, some of the parts are added to the part inventory (representing some of the work being done/parts being ordered)
 - Added button to editor GUI to cycle through the available build rates (it's the asterisk "*")
 - Removed inability to switch vessels during simulation (it was easy to get around that limitation anyway, looking at you Map Mode!)
 - Added ability to add currently simulated vessel to build list (only one of them though, "Build it!" button)
 - Fixed several issues with the crew selection GUIs, stopped autohiring and setting a default crew (for now), added button to hire the next random Kerbal when no more are available.
 - Added a window that appears on new games (or existing games with this version)

PR6a (6/6/14)
 - Fixed bug with right-clicking windows causing them to vanish until game restart.
 - Can reset spent upgrades (costs 2 upgrades, non-permanent loss).
 - Small GUI change in simulations: Time Left window doesn't appear if time limit is infinity (0), removed the small window that appeared when clicking End Simulation (merged into main Simulation Window)
 - Build times can be disabled for a "simulation only" mode. Current ships/tech will continue progressing, new ones will be instant. Toolbar button opens the settings or simulation configuration window (or nothing) depending on scene.
 - Fixed bugs with starting a new game after loading an old one and with loading a game where the mod is disabled after loading one where it is enabled.
 - Getting the ship name is better now: doesn't require a save before building to get the correct name.
 - Can change the build rate used for time estimates in the editor (want to see how long it will take at a rate of 1, 1.5, 9001?). Defaults to highest rate.
 - Can opt out of using inventory parts for a build.
 - Temporarily removed the view button since it doesn't work yet anyway.

PR6 (5/20/14)
 - ForceStopWarp setting added (stops warp when ships finish, no matter how you start the warp)
 - Internal code changes to allow for multiple ships building at once
 - Added ability to purchase upgrades to build rates from the Space Center scene. 1 point is given per tech node unlocked, or 1 can be purchased for 250 science. Works with existing saves (sandbox may require you set the total points in the settings)
 - Small change made to how times are calculated with the part inventory. The BuildEffect is included as well, so times will continue to reduce (even after a part is used more times than the InventoryEffect)
 - Added a small window to the build list (access by pressing the "*") for advanced functions (scrap ship, view in editor (not implemented yet), rename, duplicate).
 - General improvements to the build list GUI
 - Added ability to rename ships that are building/in storage
 - The editor locks when the simulation configure window appears
 - Inclination can now be altered in simulation initial conditions
 - Science can now be earned for building ships (representing the hands on experience having some value). It is a separate rate in the R&D tab of the Upgrades window. Each point put in increases the rate by 0.5 science per 86400 build points the ship is worth (0.5 science per day at 1BP/s).
 - Fixed/changed how parts are tracked (For some reason I was tracking every single instance of a part on a ship, instead of only once per ship like I thought I was.) The part tracker won't increase as quickly now.
 - Avoided loading ships as much as possible, as I can now extract the part names from the config node directly! (no more/fewer floating ghost ships)
 - Fixed a bug with booster recovery code and crashed ships that caused the Tracking Station to go haywire
 - Tech Nodes now take time by default to unlock. The default rate is 1 day per 2 science cost of the node. This rate can be increased (in the R&D upgrades, under "Development") exponentially (1d/4s, 1d/8s, 1d/16s, etc))
 - Fixed a bug with launching from the Tracking Station not removing the ship from storage.
 - Fixed bug with Warp to Complete and low orbits (game freeze because it can't reach the highest warp rate)
 
PR5 (4/30/14)
 - Adding settings menu so you can change settings in-game (time settings/enable or disable for a save). Accessible through build list in Space Center.
 - Added MaxTimeWarp, EnableAllBodies config options
 - Can begin simulation in orbit around any body you have visited (needs SOI change or Active Vessel in SOI)
 - Fixed RealChute support for Real Chute v1.1+

PR4e (4/18/14)
 - Warp to Complete reworked some to fix issues with higher than stock timewarp (RSS)
 - Fixed bug that prevented the user from being able to exit the VAB/SPH after adding a ship to the build list and then pressing the New or Load buttons
 - Part inventory is sorted by category now.
 - Finer control of build list order using up/down arrows. Build List GUI needs a bit of work, so it doesn't look all that stellar.
 - Simulations now create a backup save which is loaded whenever the flight is finished. This fixes several issues with the current method, especially when the game crashes during a simulation.
 - Code is now on github!
 - Fixed booster recovery bug where parts weren't being recovered when they said they were.

PR4d (4/8/14)
 - Automatically displays in Kerbin time or 24 hour time depending on game settings
 - Enhanced build time controls. I will cover this in a forum post on p18 or p19
 - Implemented CompatibilityChecker. Only gives warning if used with incompatible version.
 - Implemented Mission Controller Extended compatibility. On revert, you are given your money back, 100%.

PR4c2 (4/1/14)
 - Fixed an issue with vessels not being removed from storage on launch.

PR4c (4/1/14)
 - Support for 0.23.5, the Asteroid Redirect Mission update
 - Removed time jump now that we can time warp in Space Center and Tracking Station
 - Fixed a bug with booster recovery when the boosters weren't being unloaded before being destroyed
 - Added the getting started guide to the download (hence the size increase to 2 MB)
 
PR4b (3/11/14)
 - RealChute support is now done using reflection and so only ONE version of the mod is needed. It is now "universal" for both stock and RealChute.

PR4a (3/9/14)
 - Fixed bug with removing ships from the build list
 - Enabled the Build List window in the tracking station. The default GUI parameters are weird there, so it looks pretty different.
 - Fixed bug with reverting to launch not giving you crew.
 - The toolbar is now optional, but still included and is highly recommended.

PR 4 (3/8/14)
 - Added a crew selection GUI that pops up when you try to launch a ship with crew capacity
 - Fixed a bug with recovering vessels in the tracking station
 - Removed ability to add Kerbals to your part inventory
 - The part inventory now displays the normal part name (as it is in the catalog) in the editor
 - Added optional support for RealChutes, but requires two separate versions now.
 - Added (and in fact, now require) Blizzy's Toolbar support. Icon credit goes to diomedea. The icon will flash when a vessel is completed!

PR 3c (optional update, 3/6/14)
 - Added config option to use 6 hour (Kerbin centric) days for Day display. Purely cosmetic and disabled by default. Use6HourDays = False 

PR 3b (2/25/14)
 - Fixed a NullReferenceError that would totally break things when a vessel was being destroyed but the ActiveVessel was null. (I had that happen to me, it messed stuff up pretty spectacularly)

PR 3a
 - Config option to disable automatically reverting when crashing in simulations. IF YOU USE THE EVIL GUI THAT KSP POPS UP I WON'T LOVE YOU. It's evil...
 - Fixed some of the GUIs having weird dimensions (they use the same Rect object, so when the height gets expanded the others look silly)

Pre-Release 3
 - THE BUILD LIST! Vessels are added to a build list (one for VAB, one for SPH) and built sequentially. The order can be changed with the TOP button and the vessel can be cancelled with the X button. Vessels are moved to storage after completion.
 - Vessel storage. One for VAB and one for SPH. Can launch craft or scrap them for their parts. Currently unlimited size, same as Build List, but will change later.
 - If SimulationTimeLimit is set to 0, then simulations will last indefinitely, but it may not let you revert depending on what you do (docking, still untested)

Pre-Release 2
 - Simulations are now based on game time. Currently set to 2 game hours. They don't auto-revert except in the case of crashes. Periapsis/apoapsis don't matter.
 - Boosters are automatically recovered if they have enough parachutes to drop the terminal velocity below 10 m/s. Check the Alt-F2 log to see if they get recovered on unload.
 - Possibly fixed issue with time warp getting stuck on when the total build time is only an hour or two (may still overshoot a bit though).

Pre-Release 1 - initial release.