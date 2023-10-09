using KSP.UI.Screens;

namespace RP0
{
    public class GUI_DevPartsButton : GUI_TopRightButton
    {
        private static readonly EditorPartListFilter<AvailablePart> _expPartsFilter = new EditorPartListFilter<AvailablePart>("experimentalPartsFilter",
            (p => !ResearchAndDevelopment.IsExperimentalPart(p)));

        public GUI_DevPartsButton(int offset) : base(offset, StateMode.Toggle)
        {
        }

        public override void Init()
        {
            base.Init();

            if (EditorPartList.Instance != null && !IsOn)
            {
                EditorPartList.Instance.ExcludeFilters.AddFilter(_expPartsFilter);
                KCTUtilities.RemoveResearchedPartsFromExperimental();
            }
        }

        protected override void OnClick()
        {
            SpaceCenterManagement.Instance.ExperimentalPartsEnabled = IsOn;

            if (IsOn)
            {
                EditorPartList.Instance.ExcludeFilters.RemoveFilter(_expPartsFilter);
                KCTUtilities.AddResearchedPartsToExperimental();
            }
            else
            {
                EditorPartList.Instance.ExcludeFilters.AddFilter(_expPartsFilter);
                KCTUtilities.RemoveResearchedPartsFromExperimental();
            }

            EditorPartList.Instance.Refresh();
        }
    }
}
