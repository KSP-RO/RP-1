namespace RP0.Tooling
{
    public class Parameter
    {
        public Parameter(string title, string unit)
        {
            this.Title = title;
            this.Unit = unit;
        }

        public string Title { get; }
        public string Unit { get; }
    }
}