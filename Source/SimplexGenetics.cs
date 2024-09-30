
using System;
using System.Collections.Generic;
using System.Text;
using Logging;

namespace Optimization
{
  // 1/23/08
  /* ******************************************************************************************
   *                                          SimplexGenetics
   * ******************************************************************************************
   * 
   * Summary:
   *      Performs a genetic algorithm on a simplex colony via event handling.  Note that the
   *      genetic algotithm is an older and simpler version of the current GA in the 
   *      optimization library.  For example, there is no mutation.
   *      
   * Implimentation:
   *      Simply add SimplexGenetics.WorkColonyFinished to SimplexWorkColony.Finished 
   *      event handler
   * 
   * Properties and Fields:
   *      double ReproductionPercent(0-1):
   *          - percentage of population to reproduce, those who do not move on
   *      
   *      MarriageType Marriage:
   *          - KingHenry:        king will marry best to worse
   *          - Random:           random selection
   *          - Heirarchial:      best will marry the next best with no polygamy
   *          * RandomPreferable: two random selections for each mate taking best
   *          - BestWorst:        best marries worst
   * 
   *      ReproductionType Reproduction
   *          - DiscreteMixing:       1-to-1 random placement of parents into children
   *          - LinearCombination:    1-to-1 random linear combination of parents into children
   *          * RandomType:           random selection of above
   * 
   *      
   *      int MaxGenerations:
   *          - maximum number of generation to make (default 10)
   *      int int MaxEvaluations
   *          - maximum number of evaluations to allow (default max)
   *      int TotalEvaluations
   *          - total number of evaluations
   * 
   *      double BestEvaluation:
   *          - best evaluation from entire history (immortal or not)
   *      double[] BestParameter:
   *          - parameter set yielding best evaluation
   *      string BestWorker:
   *          - worker yielding best evaluation
   * 
   *      Additional properties have recently been added by request to allow for dynamic 
   *          boundary shrinking.  See code for details.
   *
   * Copyright John P. Hilbert 2008
   * 
   * ******************************************************************************************

   */

  public class SimplexGenetics
  {
    /// <summary>
    /// Colony that works then evolves
    /// </summary>
    /// <remarks>
    /// Note that the colony's population is actually static, 
    /// in other words, the children = the parents
    /// </remarks>
    public SimplexWorkColony Colony;        // Colony that does the work

    private int populationCount;            // population of colony
    private int parameterCount;             // number of parameters
    private int vectorCount;                // number of vectors

    /// <summary>
    /// Gets the current generation
    /// </summary>
    public int Generation
    {
      get { return generation; }
    }
    private int generation;                 // currrent generation

    /// <summary>
    /// Log of the Genetics which includes the Colony's
    /// </summary>
    public SimpleLogger GeneticLog;

    /// <summary>
    /// Percentage of population to marry
    /// </summary>
    public double ReproductionPercent
    {
      get { return reproductionPercent; }
      set
        {
          if (value > 1 || value < 0)
            throw new System.ArgumentException("value must be within [0,1]");
          else
            reproductionPercent = value;
        }
    }
    private double reproductionPercent;     // percentage of population that will marry

    private Random randomNumber;            // the global random number
    private string king;                    // name of king

    // total history of the colony
    private SortedList<string, List<Simplex.ValueParameter>> simplexHistory;

    // current history of colony
    private SortedList<string, List<Simplex.ValueParameter>> simplexCurrentGeneration;


    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="seed">seed for random number</param>
    public SimplexGenetics(int seed)
    {
      simplexHistory = new SortedList<string, List<Simplex.ValueParameter>>();
      marriageList = new List<string[]>();
      generation = 1;
      reproductionPercent = 1;
      TotalEvaluations = 0;

      bestValue = int.MaxValue;
      bestParameters = null;
      bestWorker = null;

      Fitness = FitnessType.Min;
      Marriage = MarriageType.RandomPreferable;
      Reproduction = ReproductionType.RandomType;

      MaxEvaluations = int.MaxValue;
      MaxGenerations = 10;

      randomNumber = new Random(seed);

      Colony = new SimplexWorkColony();

      Colony.ColonyFinished += workColonyFinished;

      GeneticLog = new SimpleLogger("GeneticSimplex");

      GeneticLog.ConsoleOutput = false;

    }

    /// <summary>
    /// Constructs a genetics instance in addition to the colony
    /// </summary>
    /// <param name="seed">seed for the genetics</param>
    /// <param name="population">static population of colony</param>
    /// <param name="upperBounds">upper bounds</param>
    /// <param name="lowerBounds">lower bounds</param>
    public SimplexGenetics(int seed, int population, double[] upperBounds, double[] lowerBounds) 
      : this(seed)
      {
        Colony.CreateColony(population, upperBounds.Length);
        Colony.UpperBounds = upperBounds;
        Colony.LowerBounds = lowerBounds;            
      }

    // Finish Conditions
    /// <summary>
    /// Stopping Condition, Maximum Generations to run (default = 10)
    /// </summary>
    public int MaxGenerations;

    /// <summary>
    /// Stopping Condition, Maximum Evaluations of whole colony to run (default = max)
    /// </summary>
    public int MaxEvaluations;              

    /// <summary>
    /// Running total evaluation of whole colony
    /// </summary>
    public int TotalEvaluations;    

    /// <summary>
    /// Number of Generation to complete before shrinking (default = 0 = off)
    /// </summary>
    public int ShrinkBoundaryPerGeneration = 0;

    /// <summary>
    /// Shrink Factor (default = 0.5)
    /// </summary>
    public double ShrinkBoundaryFactor = 0.5;

    /// <summary>
    /// Randomly recreate simplexes on shrinking (default = true)
    /// </summary>
    public bool ResetOnShrink = true;

    /// <summary>
    /// Finished event (thrown when a stopping condition is met, 
    /// however they may be changed dynamically and the algorithm will 
    /// continue)
    /// </summary>
    /// <remarks>
    /// Note that prior to being thrown, the genetics have been done 
    /// and the generation has been incremented and prepared to work
    /// (though the condition is checked w/ last completed generation)
    /// </remarks>
    public event EventHandler Finished;

#region Main Algorithm

    /// <summary>
    /// Checks if any of the stopping conditions are met
    /// </summary>
    /// <returns>true if condition is met</returns>
    private bool checkFinished()
    {
      if (MaxGenerations < generation                
          | MaxEvaluations <= TotalEvaluations)
        return true;
      else
        return false;
    }

    /// <summary>
    /// Evolves the colony
    /// </summary>
    private void evolve()
    {
      buildFitnessList(Fitness);      // builds the fitness list

      // if Shrinking and Resetting, randomly resets
      if (ShrinkBoundaryPerGeneration != 0 &&
          (generation / ShrinkBoundaryPerGeneration) * ShrinkBoundaryPerGeneration == generation
          && ResetOnShrink)
        {
          generation++;                                   
          for (int s = 0; s < Colony.Workers.Count; s++)
            {
              Colony.Workers[s].MakeInitialVectors(randomNumber.Next());
              Colony.Workers[s].Name = "worker " + s.ToString() + " G" + generation.ToString();
            }
        }
      // else normal evolution
      else
        {
          generation++;                                   
          marry(Marriage);                // creates the marriage list
          reproduce(Reproduction);        // married couples reproduce               
        }

      Colony.Restart();                   // restarts the colony
    }

    /// <summary>
    /// WorkColony event handling method.  Handles this class
    /// </summary>
    /// <param name="sender">the work colony to evolve</param>
    private void workColonyFinished(object sender, EventArgs e)
    {
      GeneticLog.Merge(Colony.ColonyLog);     // merge the Log

      // Sets the following parameters
      populationCount = Colony.Workers.Count;
      parameterCount = Colony.Workers[0].NumberOfParameters;
      vectorCount = parameterCount + 1;

      // initiate the list to hold the current generation
      simplexCurrentGeneration = new SortedList<string, List<Simplex.ValueParameter>>();

      // deep copy the final state of colony to the history and current
      foreach (Simplex s in Colony.Workers)
        {
          simplexHistory.Add(s.Name, new List<Simplex.ValueParameter>(vectorCount));
          simplexCurrentGeneration.Add(s.Name, new List<Simplex.ValueParameter>(vectorCount));
          for (int v = 0; v < vectorCount; v++)
            {
              simplexHistory[s.Name].Add(
                                         new Simplex.ValueParameter(s.Name, s.Vertices[v].Value, new double[parameterCount]));
              simplexCurrentGeneration[s.Name].Add(
                                                   new Simplex.ValueParameter(s.Name, s.Vertices[v].Value, new double[parameterCount]));
              for (int p = 0; p < parameterCount; p++)
                {
                  simplexHistory[s.Name][v].Parameters[p] = s.Vertices[v].Parameters[p];
                  simplexCurrentGeneration[s.Name][v].Parameters[p] = s.Vertices[v].Parameters[p];
                }
            }

          TotalEvaluations += s.EvaluationCount;
        }

      GeneticLog.Add(21, "Current Evaluation Count = {0}", TotalEvaluations);

      evolve();

      if (checkFinished())           // if finished
        {
          findBest();
          GeneticLog.Add(21, "{0}: Best v = {1}", bestWorker, bestValue);

          StringBuilder k = new StringBuilder(bestWorker + " with parameters \n");

          for (int i = 0; i < parameterCount; i++)
            k.AppendFormat("\t {0} \n", bestParameters[i]);

          GeneticLog.Add(21, k.ToString());

          GeneticLog.Add(21, "Total evals = {0}", TotalEvaluations);

          if (Finished != null)
            Finished(this, EventArgs.Empty);
        }
    }

#region Fitness

    /// <summary>
    /// Type of Fitness function (since each simplex has dim+1 values)
    /// </summary>
    public FitnessType Fitness;

    /// <summary>
    /// <para>Min = minimum value is fitness</para>
    /// <para>Max = maximum value is fitness</para>
    /// <para>Average = average of values is fitness</para>
    /// </summary>
    public enum FitnessType { Min, Max, Average };

    private string[] fitList;

    private void buildFitnessList(FitnessType fitType)
    {
      fitList = new string[populationCount];
      double[] valueList = new double[populationCount];

      for (int i = 0; i < populationCount; i++)
        {
          fitList[i] = simplexCurrentGeneration.Keys[i];

          switch (fitType)
            {
            case FitnessType.Min:
              valueList[i] = simplexCurrentGeneration.Values[i][0].Value;
              break;
            case FitnessType.Max:
              valueList[i] = simplexHistory.Values[i][vectorCount - 1].Value;
              break;
            case FitnessType.Average:
              foreach (Simplex.ValueParameter vp in simplexHistory.Values[i])
                valueList[i] += vp.Value;
              valueList[i] /= vectorCount;
              break;
            default:
              break;
            }                
        }

      sort<double, string>(valueList, fitList);

      king = fitList[0];

      // check if its time to shrink boundaries
      if (ShrinkBoundaryPerGeneration != 0 &&
          (generation / ShrinkBoundaryPerGeneration) * ShrinkBoundaryPerGeneration == generation)
        {
          //findBest();
          shrinkBoundaries(simplexHistory[king][0].Parameters);
        }

      GeneticLog.Add(21, "Gen {0} fitness done", generation);
      GeneticLog.Add(22, "{0} is king with fitness {1}", king, valueList[0]);
      StringBuilder s = new StringBuilder(king + " with parameters \n");

      for (int i = 0; i < parameterCount; i++)
        s.AppendFormat("\t {0} \n", this.simplexHistory[king][0].Parameters[i]);

      GeneticLog.Add(25, s.ToString());
    }

    // cut from Mathematic Library to be ease distribution
#region Sorting

    private static void swap<T>(ref T x, ref T y)
    {
      T z = x;
      x = y;
      y = z;
    }

    /// <summary>
    /// Sorts a generic array with a follower (i.e. the follower's order will 
    /// be the same order as the sorted array)
    /// </summary>
    /// <typeparam name="T">type of array to sort</typeparam>
    /// <typeparam name="U">type of array to follow</typeparam>
    /// <param name="arrayToSort">array to sort</param>
    /// <param name="follower">array to follow</param>
    private static void sort<T, U>(T[] arrayToSort, U[] follower) where T : IComparable<T>
      {
        if (arrayToSort.Length != follower.Length)
          throw new System.ArgumentException("Arrays must be of equal length");

        int M = 7;
        int i, j, k;
        int l = 1;

        int ir = arrayToSort.Length;
        int jstack = -1;
        int[] istack = new int[NSTACK];

        T a;
        U b;

        for (; ; )
          {
            if (ir - l < M)
              {
                for (j = l + 1; j <= ir; j++)
                  {
                    a = arrayToSort[j - 1];
                    b = follower[j - 1];
                    for (i = j - 1; i >= l; i--)
                      {
                        if (arrayToSort[i - 1].CompareTo(a) <= 0) break;
                        arrayToSort[i] = arrayToSort[i - 1];
                        follower[i] = follower[i - 1];
                      }
                    arrayToSort[i] = a;
                    follower[i] = b;
                  }
                if (jstack < 0) break;
                ir = istack[jstack--];
                l = istack[jstack--];
              }
            else
              {
                k = (l + ir) >> 1;
                swap<T>(ref arrayToSort[k - 1], ref arrayToSort[l]);
                swap<U>(ref follower[k - 1], ref follower[l]);

                if (arrayToSort[l - 1].CompareTo(arrayToSort[ir - 1]) > 0)
                  {
                    swap<T>(ref arrayToSort[l - 1], ref arrayToSort[ir - 1]);
                    swap<U>(ref follower[l - 1], ref follower[ir - 1]);
                  }
                if (arrayToSort[l].CompareTo(arrayToSort[ir - 1]) > 0)
                  {
                    swap<T>(ref arrayToSort[l], ref arrayToSort[ir - 1]);
                    swap<U>(ref follower[l], ref follower[ir - 1]);
                  }
                if (arrayToSort[l - 1].CompareTo(arrayToSort[l]) > 0)
                  {
                    swap<T>(ref arrayToSort[l - 1], ref arrayToSort[l]);
                    swap<U>(ref follower[l - 1], ref follower[l]);
                  }

                i = l + 1;
                j = ir;
                a = arrayToSort[l];
                b = follower[l];

                for (; ; )
                  {
                    do
                      i++;
                    while (arrayToSort[i - 1].CompareTo(a) < 0);
                    do
                      j--;
                    while (arrayToSort[j - 1].CompareTo(a) > 0);
                    if (j < i) break;
                    swap<T>(ref arrayToSort[i - 1], ref arrayToSort[j - 1]);
                    swap<U>(ref follower[i - 1], ref follower[j - 1]);
                  }
                arrayToSort[l] = arrayToSort[j - 1];
                follower[l] = follower[j - 1];
                arrayToSort[j - 1] = a;
                follower[j - 1] = b;
                jstack += 2;

                if (jstack > NSTACK)
                  throw new System.Exception("NSTACK too small in sort.");

                if (ir - i + 1 >= j - 1)
                  {
                    istack[jstack] = ir;
                    istack[jstack - 1] = i;
                    ir = j - 1;
                  }
                else
                  {
                    istack[jstack] = j - 1;
                    istack[jstack - 1] = l;
                    l = i;
                  }

              }
          }
      }

    private static int NSTACK = 50;

#endregion

    /* This was implimented by request for a specific problem. In general it
     * has been shown to increase the error and duration in finding the optimum.
     */
#region Dynamic Boundaries

    /// <summary>
    /// Shrink Boundary Type (default = ChangeLowerIfNeg)
    /// </summary>
    public ShrinkType Shrink = ShrinkType.ChangeLowerIfNeg;

    /// <summary>
    /// <para>ShrinkAround = shrinks around best (by ShrinkFactor)</para>
    /// <para>ChangeLowerIfNeg = sets lower if negative</para>
    /// </summary>
    public enum ShrinkType { ShrinkAround, ChangeLowerIfNeg };

    /// <summary>
    /// Shrink Boundary method
    /// </summary>        
    /// <param name="center">center parameter</param>
    private void shrinkBoundaries(double[] center)
    {
      GeneticLog.Add(21, "Shrinking Boundaries via {0}", Shrink);

      StringBuilder sb = new StringBuilder("Up: " + Colony.UpperBounds[0].ToString());

      for (int i = 1; i < Colony.UpperBounds.Length; i++)
        sb.AppendFormat(", {0}", Colony.UpperBounds[i]);

      GeneticLog.Add(23, sb.ToString());

      sb = new StringBuilder("Lo: " + Colony.LowerBounds[0].ToString());

      for (int i = 1; i < Colony.LowerBounds.Length; i++)
        sb.AppendFormat(", {0}", Colony.LowerBounds[i]);

      GeneticLog.Add(23, sb.ToString());

      switch (Shrink)
        {
        case ShrinkType.ShrinkAround:
          for (int i = 0; i < Colony.UpperBounds.Length; i++)
            {
              double range = (Colony.UpperBounds[i] - Colony.LowerBounds[i]);
              Colony.UpperBounds[i] = center[i] + range * ShrinkBoundaryFactor;
              Colony.LowerBounds[i] = center[i] - range * ShrinkBoundaryFactor;
            }
          break;
        case ShrinkType.ChangeLowerIfNeg:
          for (int i = 0; i < Colony.LowerBounds.Length; i++)
            if (Colony.LowerBounds[i] < 0)
              Colony.LowerBounds[i] = center[i];
          break;
        default:
          break;
        }

      sb = new StringBuilder("*Up: " + Colony.UpperBounds[0].ToString());

      for (int i = 1; i < Colony.UpperBounds.Length; i++)
        sb.AppendFormat(", {0}", Colony.UpperBounds[i]);

      GeneticLog.Add(23, sb.ToString());

      sb = new StringBuilder("*Lo: " + Colony.LowerBounds[0].ToString());

      for (int i = 1; i < Colony.LowerBounds.Length; i++)
        sb.AppendFormat(", {0}", Colony.LowerBounds[i]);

      GeneticLog.Add(23, sb.ToString());
    }

#endregion

#endregion

#region Marriage

    /// <summary>
    /// Type of marriage to use (default = RandomPreferable)
    /// </summary>
    public MarriageType Marriage;

    /// <summary>
    /// None of these marriages allow self marriage
    /// <para>KingHenry = king will marry everyone</para>
    /// <para>Random = complete random pairing</para>
    /// <para>RandomPreferable = random pairing with preference to fitness</para>
    /// <para>Hierarchical = best marries next best</para>
    /// <para>BestWorst = best marries worst</para>
    /// </summary>
    public enum MarriageType { KingHenry, Random, RandomPreferable, Hierarchical, BestWorst}

      private List<string[]> marriageList;

    private void marry(MarriageType marriageType)
    {
      marriageList.Clear();

      switch (marriageType)
        {
        case MarriageType.KingHenry:
          for (int i = 0; i < (populationCount + 1) / 2; i++)
            marriageList.Add(new string[] { king, fitList[i + 1] });
          break;

        case MarriageType.Random:
          for (int i = 0; i < (populationCount + 1) / 2; i++)
            {
              int p1 = randomNumber.Next(fitList.Length);
              int p2 = randomNumber.Next(fitList.Length - 1);
              if (p2 >= p1)
                p2++;
              marriageList.Add(new string[] { fitList[p1], fitList[p2] });
            }
          break;

        case MarriageType.RandomPreferable:
          for (int i = 0; i < (populationCount + 1) / 2; i++)
            {
              int p1 = randomNumber.Next(fitList.Length);
              int ptemp = randomNumber.Next(fitList.Length);
              if (ptemp < p1)
                p1 = ptemp;

              int p2 = randomNumber.Next(fitList.Length - 1);
              if (p2 >= p1)
                p2++;
              ptemp = randomNumber.Next(fitList.Length - 1);
              if (ptemp >= p1)
                ptemp++;

              if (ptemp < p2)
                p2 = ptemp;

              marriageList.Add(new string[] { fitList[p1], fitList[p2] });
            }
          break;

        case MarriageType.Hierarchical:
          for (int i = 0; i < populationCount; i += 2)
            {
              int p1 = i;
              int p2 = i + 1;
              if (p2 >= populationCount)
                p2 = 0;

              marriageList.Add(new string[] { fitList[p1], fitList[p2] });
            }
          break;

        case MarriageType.BestWorst:
          for (int i = 0; i < (populationCount + 1) / 2; i ++)
            {
              int p1 = i;
              int p2 = populationCount - 1 - i;
              if (p1 == p2)
                p2 = 0;

              marriageList.Add(new string[] { fitList[p1], fitList[p2] });
            }
          break;

        default:
          break;
        }
    }

#endregion

#region Reproduction

    /// <summary>
    /// Type of Reproduction (default = RandomType)
    /// </summary>
    public ReproductionType Reproduction = ReproductionType.RandomType;

    /// <summary>
    /// <para>DiscreteMixing = randomly selects parents parameters for children</para>
    /// <para>LinearCombination = random linear combination of parents become children</para>
    /// <para>RandomType = randomly chooses between Discrete and Linear</para>
    /// <para>BestSize = DO NOT USE</para>
    /// </summary>
    public enum ReproductionType { DiscreteMixing, LinearCombination, RandomType, BestSize }

      private void reproduce(ReproductionType reproductionType)
    {
      if (reproductionType == ReproductionType.BestSize)
        reproductionType = ReproductionType.RandomType;

      int workerNumber = 0;

      foreach (string[] parents in marriageList)
        {
          bool OddBall = (Colony.Workers.Count == workerNumber + 1);

          List<Simplex.ValueParameter> child1 = new List<Simplex.ValueParameter>();
          List<Simplex.ValueParameter> child2 = new List<Simplex.ValueParameter>();

          Colony.Workers[workerNumber].Name =
            "worker " + workerNumber.ToString() + " G" + generation.ToString();
          if (!OddBall)
            Colony.Workers[workerNumber + 1].Name =
              "worker " + (workerNumber + 1).ToString() + " G" + generation.ToString();

          string child1Name = "worker " + workerNumber.ToString() + " G" + generation.ToString();
          string child2Name = "worker " + (workerNumber + 1).ToString() + " G" + generation.ToString();

          ReproductionType reproType = reproductionType;
          if (reproductionType == ReproductionType.RandomType)
            reproType = (ReproductionType)randomNumber.Next(2);

          if (reproductionType == ReproductionType.BestSize)
            {
              // get best parameters of parents

              double[] p1Parameters = simplexHistory[parents[0]][0].Parameters;                    
              double[] p2Parameters = simplexHistory[parents[1]][0].Parameters;

              for (int v = 0; v < vectorCount; v++)
                {
                  Colony.Workers[workerNumber].Vertices[v].Name
                    = Colony.Workers[workerNumber].Name;
                  Colony.Workers[workerNumber].Vertices[v].Value = double.NaN;

                  if (!OddBall)
                    {
                      Colony.Workers[workerNumber + 1].Vertices[v].Name
                        = Colony.Workers[workerNumber + 1].Name;
                      Colony.Workers[workerNumber + 1].Vertices[v].Value = double.NaN;
                    }
                }

              for (int p = 0; p < parameterCount; p++)
                if (0 == randomNumber.Next(2))
                  {
                    for (int v = 0; v < vectorCount; v++)
                      {
                        Colony.Workers[workerNumber].Vertices[v].Parameters[p]
                          = p1Parameters[p];
                        if (!OddBall)
                          Colony.Workers[workerNumber + 1].Vertices[v].Parameters[p]
                            = p2Parameters[p];
                      }
                  }
                else
                  {
                    for (int v = 0; v < vectorCount; v++)
                      {
                        Colony.Workers[workerNumber].Vertices[v].Parameters[p]
                          = p2Parameters[p];
                        if (!OddBall)
                          Colony.Workers[workerNumber + 1].Vertices[v].Parameters[p]
                            = p1Parameters[p];
                      }
                  }

              double size = 100;

              for (int v = 1; v < vectorCount; v++)
                {
                  Colony.Workers[workerNumber].Vertices[v].Parameters[v - 1] += size;
                  if (!OddBall)
                    Colony.Workers[workerNumber + 1].Vertices[v].Parameters[v - 1] += size;
                }

              workerNumber += 2;
              continue;
            }


          if (randomNumber.NextDouble() >= reproductionPercent)
            reproType = ReproductionType.RandomType;    // this will force to default

          for (int v = 0; v < vectorCount; v++)
            {
              Colony.Workers[workerNumber].Vertices[v].Name
                = Colony.Workers[workerNumber].Name;
              Colony.Workers[workerNumber].Vertices[v].Value = double.NaN;

              if (!OddBall)
                {
                  Colony.Workers[workerNumber + 1].Vertices[v].Name
                    = Colony.Workers[workerNumber + 1].Name;
                  Colony.Workers[workerNumber + 1].Vertices[v].Value = double.NaN;
                }

              for (int p = 0; p < parameterCount; p++)
                switch (reproType)
                  {
                  case ReproductionType.DiscreteMixing:
                    if (0 == randomNumber.Next(2))
                      {
                        Colony.Workers[workerNumber].Vertices[v].Parameters[p]
                          = simplexHistory[parents[0]][v].Parameters[p];
                        if (!OddBall)
                          Colony.Workers[workerNumber + 1].Vertices[v].Parameters[p]
                            = simplexHistory[parents[1]][v].Parameters[p];
                      }
                    else
                      {
                        Colony.Workers[workerNumber].Vertices[v].Parameters[p]
                          = simplexHistory[parents[1]][v].Parameters[p];
                        if (!OddBall)
                          Colony.Workers[workerNumber + 1].Vertices[v].Parameters[p]
                            = simplexHistory[parents[0]][v].Parameters[p];
                      }
                    break;
                  case ReproductionType.LinearCombination:
                    double mixing = 2 * randomNumber.NextDouble() - 0.5;

                    Colony.Workers[workerNumber].Vertices[v].Parameters[p]
                      = mixing * simplexHistory[parents[0]][v].Parameters[p]
                      + (1 - mixing) * simplexHistory[parents[1]][v].Parameters[p];
                    if (!OddBall)
                      Colony.Workers[workerNumber + 1].Vertices[v].Parameters[p]
                        = mixing * simplexHistory[parents[1]][v].Parameters[p]
                        + (1 - mixing) * simplexHistory[parents[0]][v].Parameters[p];
                    break;
                  default:
                    Colony.Workers[workerNumber].Vertices[v].Parameters[p]
                      = simplexHistory[parents[0]][v].Parameters[p];
                    if (!OddBall)
                      Colony.Workers[workerNumber + 1].Vertices[v].Parameters[p]
                        = simplexHistory[parents[1]][v].Parameters[p];
                    break;
                  }
            }
          workerNumber += 2;
        }
      // buffer population

    }

#endregion

#endregion

#region Best Output

    public double BestEvaluation
    {
      get { return bestValue; }
    }

    public double[] BestParameters
    {
      get { return bestParameters; }
    }

    public string BestWorker
    {
      get { return bestWorker; }
    }

    private double bestValue;
    private double[] bestParameters;
    private string bestWorker;

    private void findBest()
    {
      double runningBestValue = double.PositiveInfinity;
      double[] runningBestParameters = new double[parameterCount];
      string runningBestWorker = null;

      foreach (string worker in simplexHistory.Keys)
        for (int v = 0; v < vectorCount; v++)
          if (simplexHistory[worker][v].Value < runningBestValue)
            {
              runningBestValue = simplexHistory[worker][v].Value;
              runningBestParameters = simplexHistory[worker][v].Parameters;
              runningBestWorker = worker;
            }

      bestValue = runningBestValue;
      bestWorker = runningBestWorker;
      bestParameters = new double[parameterCount];
      for (int p = 0; p < parameterCount; p++)
        bestParameters[p] = runningBestParameters[p];
    }

#endregion

  }
}

