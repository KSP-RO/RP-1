namespace RP0.ModuleTags
{
    public class ModuleTagAvionics : ModuleTag
    {
        public override string GetInfo()
        {
            return "Contains avionics to control the craft which requires extensive testing which increase the overall launch cost.\n\n" +
                   "<b><color=orange>Launch Cost: Cost of This Part * 3</color></b>";
        }
    }
}
