[![Build Status](https://travis-ci.org/KSP-RO/RP-0.svg?branch=master)](https://travis-ci.org/KSP-RO/RP-0)

Welcome to Realistic Progression Zero, the lightweight career addon for Kerbal Space Program's Realism Overhaul.

RP-0 is a career mode for RealismOverhaul with minimal install requirements, and with fair and balanced gameplay. Our aim is to allow players to enjoy RealismOverhaul in career mode, without installing a huge number of modules on top of those required by RealismOverhaul itself. However we also wish to ensure that RP-0 works with as many additional mods as possible; we use the Community Tech Tree for the basis of career progression, and try to place as many parts from other mods as possible in a balanced fashion without them being requirements.

RP-0 is a community effort, and your contributions are appreciated. You can report issues [on our issues page](https://github.com/KSP-RO/RP-0), and access the [source code on github](https://github.com/KSP-RO/RP-0). The [tech tree](https://github.com/KSP-RO/RP-0/blob/master/tree.yml) itself is designed to be particularly easy for humans to edit.

When starting the game, the balance should be similar to KSP's normal career, so we recommend "Moderate" or "Hard" settings. If playing without part unlock costs, it's recommended you drop contract funds payouts to 20% or less to maintain balance, since in real life the research programs and the setting up of factories cost much more than serial production.

This top-post is jointly maintained by pjf and NathanKell.

---

License: CC-BY-NC-SA-4.0

Development thread: http://forum.kerbalspaceprogram.com/threads/103196
Github Repo:  https://github.com/KSP-RO/RP-0

We recommend installing via the CKAN, `ckan.exe install RP-0` on the command line, or just look for "Realistic Progression Zero" in the GUI.

**Requirements:**
- Realism Overhaul and all its required mods
- Community Tech Tree
- Lack's SXT (although only the engines have RO configs as of yet)
- Ven's Stock Parts Revamp
- Contract Configurator
- Custom Barn Kit
- Deadly Reentry (RO only recommends it. We require it.)

**Recommended:**
- Procedural Fairings
- Procedural Parts
- B9 Procedural Wings
- Kerbal Construction Time
- Taerobee

**Supported**
- Raidernick's US Probes
- FASA
- AIES
- Soviet engines
- B9 Aerospace
- Habitat Pack
- Space Shuttle by Mike NZ
- Space Shuttle by Dragon1
- SCAN SAT
- Infernal Robotics
- KAS
- KIS
- Bahamuto Adjustable Landing Gear
- Bahamuto mk2 Lightning Cockpit
- Raidernick's Soviet rockets
- Raidernick's US Probes
- Raidernick's 7K-LOK
- Raidernick's Soyuz, Salyut
- Raidernick's Skylab
- TAC Life Support
- Starshine Merlin engines
- Remote Tech
- NearFuture Solar panels

**In Progress**
- DMagic's Orbital Science
- Universal Storage
- Raidernick Soviet probes and rockets
- LazTek SpaceX 
- Chaka Monkey 
- KW Rocketry
- KAX 
- Spice Launch System 
- Constellation Essentials 
- FusTek
- Nova Punch
- Tantares
- Cryogenic Engines

Note that more effort has gone into balancing earlier nodes than later nodes. Your feedback and assistance in balancing all nodes is appreciated!


Procedural Avionics
=========

Procedural Avionics is an addition to RP-0 which allows you to build custom avionics units, instead of relying on pre-build avionics (think Procedural Parts, except for avionics).  In fact, Procedural Avionics was built on top of Procedural Parts, we just took the Procedural Battery that RP-0 had and added a scalable avionics unit inside of it.

### Tonnage
In RP-0, avionics units have a maximum amount of mass they can control.  When using a procedural avionics part, you'll notice it had a tonnage slider.  Sliding this slider increases the amount of mass this part can control.  The maximum amount of mass it can control is limited by two things:

 - Part Efficiency
 - Part Volume
 - Configuration (more on this later)

Efficiency is limited by tech level.  Volume is limited by part size and shape.  Tonnage can further be limited by setting the appropriate values in the configuration.

>In the GUI, you'll see a Percent Utilization field in the editor that ranges from 0% - 200%.  It's not always the best idea to just max out the avionics slider, more on that later.

### Configuration
Procedural Avionics can have different configurations.  For instance, booster avionics might not be toggleable, use a lot of power, but be fairly light (for the amount of mass they can control), while probeCore avionics would be more efficient power wise, but a bit heavier (due to their robustness).  Upper stages might lie somewhere in the middle.

### Optimal Utilization Percentages

As part utilization goes up, so do part cost and part weight.  However, these do not rise at the same rates.  Consider this example: as you try to fit more avionics controls into the same amount of space, you should probably invest some effort into making those controls smaller and more efficient.  The smaller and more efficient you try to make those parts, the more they cost (and the higher the cost per controllable ton).  However, since these parts are more efficient, while your overall part mass continues to rise, your mass per controllable ton actually decreases.  Because of this, we actually get two different "curves", a mass curve, and a cost curve:

 - mass curve: -(x^2) + 2x  (as utilization goes up, additional mass needed per controllable mass goes down)
 - cost curve: x^2  (as utilization goes up, cost rises exponentially)
>Note that while utilization is normally displayed as a range between 0-200%, the above formulas treat utilization as 0-100%

Because the actual efficiency of a part depends on its utilization, we run into a problem when trying to configure these parts.  Thus, we'll define a "sweet spot" of 100% utilization.  If a part is 100% utilized, then their cost/controllable ton and mass/controllable ton will be what's in the configs.  Bringing the utilization up to 200% (and reduce the volume accordingly, so you're still controlling the same amount of mass), and you should find your part mass decreased by about 1/3, but your cost to be 4x as much.

>It is worth noting that the mass and cost curves here only define the mass of the avionics module portion of the part.  As these parts by default also contain batteries, there is additional mass and cost dependant on the size of the part.

### Electric charge consumption

Electric charge consumption is not nearly as complicated, as it scales linearly with utilization.  At 0% utilization (I don't know why you would ever choose that), power consumption is 50% of its rated (sweet spot) value.  100% utilization consumes 100% of rated value, and 200% utilization consumes 150%.  This is true for both the active and inactive power drain rates.

### Tech Node Config
Let's take a look at a sample MM config for procedural avionics.  I'll annotate what things mean inline.


    MODULE
    {
        name = ModuleProceduralAvionics

        AVIONICSCONFIG  # A Procedural Avionics unit can have 
                        # multiple AVIONICSCONFIG nodes
        {
            name = booster  # The name of this config

            TECHLIMIT  # Guidance Unit (Starting), 1m
                       # We'll define our efficiencies per tech level 
                       # Sandbox play will pick the best TECHLIMIT per AVIONICSCONFIG
                       # We generally try to add a comment saying what this confic
                       # was created from (in this case, the first Guidance Unit)
            {
                name = start                # This is the name of the technology that
                                            # should be unlocked for this to be
                                            # available.
                                            
                unlockCost = 0              # This node is free

                tonnageToMassRatio = 26.13  # This is how efficient this node is mass 
                                            # wise at 100% utilization. A higher number 
                                            # here is more efficient. More than 100%
                                            # utilization will cause this to be
                                            # effectively higher too.
                                            
                costPerControlledTon = 13   # This is how much the avionics portion of 
                                            # this part costs at 100% utilization.
                                            # Lower is better. This goes up exponentially
                                            # as utilization goes up. 
                                            
                enabledProceduralW = 150    # This is the power drain per controllable 
                                            # ton when this part is active, 
                                            # again at 100% utilization.
                                            
                standardAvionicsDensity = 5 # This helps make procedural avionics
                                            # match up to their in-game counterparts.
                                            # It's pretty much part mass / part volume.
                                            #
                                            # Another note about this value: generally, 
                                            # it has no effect on performance efficiency,
                                            # since this just ties the weight of avionics
                                            # to the size of the part. In the end, 
                                            # avionics is still dependant on part mass.

                minimumTonnage = 15         # No matter what values you have set elsewhere,
                                            # this is the minimum amout of control you can
                                            # set this part to.
                                            # This prevents you from using ultra-small booster
                                            # units as probe cores.

                maximumTonnage = 40         # No matter what values you have set elsewhere,
                                            # this is the maximum amout of control you can
                                            # set this part to.
                                            # This prevents you from using unrealistic 
                                            # sized avioncs units early on.

                SASServiceLevel = 0         # The service level of the built in SAS Module.
                                            # All procedural avionics units have SAS modules,
                                            # this value can range from 0-3
            }
            TECHLIMIT # Guidance Unit (Early), 1m
                      # An example of another, more efficient 
                      # tech node for the same config
            {
                name = earlyAvionics
                unlockCost = 9000           # This node costs 9000 funds
                tonnageToMassRatio = 104.16
                costPerControlledTon = 3.8
                enabledProceduralW = 6.666
                standardAvionicsDensity = 2.7504
                minimumTonnage = 15
                maximumTonnage = 90         # Notice minimumTonnage stayed the same,
                                            # while we've increased maximumTonnage
                SASServiceLevel = 0
            }
            TECHLIMIT # Guidance Unit (Early), 2m, 3m average
            {
                name = basicAvionics
                unlockCost = 15000          # This node costs 15000 funds
                                            # However, if you've purchased the previous node(s),
                                            # it would only cost 6000 funds

                tonnageToMassRatio = 940
                costPerControlledTon = .53
                enabledProceduralW = 5.81
                standardAvionicsDensity = 0.083
                minimumTonnage = 15
                maximumTonnage = 600
                SASServiceLevel = 1
            }
        }
        AVIONICSCONFIG # A second config
        {
            name = upperStage

            TECHLIMIT # Able Avionics Package
            {
                name = earlyAvionics             # Notice there is no node with the name 
                                                 # of "start".  This will cause the 
                                                 # upperStage config not to be
                                                 # unlocked until later in the gameplay

                unlockCost = 6000
                tonnageToMassRatio = 52.8169
                costPerControlledTon = 4.8
                enabledProceduralW = 30
                standardAvionicsDensity = 0.2279
                minimumTonnage = 3
                maximumTonnage = 10
                SASServiceLevel = 0
            }
            TECHLIMIT # Agena Avionics Package, with some fudged stats
            {
                name = stability
                unlockCost = 24000
                tonnageToMassRatio = 108
                costPerControlledTon = 23
                enabledProceduralW = 15.625
                disabledProceduralW = 0.625    # Here we have a new node.  This controls
                                               # power drain when the part is disabled.
                                               # this worked the same way as
                                               # enabledProceduralW
                standardAvionicsDensity = 0.07
                minimumTonnage = 3
                maximumTonnage = 10
                SASServiceLevel = 3
            }
        }
        AVIONICSCONFIG
        {
            name = probeCore

            TECHLIMIT # Ranger Block I Core
            {
                name = basicScience
                unlockCost = 36000
                tonnageToMassRatio = 4.327
                costPerControlledTon = 4425
                enabledProceduralW = 166.66
                disabledProceduralW = 2.666
                standardAvionicsDensity = 0.416
                SASServiceLevel = 1

                # Since probeCores should be the least mass efficient, we probably don't
                # need to add any restrictions on min/max tonnage
            }
            TECHLIMIT # Ranger Block III Core
            {
                name = miniaturization
                unlockCost = 45000
                tonnageToMassRatio = 8
                costPerControlledTon = 2550
                enabledProceduralW = 133.33
                disabledProceduralW = 2.5
                standardAvionicsDensity = 0.28
                SASServiceLevel = 1
                hasScienceContainer = true      # This enables the ModularScienceContainer
            }
        }
    }

### Building your own TECHLIMIT nodes
I have created a spreadsheet [here](https://docs.google.com/spreadsheets/d/1lpuszH_nUbWlTXZzfOMzsy-uhUNh016eNu0WL8x6qvU/edit#gid=1101913381) that you can copy and use as a scratchpad to help you configure tech nodes.  I'll give a manual walkthrough of what I would expect someone to do here, and I have created a few nodes at the top of the spreadsheet for some early existing parts.

Feel free to update the spreadsheet and create PRs to add new nodes.  An example PR can be found [here](https://github.com/KSP-RO/RP-0/pull/626).

 - Create a new line, copying the line above it to get all the formulas.
     - Note: You should only need to edit the values in the blue columns.
 - Take a look at the parts mass, cost, and controllable mass.  Put those in columns and D, E, and F respectively.
 - Columns G and H should be what the avionics unit drain is (in kW)
 - Create a part roughly the same size and shape of an existing avionics module unlocked in that tech node.
     - Take note of its volume.  Enter this in column G of the spreadsheet. (Note: the KSP UI normally shows liters, but in the spreadsheets it's kiloliters.)
     - Slide the utilization slider down to 0, and take note of its empty weight and empty cost.  Put those in columns H and I respectively.
 - Columns L-P should now contain values for `tonnageToMassRatio`, `costPerControlledTon`, `standardAvionicsDensity`, `enabledProceduralW` and  `disabledProceduralW`
 - For setting `minimumTonnage` and `maximumTonnage`, I generally halve and double the values defined for the existing avionics part.
 - For setting `SASServiceLevel`, I just use the value already present in the `ModuleSAS` already on this part.
 - For setting `unlockCost`, I usually just use 1.5x the unlock price of the non-procedural version

> There are many cases where you have more than one avionics unit per tech node.  This may be because they should be in two different configs.  If there is more that one for the same config, I would suggest averaging their values.

> It's also worth noting that the maximum size of a Procedural Part can be setup by using ProcPart TECHNODES in the `ProceduralPart` MODULE, however we have cleared those out.

