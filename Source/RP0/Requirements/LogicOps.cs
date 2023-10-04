using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using UniLinq;
using UnityEngine;

namespace RP0.Requirements
{
    public abstract class LogicOp
    {
        public abstract override string ToString();

        public abstract Expression<Func<bool>> CombineExpressions(IEnumerable<Expression<Func<bool>>> expressions);

        public static LogicOp Parse(ConfigNode cn)
        {
            string s = cn.name;
            if (s.Equals("any", StringComparison.OrdinalIgnoreCase))
            {
                return new Any();
            }
            else if (s.Equals("atleast", StringComparison.OrdinalIgnoreCase))
            {
                return new AtLeast(cn);
            }
            else
            {
                return new All();
            }
        }
    }
    public class All : LogicOp
    {
        public override string ToString() => "All";

        public override Expression<Func<bool>> CombineExpressions(IEnumerable<Expression<Func<bool>>> expressions)
        {
            if (expressions == null || expressions.Count() == 0)
            {
                return () => true;
            }
            return expressions.Aggregate((a, b) => a.And(b));
        }
    }

    public class Any : LogicOp
    {
        public override string ToString() => "Any";

        public override Expression<Func<bool>> CombineExpressions(IEnumerable<Expression<Func<bool>>> expressions)
        {
            if (expressions == null || expressions.Count() == 0)
            {
                return () => true;
            }
            return expressions.Aggregate((a, b) => a.Or(b));
        }
    }

    public class AtLeast : LogicOp
    {
        public uint Count { get; private set; }

        public AtLeast(uint count)
        {
            Count = count;
        }

        public AtLeast(ConfigNode cn)
        {
            uint count = 0;
            if (!cn.TryGetValue("count", ref count))
            {
                RP0Debug.LogError("Invalid RequirementBlock logic operator: " + cn);
            }
            Count = count;
        }

        public override string ToString() => $"At least {Count}";

        public override Expression<Func<bool>> CombineExpressions(IEnumerable<Expression<Func<bool>>> expressions)
        {
            if (expressions == null || expressions.Count() == 0)
            {
                return () => true;
            }
            Func<bool>[] compiledExprs = expressions.Select(x => x.Compile()).ToArray();
            return () => compiledExprs.Count(expr => expr.Invoke()) >= Count;
        }
    }
}
