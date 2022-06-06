using Strategies;

namespace RP0.Programs
{
    public class VirtualStrategy : Strategy
    {
        new public virtual bool Activate() { return base.Activate(); }
        new public virtual bool CanBeActivated(out string reason) { return base.CanBeActivated(out reason); }
        new public virtual bool CanBeDeactivated(out string reason) { return base.CanBeDeactivated(out reason); }
        new public virtual bool Deactivate() { return base.Deactivate(); }
        new public virtual void Load(ConfigNode node) { base.Load(node); }
        new public virtual void Register() { base.Register(); }
        new public virtual void Save(ConfigNode node) { base.Save(node); }
        new public virtual void Unregister() { base.Unregister(); }
        new public virtual void Update() { base.Update(); }
    }
}
