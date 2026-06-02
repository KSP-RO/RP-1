using System;
using UnityEngine;

namespace RP0
{
    // Port of the alexmoon / Transfer Window Planner Lambert solver (Sun, F.T., "On the Minimum Time
    // Trajectory and Multiple Solutions of Lambert's Problem", AAS 79-164, 1979). Used to orient the
    // hyperbolic-approach sim so its arrival v∞ direction matches TWP's: stock TransferMath's solvers
    // gave a v∞ direction ~1.5° off the value TWP and a real flown trajectory produce.
    //
    // Validated against Curtis "Orbital Mechanics for Engineering Students" Example 5.2.
    public static class LambertSolver
    {
        private const double MachineEpsilon = 2.2204460492503131e-16;
        private const double TwoPi = 2.0 * Math.PI;
        private const double HalfPi = 0.5 * Math.PI;

        private struct Range
        {
            public double lower, upper;
            public Range(double lower, double upper) { this.lower = lower; this.upper = upper; }
        }

        // Solve Lambert's problem. pos1/pos2 are positions relative to the central body; longWay selects
        // the >180° transfer arc. Returns the velocity at pos1 (departure); outputs velocity at pos2 (arrival).
        public static Vector3d Solve(double gravParameter, Vector3d pos1, Vector3d pos2, double timeOfFlight, bool longWay, out Vector3d v2)
        {
            double r1 = pos1.magnitude;
            double r2 = pos2.magnitude;

            Vector3d deltaPos = pos2 - pos1;
            double c = deltaPos.magnitude;
            double m = r1 + r2 + c;
            double n = r1 + r2 - c;

            double angleParameter = Math.Sqrt(n / m);
            if (longWay) angleParameter = -angleParameter;

            double normalizedTime = 4.0 * timeOfFlight * Math.Sqrt(gravParameter / (m * m * m));
            double parabolicNormalizedTime = 2.0 / 3.0 * (1.0 - angleParameter * angleParameter * angleParameter);

            ComputePathParameters(angleParameter, normalizedTime, parabolicNormalizedTime, out double x, out double y);

            double sqrtMu = Math.Sqrt(gravParameter);
            double invSqrtM = 1.0 / Math.Sqrt(m);
            double invSqrtN = 1.0 / Math.Sqrt(n);

            double vc = sqrtMu * (y * invSqrtN + x * invSqrtM);
            double vr = sqrtMu * (y * invSqrtN - x * invSqrtM);
            Vector3d ec = deltaPos * (vc / c);

            v2 = ec - pos2 * (vr / r2);
            return ec + pos1 * (vr / r1);
        }

        // Solve the (x, y) path parameters for the transfer depending on the normalized time-of-flight:
        // parabolic, hyperbolic, or elliptical (minimum-energy / high path / low path).
        private static void ComputePathParameters(double angleParameter, double normalizedTime, double parabolicNormalizedTime, out double x, out double y)
        {
            Func<double, double> fy = (xn) => (angleParameter < 0)
                ? -Math.Sqrt(1.0 - angleParameter * angleParameter * (1.0 - xn * xn))
                : Math.Sqrt(1.0 - angleParameter * angleParameter * (1.0 - xn * xn));

            if (RelativeError(normalizedTime, parabolicNormalizedTime) < 1e-6) // Parabolic
            {
                x = 1.0;
                y = (angleParameter < 0) ? -1 : 1;
                return;
            }

            if (normalizedTime < parabolicNormalizedTime) // Hyperbolic
            {
                Func<double, double> fdt = (xn) =>
                {
                    double yn = fy(xn);
                    double g = Math.Sqrt(xn * xn - 1.0);
                    double h = Math.Sqrt(yn * yn - 1.0);
                    return (Acoth(yn / h) - Acoth(xn / g) + xn * g - yn * h) / (g * g * g) - normalizedTime;
                };

                Range bounds = new Range(1.0 + MachineEpsilon, 2.0);
                while (fdt(bounds.upper) > 0.0)
                {
                    bounds.lower = bounds.upper;
                    bounds.upper *= 2.0;
                }

                x = FindRoot(bounds, 1e-6, fdt);
                y = fy(x);
                return;
            }

            // Elliptical
            double minimumEnergyNormalizedTime = Math.Acos(angleParameter) + angleParameter * Math.Sqrt(1 - angleParameter * angleParameter);
            if (RelativeError(normalizedTime, minimumEnergyNormalizedTime) < 1e-6) // Minimum-energy ellipse
            {
                x = 0.0;
                y = fy(x);
                return;
            }

            Func<double, double> fdtElliptic = (xn) =>
            {
                double yn = fy(xn);
                double g = Math.Sqrt(1.0 - xn * xn);
                double h = Math.Sqrt(1.0 - yn * yn);
                return (Acot(xn / g) - Math.Atan(h / yn) - xn * g + yn * h) / (g * g * g) - normalizedTime;
            };

            Range ellipticBounds = normalizedTime > minimumEnergyNormalizedTime
                ? new Range(-1.0 + MachineEpsilon, 0.0)  // High path
                : new Range(0.0, 1.0 - MachineEpsilon);  // Low path

            x = FindRoot(ellipticBounds, 1e-6, fdtElliptic);
            y = fy(x);
        }

        // Brent's root-finding method. Returns NaN if it fails to converge in the iteration cap (the
        // caller treats a NaN result as "couldn't solve" and falls back).
        private static double FindRoot(Range bounds, double tolerance, Func<double, double> f)
        {
            double a = bounds.lower;
            double b = bounds.upper;
            double c = a;
            double fa = f(a);
            double fb = f(b);
            double fc = fa;
            double d = b - a;
            double e = d;

            tolerance *= 0.5;

            for (int i = 0; i <= 100; i++)
            {
                if (Math.Abs(fc) < Math.Abs(fb)) // c closer to root than b: swap
                {
                    a = b; b = c; c = a;
                    fa = fb; fb = fc; fc = fa;
                }

                double tol = 2.0 * MachineEpsilon * Math.Abs(b) + tolerance;
                double mm = 0.5 * (c - b);

                // |fb| <= double.Epsilon is the exact-root test (fb is essentially zero).
                if (Math.Abs(fb) <= double.Epsilon || Math.Abs(mm) <= tol)
                    return b;

                if (Math.Abs(e) < tol || Math.Abs(fa) <= Math.Abs(fb)) // Bisection step
                {
                    d = e = mm;
                }
                else
                {
                    double step = InterpolationStep(a, b, c, fa, fb, fc, mm, tol, e);
                    if (double.IsNaN(step)) // Interpolation rejected -> bisection
                    {
                        d = e = mm;
                    }
                    else
                    {
                        e = d;
                        d = step;
                    }
                }

                a = b;
                fa = fb;

                if (Math.Abs(d) > tol)
                    b += d;
                else
                    b += (mm > 0 ? tol : -tol);

                fb = f(b);

                if ((fb < 0 && fc < 0) || (fb > 0 && fc > 0)) // Keep fb, fc opposite-signed
                {
                    c = a;
                    fc = fa;
                    d = e = b - a;
                }
            }

            return double.NaN;
        }

        // Brent interpolation candidate step: inverse-quadratic when three distinct points are available,
        // otherwise linear (secant). Returns NaN when the step should be rejected in favour of bisection.
        private static double InterpolationStep(double a, double b, double c, double fa, double fb, double fc, double mm, double tol, double e)
        {
            double s = fb / fa;
            double p, q;

            // |a - c| <= double.Epsilon means a and c are the same point (only two distinct abscissae).
            if (Math.Abs(a - c) <= double.Epsilon) // Linear interpolation
            {
                p = 2.0 * mm * s;
                q = 1.0 - s;
            }
            else // Inverse quadratic interpolation
            {
                double qq = fa / fc;
                double r = fb / fc;
                p = s * (2.0 * mm * qq * (qq - r) - (b - a) * (r - 1.0));
                q = (qq - 1.0) * (r - 1.0) * (s - 1.0);
            }

            if (p > 0.0) q = -q;
            else p = -p;

            if (2.0 * p < Math.Min(3.0 * mm * q - Math.Abs(tol * q), Math.Abs(e * q))) // Accept
                return p / q;

            return double.NaN; // Reject
        }

        private static double Acot(double x) => HalfPi - Math.Atan(x);
        private static double Acoth(double x) => 0.5 * Math.Log((x + 1) / (x - 1));
        private static double RelativeError(double a, double b) => Math.Abs(1.0 - a / b);
    }
}
