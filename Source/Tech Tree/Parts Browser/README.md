# RP-0-Parts-Browser
This is a browser/editor application for the RP-0 parts list (converted to json files per mod)  that can also generate the needed configs from it, created by [@mattwrobel](https://github.com/mattwrobel)

To get it working:
   1. It uses Python 3, so that needs to be installed.
   2. It uses flask, so:  pip install flask
   3. Flask uses slugify (I think) so:  pip install slugify
   4. Then you should be able to hit:  python app.py
   5. Point your browser at:  http://localhost:5000/dashboard
   
# RP-0 Needs

For RP-0, every part used in the game must be configured in a few ways to be used in the career mode game.
   1.  Need to define where it goes in the tech tree as well as an entry cost and a part cost.
   2.  Need to declare any parts from any mods that represent the same part. (Identical Parts)
   3.  Need to declare any entry cost modifiers (ECMs) that affect the entry cost of linked parts (for example, unlocking the LR-79 drastically reduces the entry cost of unlocking the LR-89)
   4.  Need to link engines to their engine configs.
   5.  Need to generate engine configs.

In the past the files that govern these configs (TREE-Parts.cfg, TREE-Engines.cfg, ECM-Parts.cfg, ECM-Engines.cfg, and identicalParts.cfg) were generated from a google spreadsheet with formulas and whatnot.  There were several issues with this, so after consulting with Pap and TidalStream I came up with the idea of instead building this browser/editor/config generator that would instead operate on JSON files which could be tracked and source controlled by git instead of relying on the google sheet.

# What the browser does

## Converted from sheet to JSON files per mod
  
1. I originally read all the data in from the PartSheet.csv (generated from Pap's database) in /original_sheet_data.
2. Then I converted it to JSON files per mod, which are now in the /data directory.
   
## Created a Part Browser/Editor that you can run locally as a webapp using Python 3
  
### Browser
    
1.  The Part Browser allows you to view all of the currently known parts in RP-0. Some of these parts have been configured, but by no means all of them.  Some are partially configured.  Some are 'orphaned' which is kind of a Work in Progress flag.
2. The browser allows for filtering by any of the columns.
3. Easy way to visualize and inspect the currently known parts.
    
### Editor
    
1.  The far right column contains an edit button that pops up a form for that part to be edited.
2.  The button at the bottom 'queue changes' will add any changes you make to a 'currently queued changes' list that shows up at the bottom of th page.
3.  When changes have been made, there will be a button at the bottom that says 'commit changes'.  This button will actively make your changes in the JSON files in the /data directory, making them 'permanent'.  It's then up to you to create a pull request to have them changes in the git repository.
      
### Config Generation
   
1.  The button at the top of the page will take the current data in the app (including any queued changes that have been committed), and regenerate the config files into the /output folder.
   
