using KSP.UI.Screens;

namespace KerbalConstructionTime
{
    public class GUI_DevPartsButton : GUI_TopRightButton
    {
        private static readonly EditorPartListFilter<AvailablePart> _expPartsFilter = new EditorPartListFilter<AvailablePart>("experimentalPartsFilter",
            (p => !ResearchAndDevelopment.IsExperimentalPart(p)));

        public GUI_DevPartsButton(int offset) : base(offset, GUI_TopRightButton.StateMode.Toggle)
        {
        }

        public override void Init()
        {
            base.Init();

            if (EditorPartList.Instance != null && !IsOn)
            {
                EditorPartList.Instance.ExcludeFilters.AddFilter(_expPartsFilter);
                Utilities.RemoveResearchedPartsFromExperimental();
            }
        }

        protected override void OnClick()
        {
            KerbalConstructionTimeData.Instance.ExperimentalPartsEnabled = IsOn;

            if (IsOn)
            {
                EditorPartList.Instance.ExcludeFilters.RemoveFilter(_expPartsFilter);
                Utilities.AddResearchedPartsToExperimental();
            }
            else
            {
                EditorPartList.Instance.ExcludeFilters.AddFilter(_expPartsFilter);
                Utilities.RemoveResearchedPartsFromExperimental();
            }

            EditorPartList.Instance.Refresh();
        }
    }
}
