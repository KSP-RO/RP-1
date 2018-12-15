# RP-0-Parts-Browser
This is a browser/editor application for the RP-0 parts list (converted to json files per mod)  that can also generate the needed configs from it.

To get it working:
   1. It uses Python 3, so that needs to be installed.
   2. It uses flask, so:  pip install flask
   3. Flask uses slugify (I think) so:  pip install slugify
   4. Then you should be able to hit:  python app.py
   5. Point your browser at:  http://localhost:5000/dashboard
