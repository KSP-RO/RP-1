using Strategies;

namespace RP0.Leaders
{
    /// <summary>
    /// Abstract base for simple multiplier-based leader effects
    /// </summary>
    public abstract class BaseEffect : StrategyEffect
    {
        /// <summary>
        /// The base effect description, combined with effect multiplier and the actual thing the effect touches (in child class)
        /// </summary>
        [Persistent]
        protected string effectDescription = string.Empty;

        /// <summary>
        /// Ordinarily the loc string is generated from the effect multiplier and the effect data.
        /// If this is specified, it is used as-is.
        /// </summary>
        [Persistent]
        protected string locStringOverride = string.Empty;

        [Persistent]
        protected string effectTitle = string.Empty;

        [Persistent]
        protected double multiplier = 1d;

        /// <summary>
        /// Ordinarily multipliers > 1 are positive and <1 are negative effects. This will flip the coloration.
        /// </summary>
        [Persistent]
        protected bool flipPositive = false;

        protected const string _GoodColor = "#caff00";
        protected const string _BadColor = "#feb200";

        public BaseEffect(Strategy parent)
            : base(parent)
        {
        }

        protected virtual bool IsPositive => (multiplier > 1d) ^ flipPositive;

        protected abstract string DescriptionString();

        public override string GetDescription()
        {
            string retStr = $"<color={(IsPositive ? _GoodColor : _BadColor)}>{DescriptionString()}</color>";

            if (Parent is StrategyRP0 sr && sr.ShowExtendedInfo && !string.IsNullOrEmpty(effectTitle))
                retStr = effectTitle + "\n  " + retStr;

            return retStr;
        }

        public override void OnLoadFromConfig(ConfigNode node)
        {
            ConfigNode.LoadObjectFromConfig(this, node);
        }
    }
}
