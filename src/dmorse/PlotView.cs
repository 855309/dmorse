using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace dmorse
{
    public partial class PlotView : Form
    {
        ScottPlot.FormsPlot fplot; 
        public PlotView()
        {
            InitializeComponent();


        }

        public void AddData(double[] data, string title)
        {
            TabPage t = new TabPage();
            t.Text = title;

            plotTabControl.Controls.Add(t);

            fplot = new ScottPlot.FormsPlot();
            fplot.Dock = DockStyle.Fill;

            t.Controls.Add(fplot);

            fplot.Plot.AddSignal(data);
            fplot.Plot.YLabel(title);
            fplot.Plot.Margins(0);
            fplot.Refresh();

            //this.Text = title;

            //fplot.Plot.SaveFig("signal.png");
        }
    }
}
