namespace RP0
{
    class ModuleAvionicsModifier : PartModule
    {
        [KSPField]
        public float multiplier = 1f;
        public override string GetInfo() => $"This part contributes {multiplier:P} of its mass to avionics requirements.";
    }
}
