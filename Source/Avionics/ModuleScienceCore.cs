namespace RP0
{
    class ModuleScienceCore : PartModule
    {
        [KSPField]
        public bool allowAxial = false;

        public override string GetInfo() => "This part alone only allows limited command interaction; you will not be allowed full attitude control unless another command part without this module is attached.";
    }
}
