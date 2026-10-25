using KSP.Localization;
using RP0.UI;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace RP0.Programs
{
    public class ProgramFundingOverview : MonoBehaviour
    {
        private const int ChartWidth = 500;
        private const int ChartHeight = 330;

        private LineChart _chart;
        private Program _program;

        internal void Awake()
        {
            var le = gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = ChartWidth;
            le.preferredHeight = ChartHeight;

            _chart = gameObject.AddComponent<LineChart>();
            _chart.XAxisTitle = Localizer.Format("#rp0_Admin_FundingChart_XAxis");
            _chart.YAxisTitle = Localizer.Format("#rp0_Admin_FundingChart_YAxis");
            _chart.MinXTickStep = 1;

            gameObject.SetActive(false);
        }

        public void SetupProgram(Program program)
        {
            _program = program;
            if (program == null)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);

            var shownYears = Math.Ceiling(_program.DurationYears * 1.33);
            var points = (int)Math.Round(shownYears * 24);
            var xValues = new double[points];
            var yValues = new double[points];

            const double secPerYear = 365.25d * 86400d;
            var xStep = shownYears / points;
            double totalPaid = 0;
            for (int i = 0; i < points; i++)
            {
                var x = i * xStep;
                double fundAtNextX = _program.GetFundsAtTime((x + xStep) * secPerYear);
                double paidThisPeriod = fundAtNextX - totalPaid;
                totalPaid = fundAtNextX;

                xValues[i] = x;
                yValues[i] = paidThisPeriod / (xStep * 12);    // Scale from step length to one month
            }

            _chart.Clear();
            _chart.SetXRange(0, shownYears);
            _chart.AddSeries(null, xValues, yValues, XKCDColors.BrightCyan);
            if (_program.IsActive)
                _chart.AddMarker(Localizer.Format("#rp0_Admin_FundingChart_Now"), _program.FracElapsed * _program.DurationYears, Color.green);
            _chart.AddMarker(Localizer.Format("#rp0_Admin_FundingChart_Deadline"), _program.DurationYears, Color.red);
            _chart.Rebuild();
        }
    }
}
