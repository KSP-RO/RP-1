using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UniLinq;
using System.Collections.Generic;
using KSP.Localization;
using System.IO;
using ROUtils;

namespace RP0.Milestones
{
    [KSPAddon(KSPAddon.Startup.SpaceCentre, false)]
    public class NewspaperUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public static GameObject NewspaperCanvas = null;
        private static Vector2 dragStart;
        private static Vector2 altStart;

        private static RP0Settings _settings;

        public static bool IsOpen => NewspaperCanvas != null;

        public static void ShowGUI(Milestone m, List<string> data)
        {
            _settings = HighLogic.CurrentGame.Parameters.CustomParams<RP0Settings>();
            // Load the UI and show it
            NewspaperCanvas = (GameObject)Instantiate(NewspaperLoader.PanelPrefab);

            // Parent it to the KSP Main Canvas
            NewspaperCanvas.transform.SetParent(MainCanvasUtil.MainCanvas.transform);
            NewspaperCanvas.AddComponent<NewspaperUI>();

            // Get the size of the panel and center it on the screen
            float windowWidth = NewspaperCanvas.GetComponent<RectTransform>().sizeDelta.x;
            float windowHeight = NewspaperCanvas.GetComponent<RectTransform>().sizeDelta.y;
            Vector3 currentPos = NewspaperCanvas.transform.position;
            Vector3 windowPos = new Vector3(currentPos.x - windowWidth / 2, currentPos.y + windowHeight / 2, 0f);
            NewspaperCanvas.transform.position = windowPos;

            // Add a callback for the button action
            Button button = NewspaperCanvas.transform.FindDeepChild("NewsButton").GetComponent<Button>();
            button.onClick.AddListener(OnButtonPressed);

            // Add tooltip to the button
            //var tooltip = button.gameObject.AddComponent<TooltipController_TextFunc>();
            //var prefab = AssetBase.GetPrefab<Tooltip_Text>("Tooltip_Text");
            //tooltip.prefab = prefab;
            //tooltip.getStringAction = GetTooltipTextButton;
            //tooltip.continuousUpdate = true;

            // Get the relevant parts that can be changed via config text
            var newspaperTitle = NewspaperCanvas.transform.FindDeepChild("NewspaperName").GetComponent<Text>();
            var milestoneDate = NewspaperCanvas.transform.FindDeepChild("DateText").GetComponent<Text>();
            var headlineText = NewspaperCanvas.transform.FindDeepChild("HeadlineText").GetComponent<Text>();
            var articleText = NewspaperCanvas.transform.FindDeepChild("ArticleText").GetComponent<Text>();
            var playerFlag = NewspaperCanvas.transform.FindDeepChild("ProgramFlag").GetComponent<Image>();
            var milestoneImage = NewspaperCanvas.transform.FindDeepChild("NewsImage").GetComponent<Image>();
            //var newsButtonText = NewspaperCanvas.transform.FindDeepChild("NewsButtonText").GetComponent<Text>();


            // Set the variable text and data based on the completed contract
            newspaperTitle.text = GetTitle();
            milestoneDate.text = GetDate(m, data);
            playerFlag.sprite = GetPlayerFlag();
            headlineText.text = FillText(m.headline, data);
            articleText.text = FillText(m.article, data);
            milestoneImage.sprite = GetImage(m);
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
                MilestoneHandler.Instance?.TryCreateNewspaper();
            }
        }

        public static void Destroy()
        {
            NewspaperCanvas.DestroyGameObject();
            NewspaperCanvas = null;
        }

        private static string GetTitle()
        {
            string title = _settings.NewspaperTitle;
            return title;
        }

        private static string GetDate(Milestone m, List<string> data)
        {
            if (data != null && data.Count > 0)
            {
                return data.Pop();
            }

            double contractDate = double.MaxValue, programDate = double.MaxValue ;
            if(!string.IsNullOrEmpty(m.contractName))
                contractDate = Contracts.ContractSystem.Instance.ContractsFinished.FirstOrDefault(c => c is ContractConfigurator.ConfiguredContract cc && cc.contractType.name == m.contractName)?.DateFinished ?? contractDate;
            if (!string.IsNullOrEmpty(m.programName))
                programDate = Programs.ProgramHandler.Instance.CompletedPrograms.FirstOrDefault(p => p.name == m.programName)?.completedUT ?? programDate;

            double date = Math.Min(contractDate, programDate);
            return KSPUtil.PrintDate(date, false);
        }

        private static string FillText(string template, List<string> data)
        {
            if (data == null || data.Count == 0)
                return template;

            return Localizer.Format(template, data.ToArray());
        }

        private static Sprite GetImage(Milestone m)
        {
            Texture2D tex = null;
            string filePath = $"{KSPUtil.ApplicationRootPath}/saves/{HighLogic.SaveFolder}/{m.name}.png";
            if (File.Exists(filePath) && !_settings.UseLastScreenshot)
            {
                tex = new Texture2D(2, 2);
                tex.LoadImage(File.ReadAllBytes(filePath));
            }
            if (_settings.UseLastScreenshot)
            {
                var directory = new DirectoryInfo($"{KSPUtil.ApplicationRootPath}/Screenshots/");
                tex = new Texture2D(2, 2);
                var latestFile = directory.GetFiles("*.png").OrderByDescending(f => f.LastWriteTime).First().FullName;
                tex.LoadImage(File.ReadAllBytes(latestFile));
            }
            if (tex == null)
                tex = GameDatabase.Instance.GetTexture(m.image, asNormalMap: false);
            Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            return sprite;
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
