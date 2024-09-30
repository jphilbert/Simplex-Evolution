using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace Graphing
{
  public partial class Best : Form
  {
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="p">parameters to show in dialog</param>
    public Best(double[] p)
    {
      InitializeComponent();

      if (p == null)
        tb_BestParams.Text = "No Best, Run First";
      else
        {
          tb_BestParams.Text = p[0].ToString("0.000000");
          for (int i = 1; i < p.Length; i++)
            tb_BestParams.Text += Environment.NewLine +
              p[i].ToString("0.000000");                    
        }

      tb_BestParams.Select(0, 0);
    }

  }
}

