using System;
using System.Linq.Expressions;

namespace RP0.Programs
{
    public static class PredicateBuilder
    {
        public static Expression<Func<bool>> Or(this Expression<Func<bool>> expr1,
                                                     Expression<Func<bool>> expr2)
        {
            return Expression.Lambda<Func<bool>>(Expression.OrElse(expr1.Body, expr2.Body));
        }

        public static Expression<Func<bool>> And(this Expression<Func<bool>> expr1,
                                                      Expression<Func<bool>> expr2)
        {
            return Expression.Lambda<Func<bool>>(Expression.AndAlso(expr1.Body, expr2.Body));
        }

        public static Expression Replace(this Expression expression,
            Expression searchEx, Expression replaceEx)
        {
            return new ReplaceVisitor(searchEx, replaceEx).Visit(expression);
        }

        private class ReplaceVisitor : ExpressionVisitor
        {
            private readonly Expression _from, _to;

            public ReplaceVisitor(Expression from, Expression to)
            {
                _from = from;
                _to = to;
            }
            public override Expression Visit(Expression node)
            {
                return node == _from ? _to : base.Visit(node);
            }
        }
    }
}
