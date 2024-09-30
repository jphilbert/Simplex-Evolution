using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Optimization;
using ZedGraph;

namespace Graphing
{

  public partial class FormSimEvol : Form
  {
    public void EvalReq(object sender, EventArgs e)
    {
      //Console.WriteLine("I recieved an event to evaluate");
    }

    public void Fin(object sender, EventArgs e)
    {
      fin = true;
    }

    public bool fin = false;

    /// <summary>
    /// dummy simulator, simply evaluates a list of parameter sets
    /// </summary>
    /// <param name="valueParemeters">list of VPs</param>
    public void JohnsRunSimulator(List<Simplex.ValueParameter> valueParemeters)
    {
      for (int i = 0; i < valueParemeters.Count; i++)
        valueParemeters[i].Value = griewank(valueParemeters[i].Parameters);
    }


    /// <summary>
    /// griewank function (may have slight modifications)
    /// </summary>
    /// <param name="z">variables</param>
    /// <returns>value</returns>
    double griewank(double[] z)
    {
      int n = z.Length;
      double term1 = 0;
      double term2 = 1;

      for (int i = 0; i < n; i++)
        {
          term1 += (z[i] - griewankOffset) * (z[i] - griewankOffset);
          term2 *= Math.Cos((z[i] - griewankOffset) / Math.Sqrt((double)(i + 1)));
        }

      term1 /= (double)(griewankSlope * n);

      return (term1 - term2) + 1;
    }


    SimplexGenetics myGenetics;

    double griewankOffset = 0;
    int seed = 0;
    int genInc = 1;
    double griewankSlope = 400;        

    /// <summary>
    /// Constructor
    /// </summary>
    public FormSimEvol()
    {
      InitializeComponent();

      initializeGraph();

      double[] UpperBounds
        = new double[] { 10000, 10000, 10000, 10000, 10000, 10000, 
        , 10000, 10000, 10000, 10000, 10000, 
        , 10000, 10000, 10000, 10000, 10000, 
        , 10000, 10000, 10000, 10000, 10000 };
      double[] LowerBounds
        = new double[] { -10000, -10000, -10000, -10000, -10000, -10000, 
        -10000, -10000, -10000, -10000, -10000, -10000, 
        -10000, -10000, -10000, -10000, -10000, -10000, 
        -10000, -10000, -10000, -10000, -10000, -10000 };

      myGenetics = new SimplexGenetics(seed, 16, UpperBounds, LowerBounds);

      myGenetics.GeneticLog.LogPriority = "100";
      myGenetics.GeneticLog.ConsoleOutput = false;
      myGenetics.Colony.EvaluationChunkSize = 16;     // set processing chunk size (default is 1)
      myGenetics.Colony.LazyWorkers = true;           // change LazyWorkers (default is true)

      myGenetics.Finished += Fin;                     // thrown if finished

    }

    /// <summary>
    /// Initialize the Graph Pane
    /// </summary>
    public void initializeGraph()
    {
      GraphPane myPane = zg1.GraphPane;

      // Set the titles and axis labels
      myPane.Title.Text = "Simplex Evolution";
      myPane.XAxis.Title.Text = "Evals Per Simplex";
      myPane.YAxis.Title.Text = "Log Value";

      // Semilog plot
      myPane.XAxis.Type = AxisType.Linear;
      myPane.YAxis.Type = AxisType.Log;

      // Make as pretty as possible
      myPane.Legend.IsVisible = false;
      myPane.Border.IsVisible = false;
      myPane.Fill = new Fill(this.BackColor);

      myPane.CurveList.Clear();
      zg1.AxisChange();
      zg1.Show();
    }

    /// <summary>
    /// Create / Update Graph
    /// </summary>
    /// <param name="zgc">graph control</param>
    public void CreateGraph(ZedGraphControl zgc)
    {
      GraphPane myPane = zgc.GraphPane;

      // Set the titles (adds the generation to the title)
      myPane.Title.Text = "Simplex Evolution " + (myGenetics.Generation - 1).ToString();

      // if there are no curves made set them now
      if (myPane.CurveList.Count == 0)
        {
          Random rnd = new Random();
          List<PointPairList> PPlist = new List<PointPairList>();

          foreach (List<double> bestlist in myGenetics.Colony.BestList)
            {
              PointPairList plist = new PointPairList();
              for (int x = 0; x <= bestlist.Count - 1; x++)
                plist.Add((double)x, bestlist[x]);

              PPlist.Add(plist);
              bestlist.Clear();
            }

          for (int i = 0; i < PPlist.Count; i++)
            myPane.AddCurve(i.ToString(), PPlist[i],
                            Color.FromArgb(rnd.Next(255), rnd.Next(255), rnd.Next(255)), SymbolType.None);
        }

      // curves are made, add new points.
      else
        {
          double lastX = myPane.CurveList[0].Points[myPane.CurveList[0].NPts - 1].X;
          for (int i = 0; i < myPane.CurveList.Count; i++)
            {
              for (int pt = 0; pt < myGenetics.Colony.BestList[i].Count; pt++)
                myPane.CurveList[i].AddPoint(lastX + pt + 1, myGenetics.Colony.BestList[i][pt]);

              myGenetics.Colony.BestList[i].Clear();
            }                
        }


      // Calculate the Axis Scale Ranges
      zgc.AxisChange();
      zgc.Show();
    }

    /// <summary>
    /// Resize Event
    /// </summary>
    private void FormSimEvol_Resize(object sender, EventArgs e)
    {
      SetSize();
    }

    /// <summary>
    /// Sets the size of graph
    /// </summary>
    private void SetSize()
    {
      zg1.Location = new Point(10, 10);
      // Leave a small margin around the outside of the control
      zg1.Size = new Size(this.ClientRectangle.Width - 20, this.ClientRectangle.Height - 20);
    }

    /// <summary>
    /// Load form
    /// </summary>
    private void FormSimEvol_Load(object sender, EventArgs e)
    {
      //CreateGraph(zg1);
      SetSize();
    }

    /// <summary>
    /// Main algorithm run
    /// </summary>
    private void buttonRun_Click(object sender, EventArgs e)
    {
      fin = false;
      int gen = myGenetics.Generation;
      while (!fin)
        {
          myGenetics.Colony.Run();    // runs algorithm

          // plot results of each simplex after each generation
          if (gen != myGenetics.Generation)
            {
              gen = myGenetics.Generation;
              CreateGraph(zg1);
              this.Refresh();
            }

          // evaluate a chunk
          JohnsRunSimulator(myGenetics.Colony.EvaluationChunk);
        }

      CreateGraph(zg1);
      label_Best.Text = myGenetics.BestEvaluation.ToString();
      this.Refresh();

      myGenetics.MaxGenerations += genInc;        // Increase max to allow Run_Click again
    }

    /// <summary>
    /// Opens Setting dialog
    /// </summary>
    private void buttonSettings_Click(object sender, EventArgs e)
    {
      Settings settings = new Settings();
      settings.Genetics = myGenetics;         // pass the genetic class
      settings.seed = seed;                   // pass this instance seed

      // pass Griewank modifications
      settings.minLocation = griewankOffset;
      settings.slope = griewankSlope;

      // pass generation inc
      settings.extendGenerations = genInc;

      settings.ShowDialog();

      // pass back and initialize
      myGenetics = settings.Genetics;
      myGenetics.Finished += Fin;
      myGenetics.GeneticLog.LogPriority = "100";
      myGenetics.GeneticLog.ConsoleOutput = false;
      myGenetics.Colony.EvaluationChunkSize = 16;     // set processing chunk size (default is 1)
      myGenetics.Colony.LazyWorkers = true;           // change LazyWorkers (default is true)


      seed = settings.seed;
      griewankOffset = settings.minLocation;
      griewankSlope = settings.slope;
      genInc = settings.extendGenerations;

      // if setting require graph to be reset, do it
      if (settings.requestGraphReset)            
        initializeGraph();
    }

    /// <summary>
    /// Clear Graph
    /// </summary>
    private void buttonClear_Click(object sender, EventArgs e)
    {
      GraphPane myPane = zg1.GraphPane;

      // Set the titles and axis labels

      myPane.CurveList.Clear();

      myPane.XAxis.Type = AxisType.Linear;
      myPane.YAxis.Type = AxisType.Log;

      myPane.Legend.IsVisible = false;

      // Fill the axis background with a color gradient
      myPane.Chart.Fill = new Fill(Color.White, Color.White, 45F);

      zg1.AxisChange();
      zg1.Show();

      this.Refresh();

    }

    /// <summary>
    /// Show best parameter dialog
    /// </summary>
    private void buttonParams_Click(object sender, EventArgs e)
    {
      Best best = new Best(myGenetics.BestParameters);
      best.Show();
    }
  }
}

