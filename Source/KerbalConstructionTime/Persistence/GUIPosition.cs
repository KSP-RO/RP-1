using System;
using System.IO;
using UnityEngine;

namespace KerbalConstructionTime
{
    public class GUIPosition
    {
        [Persistent] public string guiName;
        [Persistent] public float xPos, yPos;
        [Persistent] public bool visible;

        public GUIPosition() { }

        public GUIPosition(string name, float x, float y, bool vis)
        {
            guiName = name;
            xPos = x;
            yPos = y;
            visible = vis;
        }
    }

    public class GUIDataSaver
    {
        protected string filePath = KSPUtil.ApplicationRootPath + "GameData/RP-0/PluginData/KCT_Windows.txt";
        [Persistent] GUIPosition editorPositionSaved, buildListPositionSaved, editorBuildListPositionSaved;

        public void Save()
        {
            try
            {
                buildListPositionSaved = new GUIPosition("buildList", KCT_GUI.BuildListWindowPosition.x, KCT_GUI.BuildListWindowPosition.y, KCTGameStates.ShowWindows[0]);
                editorPositionSaved = new GUIPosition("editor", KCT_GUI.EditorWindowPosition.x, KCT_GUI.EditorWindowPosition.y, KCTGameStates.ShowWindows[1]);
                editorBuildListPositionSaved = new GUIPosition("editorBuildList", KCT_GUI.EditorBuildListWindowPosition.x, KCT_GUI.EditorBuildListWindowPosition.y, false);

                ConfigNode cnTemp = ConfigNode.CreateConfigFromObject(this, new ConfigNode());
                cnTemp.Save(filePath);
            }
            catch(Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        public void Load()
        {
            if (!File.Exists(filePath))
                return;

            try 
            {
                ConfigNode cnToLoad = ConfigNode.Load(filePath);
                ConfigNode.LoadObjectFromConfig(this, cnToLoad);

                if (buildListPositionSaved != null)
                {
                    KCT_GUI.BuildListWindowPosition.x = buildListPositionSaved.xPos;
                    KCT_GUI.BuildListWindowPosition.y = buildListPositionSaved.yPos;
                    KCTGameStates.ShowWindows[0] = buildListPositionSaved.visible;
                }

                if (buildListPositionSaved != null)
                {
                    KCT_GUI.EditorWindowPosition.x = editorPositionSaved.xPos;
                    KCT_GUI.EditorWindowPosition.y = editorPositionSaved.yPos;
                    KCTGameStates.ShowWindows[1] = editorPositionSaved.visible;
                }

                if (editorBuildListPositionSaved != null)
                {
                    KCT_GUI.EditorBuildListWindowPosition.x = editorBuildListPositionSaved.xPos;
                    KCT_GUI.EditorBuildListWindowPosition.y = editorBuildListPositionSaved.yPos;
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
    }
}

/*
    KerbalConstructionTime (c) by Michael Marvin, Zachary Eck

    KerbalConstructionTime is licensed under a
    Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International License.

    You should have received a copy of the license along with this
    work. If not, see <http://creativecommons.org/licenses/by-nc-sa/4.0/>.
*/
