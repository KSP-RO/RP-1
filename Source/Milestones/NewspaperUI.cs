using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using KSP.UI.TooltipTypes;
using RP0.UI;

namespace RP0.Milestones
{
    [KSPAddon(KSPAddon.Startup.AllGameScenes, true)]
    public class NewspaperUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public static GameObject NewspaperCanvas = null;
        public static GameObject NewspaperName = null;
        public static GameObject NewspaperFlag = null;
        public static GameObject NewspaperDate = null;
        public static GameObject NewspaperHeadline = null;
        public static GameObject NewspaperArticle = null;
        public static GameObject NewspaperImage = null;
        public static GameObject NewspaperButton = null;
        public static GameObject NewspaperButtonText = null;
        public static Text newspaperTitle;
        public static Text milestoneDate;
        public static Text headlineText;
        public static Text articleText;
        public static Image playerFlag;
        public static Image milestoneImage;
        private static Vector2 dragStart;
        private static Vector2 altStart;
        private static float windowWidth = 0f;
        private static float windowHeight = 0f;

        private static readonly DateTime epoch = new DateTime(1951, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static void ShowGUI(Milestone m)
        {
            // Load the UI and show it
            NewspaperCanvas = (GameObject)Instantiate(NewspaperLoader.PanelPrefab);

            // Parent it to the KSP Main Canvas
            NewspaperCanvas.transform.SetParent(MainCanvasUtil.MainCanvas.transform);
            NewspaperCanvas.AddComponent<NewspaperUI>();

            // Get the size of the panel and center it on the screen
            windowWidth = NewspaperCanvas.GetComponent<RectTransform>().sizeDelta.x;
            windowHeight = NewspaperCanvas.GetComponent<RectTransform>().sizeDelta.y;
            Vector3 currentPos = NewspaperCanvas.transform.position;
            Vector3 windowPos = new Vector3(currentPos.x - windowWidth / 2, currentPos.y + windowHeight / 2, 0f);
            NewspaperCanvas.transform.position = windowPos;

            // Find the game objects by what they are named in Unity
            NewspaperName = (GameObject)GameObject.Find("NewspaperName");
            NewspaperFlag = (GameObject)GameObject.Find("ProgramFlag");
            NewspaperDate = (GameObject)GameObject.Find("DateText");
            NewspaperHeadline = (GameObject)GameObject.Find("HeadlineText");
            NewspaperArticle = (GameObject)GameObject.Find("ArticleText");
            NewspaperImage = (GameObject)GameObject.Find("NewsImage");
            NewspaperButton = (GameObject)GameObject.Find("NewsButton");
            NewspaperButtonText = (GameObject)GameObject.Find("NewsButtonText");

            // Add a callback for the button action
            Button button = NewspaperButton.GetComponent<Button>();
            button.onClick.AddListener(delegate { OnButtonPressed(); });

            // Add tooltip to the button
            var tooltip = NewspaperButton.AddComponent<TooltipController_TextFunc>();
            var prefab = AssetBase.GetPrefab<Tooltip_Text>("Tooltip_Text");
            tooltip.prefab = prefab;
            tooltip.getStringAction = GetTooltipTextButton;
            tooltip.continuousUpdate = true;

            // Get the relevant parts that can be changed via config text
            newspaperTitle = NewspaperName.GetComponent<Text>();
            milestoneDate = NewspaperDate.GetComponent<Text>();
            headlineText = NewspaperHeadline.GetComponent<Text>();
            articleText = NewspaperArticle.GetComponent<Text>();
            playerFlag = NewspaperFlag.GetComponent<Image>();
            milestoneImage = NewspaperImage.GetComponent<Image>();

            // Set the variable text and data based on the completed contract
            newspaperTitle.text = GetNewspaperTitle();
            milestoneDate.text = GetMilestoneDate(m);
            playerFlag.sprite = GetPlayerFlag();
            headlineText.text = m.headline;
            articleText.text = m.article;
            milestoneImage.sprite = GetMilestoneImage(m);
    }

        private static string GetTooltipTextButton()
        {
            // TO-DO
            return "Tooltip";
        }

        static void OnButtonPressed()
        {
            if (NewspaperCanvas != null)
            {
                Destroy();
            }
        }

        public static void Destroy()
        {
            NewspaperCanvas.DestroyGameObject();
            NewspaperCanvas = null;
        }

        private static string GetNewspaperTitle()
        {
            string title = "Space Gazette";
            return title;
        }

        private static string GetMilestoneDate(Milestone m)
        {
            DateTime curDate = epoch.AddSeconds(m.date);
            return curDate.ToString("D");
        }

        private static Sprite GetMilestoneImage(Milestone m)
        {
            Texture2D tex = GameDatabase.Instance.GetTexture(HighLogic.CurrentGame.flagURL, asNormalMap: false);
            Sprite flagSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            return flagSprite;
        }

        private static Sprite GetPlayerFlag()
        {
            Texture2D tex = GameDatabase.Instance.GetTexture(HighLogic.CurrentGame.flagURL, asNormalMap: false);
            Sprite flagSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            return flagSprite;
        }

        // This event fires when a drag event begins
        public void OnBeginDrag(PointerEventData data)
        {
            dragStart = new Vector2(data.position.x - Screen.width / 2, data.position.y - Screen.height / 2);
            altStart = NewspaperCanvas.transform.position;
        }

        // This event fires while we're dragging. It's constantly moving the UI to a new position
        public void OnDrag(PointerEventData data)
        {
            Vector2 dpos = new Vector2(data.position.x - Screen.width / 2, data.position.y - Screen.height / 2);
            Vector2 dragdist = dpos - dragStart;
            NewspaperCanvas.transform.position = altStart + dragdist;
        }

        // This event fires when we let go of the mouse and stop dragging
        public void OnEndDrag(PointerEventData data)
        {
            // TO-DO: Add memory of where it was moved to
        }
    }
}
