
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Optimization;

namespace Graphing
{
  public partial class Settings : Form
  {
    /// <summary>
    /// this form's SimplexGenetic instance
    /// </summary>
    public SimplexGenetics Genetics;

    /// <summary>
    /// Changes require graph reset
    /// </summary>
    public bool requestGraphReset = false;

    /// <summary>
    /// Location of true minimum
    /// </summary>
    public double minLocation = 0;

    /// <summary>
    /// Slope of function
    /// </summary>
    public double slope = 400;

    /// <summary>
    /// random seed
    /// </summary>
    public int seed = 0;

    /// <summary>
    /// Generation increment per run click
    /// </summary>
    public int extendGenerations = 1;


    public Settings()
    {
      InitializeComponent();
    }

#region Validation

    /// <summary>
    /// Double Validation
    /// </summary>
    private void double_Validated(object sender, System.EventArgs e)
    {
      TextBox tb = (TextBox)sender;

      double x = 0;
      if (double.TryParse(tb.Text, out x))
        {
          errorProvider.SetError(tb, "");


        }
      else
        errorProvider.SetError(tb, "must be a valid double number");
    }

    /// <summary>
    /// Double within [0, 1] Validation
    /// </summary>
    private void doubleBetween01_Validated(object sender, System.EventArgs e)
    {
      TextBox tb = (TextBox)sender;

      double x = 0;
      if (double.TryParse(tb.Text, out x) && x >= 0 && x <= 1)
        {
          errorProvider.SetError(tb, "");


        }
      else
        errorProvider.SetError(tb, "must be a double between 0 and 1");
    }

    /// <summary>
    /// Double &gt; 1 Validation
    /// </summary>
    private void doubleGreaterThan1_Validated(object sender, System.EventArgs e)
    {
      TextBox tb = (TextBox)sender;

      double x = 0;
      if (double.TryParse(tb.Text, out x) && x >= 1)
        errorProvider.SetError(tb, "");
      else
        errorProvider.SetError(tb, "must be a double greater than 1");
    }

    /// <summary>
    /// Positive Int Validation
    /// </summary>
    private void intPositive_Validated(object sender, System.EventArgs e)
    {
      TextBox tb = (TextBox)sender;

      int x = 0;
      if (int.TryParse(tb.Text, out x) && x >= 0)
        errorProvider.SetError(tb, "");
      else
        errorProvider.SetError(tb, "must be a positive integer");
    }

    /// <summary>
    /// Positive Non Zero Int Validation
    /// </summary>
    private void intNonZero_Validated(object sender, System.EventArgs e)
    {
      TextBox tb = (TextBox)sender;

      int x = 0;
      if (int.TryParse(tb.Text, out x) && x > 0)
        errorProvider.SetError(tb, "");
      else
        errorProvider.SetError(tb, "must be an integer greater than 0");
    }

    /// <summary>
    /// Int &gt; 1 Validation
    /// </summary>
    private void intGreaterThan1_Validated(object sender, System.EventArgs e)
    {
      TextBox tb = (TextBox)sender;

      int x = 0;
      if (int.TryParse(tb.Text, out x) && x >= 2)
        errorProvider.SetError(tb, "");
      else
        errorProvider.SetError(tb, "must be an integer greater than 1");
    }

    /// <summary>
    /// Number of Evaluations Validation
    /// </summary>
    private void eval_Validated(object sender, System.EventArgs e)
    {
      int x = 0;
      int y = 0;

      if (int.TryParse(tbEvals.Text, out x) && int.TryParse(tbDimensions.Text, out y) && x > y)
        errorProvider.SetError(tbEvals, "");
      else
        errorProvider.SetError(tbEvals, "must be an integer greater dimensions");
    }

    /// <summary>
    /// Upper and Lower Bounds Validation
    /// </summary>
    private void bounds_Validated(object sender, System.EventArgs e)
    {
      double u = 0;
      double l = 0;

      if (double.TryParse(tbUpperBounds.Text, out u) && double.TryParse(tbLowerBounds.Text, out l) && u > l)
        {
          errorProvider.SetError(tbUpperBounds, "");
          errorProvider.SetError(tbLowerBounds, "");
        }
      else
        {
          errorProvider.SetError(tbUpperBounds, "Upper bounds must be greater than lower bounds");
          errorProvider.SetError(tbLowerBounds, "Lower bounds must be less than upper bounds");
        }
    }

#endregion

    /// <summary>
    /// Loads all current settings
    /// </summary>
    private void Settings_Load(object sender, EventArgs e)
    {
      tbPopulation.Text = Genetics.Colony.Workers.Count.ToString();
      tbUpperBounds.Text = Genetics.Colony.UpperBounds[0].ToString();
      tbLowerBounds.Text = Genetics.Colony.LowerBounds[0].ToString();
      tbDimensions.Text = Genetics.Colony.UpperBounds.Length.ToString();
      // the above need genetics reinitialize

      tbGenerations.Text = Genetics.MaxGenerations.ToString();
      tbEvals.Text = Genetics.Colony.MaxEvaluations.ToString();
      tbShrink.Text = Genetics.Colony.ShrinkFactor.ToString();
      tbGrow.Text = Genetics.Colony.GrowFactor.ToString();

      switch (Genetics.Fitness)
        {
        case SimplexGenetics.FitnessType.Min:
          rbMin.Checked = true;
          break;
        case SimplexGenetics.FitnessType.Max:
          rbMax.Checked = true;
          break;
        case SimplexGenetics.FitnessType.Average:
          rbAverage.Checked = true;
          break;
        default:
          break;
        }

      cbForceBounds.Checked = Genetics.Colony.ForceBoundaryConditions;

      switch (Genetics.Colony.BoundaryCondition)
        {
        case Simplex.BoundaryConditionsType.Random:
          rbBRandom.Checked = true;
          break;
        case Simplex.BoundaryConditionsType.Sticky:
          rbBSticky.Checked = true;
          break;
        case Simplex.BoundaryConditionsType.Periodic:
          rbBPeriodic.Checked = true;
          break;
        case Simplex.BoundaryConditionsType.Reflective:
          rbBReflective.Checked = true;
          break;
        default:
          break;
        }

      tbShrinkGen.Text = Genetics.ShrinkBoundaryPerGeneration.ToString();
      tbBoundShrink.Text = Genetics.ShrinkBoundaryFactor.ToString();
      cbResetOnShrink.Checked = Genetics.ResetOnShrink;

      switch (Genetics.Shrink)
        {
        case SimplexGenetics.ShrinkType.ShrinkAround:
          rbShrinkBest.Checked = true;
          break;
        case SimplexGenetics.ShrinkType.ChangeLowerIfNeg:
          rbOffsetToBest.Checked = true;
          break;
        default:
          break;
        }

      switch (Genetics.Marriage)
        {
        case SimplexGenetics.MarriageType.KingHenry:
          rbMKingHenry.Checked = true;
          break;
        case SimplexGenetics.MarriageType.Random:
          rbMRandom.Checked = true;
          break;
        case SimplexGenetics.MarriageType.RandomPreferable:
          rbMRandomPreferable.Checked = true;
          break;
        case SimplexGenetics.MarriageType.Hierarchical:
          rbMHierarchical.Checked = true;
          break;
        case SimplexGenetics.MarriageType.BestWorst:
          rbMBestWorst.Checked = true;
          break;
        default:
          break;
        }

      tbReproductionAmount.Text = Genetics.ReproductionPercent.ToString();
      switch (Genetics.Reproduction)
        {
        case SimplexGenetics.ReproductionType.DiscreteMixing:
          rbRDiscrete.Checked = true;
          break;
        case SimplexGenetics.ReproductionType.LinearCombination:
          rbRLinear.Checked = true;
          break;
        case SimplexGenetics.ReproductionType.RandomType:
          rbRRandom.Checked = true;
          break;
        default:
          break;
        }

      tbSeed.Text = seed.ToString();
      tbExtendGen.Text = extendGenerations.ToString();
      tbMinLoc.Text = minLocation.ToString();
      tbSlope.Text = slope.ToString();

    }

    /// <summary>
    /// Set / Validate Button
    /// </summary>
    private void buttonSet_Click(object sender, EventArgs e)
    {
      if (!isError())
        {
          set();
          this.Close();
        }

    }

    /// <summary>
    /// Checks all controls for errors
    /// </summary>
    /// <returns>true if error exists</returns>
    private bool isError()
    {
      foreach (Control C in this.Controls)
        foreach (Control c in C.Controls)
        {
          string errorCode = errorProvider.GetError(c);                    
          if (errorCode != "")
            {   errorProvider.SetError(c, "");
              errorProvider.SetError(c, errorCode);
              return true;
            }
        }
      return false;
    }

    /// <summary>
    /// Set parameters
    /// </summary>
    private void set()
    {
      if (tbPopulation.Text != Genetics.Colony.Workers.Count.ToString() ||
          tbUpperBounds.Text != Genetics.Colony.UpperBounds[0].ToString() ||
          tbLowerBounds.Text != Genetics.Colony.LowerBounds[0].ToString() ||
          tbDimensions.Text != Genetics.Colony.UpperBounds.Length.ToString() ||
          tbSeed.Text != seed.ToString())
        {
          int pop = int.Parse(tbPopulation.Text);
          double uB = double.Parse(tbUpperBounds.Text);
          double lB = double.Parse(tbLowerBounds.Text);
          int dim = int.Parse(tbDimensions.Text);

          double[] uBArray = new double[dim];
          double[] lBArray = new double[dim];

          for (int i = 0; i < dim; i++)
            {
              uBArray[i] = uB;
              lBArray[i] = lB;
            }

          seed = int.Parse(tbSeed.Text);

          Genetics = new SimplexGenetics(seed, pop, uBArray, lBArray);
          requestGraphReset = true;
        }

      minLocation = double.Parse(tbMinLoc.Text);
      extendGenerations = int.Parse(tbExtendGen.Text);
      slope = double.Parse(tbSlope.Text);


      Genetics.MaxGenerations = int.Parse(tbGenerations.Text);
      Genetics.Colony.MaxEvaluations = int.Parse(tbEvals.Text);
      Genetics.Colony.ShrinkFactor = double.Parse(tbShrink.Text);
      Genetics.Colony.GrowFactor = double.Parse(tbGrow.Text);



      if (rbMin.Checked)
        Genetics.Fitness = SimplexGenetics.FitnessType.Min;
      if (rbMax.Checked)
        Genetics.Fitness = SimplexGenetics.FitnessType.Max;
      if (rbAverage.Checked)
        Genetics.Fitness = SimplexGenetics.FitnessType.Average;


      Genetics.Colony.ForceBoundaryConditions = cbForceBounds.Checked;


      if (rbBRandom.Checked)
        Genetics.Colony.BoundaryCondition = Simplex.BoundaryConditionsType.Random;
      if (rbBSticky.Checked)
        Genetics.Colony.BoundaryCondition = Simplex.BoundaryConditionsType.Sticky;
      if (rbBPeriodic.Checked)
        Genetics.Colony.BoundaryCondition = Simplex.BoundaryConditionsType.Periodic;
      if (rbBReflective.Checked)
        Genetics.Colony.BoundaryCondition = Simplex.BoundaryConditionsType.Reflective;

      Genetics.ShrinkBoundaryPerGeneration = int.Parse(tbShrinkGen.Text);
      Genetics.ShrinkBoundaryFactor = double.Parse(tbBoundShrink.Text);
      Genetics.ResetOnShrink = cbResetOnShrink.Checked;

      if (rbShrinkBest.Checked)
        Genetics.Shrink = SimplexGenetics.ShrinkType.ShrinkAround;
      if (rbOffsetToBest.Checked)
        Genetics.Shrink = SimplexGenetics.ShrinkType.ChangeLowerIfNeg;



      if (rbMKingHenry.Checked)
        Genetics.Marriage = SimplexGenetics.MarriageType.KingHenry;
      if (rbMRandom.Checked)
        Genetics.Marriage = SimplexGenetics.MarriageType.Random;
      if (rbMRandomPreferable.Checked)
        Genetics.Marriage = SimplexGenetics.MarriageType.RandomPreferable;
      if (rbMHierarchical.Checked)
        Genetics.Marriage = SimplexGenetics.MarriageType.Hierarchical;
      if (rbMBestWorst.Checked)
        Genetics.Marriage = SimplexGenetics.MarriageType.BestWorst;



      Genetics.ReproductionPercent = double.Parse(tbReproductionAmount.Text);
      if (rbRDiscrete.Checked)
        Genetics.Reproduction = SimplexGenetics.ReproductionType.DiscreteMixing;
      if (rbRLinear.Checked)
        Genetics.Reproduction = SimplexGenetics.ReproductionType.LinearCombination;
      if (rbRRandom.Checked)
        Genetics.Reproduction = SimplexGenetics.ReproductionType.RandomType;

    }

    /// <summary>
    /// Load default button
    /// </summary>
    private void buttonDefaults_Click(object sender, EventArgs e)
    {
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

      Genetics = new SimplexGenetics(0, 16, UpperBounds, LowerBounds);

      Settings_Load(this, EventArgs.Empty);

      requestGraphReset = true;
    }  

  }
}

