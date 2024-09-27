
using System;
using System.Collections.Generic;
using System.Text;
using Logging;

namespace Optimization
{   
  // 01/21/08
  /* ********************************************************************************
   *                                   Simplex Class
   * ********************************************************************************
   * 
   * Summary:
   *      This is an event driven Down Hill Simplex algorithm for minimizing a
   *      function.  It is derived from the Neader-Meade method.
   * 
   * Implimentation:
   *      Event Driven:
   *          - initialize the class giving the # of parameters
   *          - set method to handle PurgeQueue Event (see below) and has access
   *              to a Queue of EventHandlers
   *          - run a MakeInitialVectors method
   *              (this is standalone / no evaluations are triggered)
   *          - add the FindMinimum event to Queue
   *          - Dequeue until empty
   *          - use Best... properties to collect results
   *      
   * PurgeQueue Event:
   *      this event is triggered when the algorithm need evaluation(s).  A method
   *      handling this event MUST do:
   *          - evaluate all items in EvaluationQueue.  Pulling .Parameters and storing
   *              in .Value
   *          - EnQueue next task into event queue via
   *              ((Simplex.PurgeQueueEventArgs)eventArg).NextTask
   *      In this manner the event queue stays full until NextTask is null, in which case
   *      Finished would have been thrown.
   *      
   * Properties and Fields:
   *      int MaxIterations:
   *          - inclusive maximimum iterations to do
   *      int MaxEvaluations:
   *          - inclusive maximimum evaluations to do (fuzzy)
   *      bool ForceBoundaryConditions:
   *          - forces simplex within boundary if true
   *      string Name:
   *          - internal name of simplex
   *      double[] UpperBounds:
   *          - upper bounds of parameter domain (> LowerBounds)
   *      double[] LowerBounds:
   *          - lower bounds of parameter domain (< UpperBounds)
   *      double BestEvaluation:
   *          - best evaluation
   *      double[] BestParameters:
   *          - parametes yielding best evaluation
   *      double GrowFactor:
   *          - factor to grow simplex (default 2); must be greater than 1
   *      double ShrinkFactor:
   *          - factor to shrink simplex (default 1/2); must be within [0,1]
   *      double RelativeAverageSize:
   *          - average[ (best - center) / domain ]
   *      double EuclidianSize:
   *          - || best - center ||
   *      doubel[] Center:
   *          - parameters of the center of simplex
   *      int EvaluationCount:
   *      int NumberOfParameters:
   *      
   * Copyright John P. Hilbert 2008
   * 
   * ********************************************************************************/

  /// <summary>
  /// A single multi-leg geometric construct for optimization
  /// </summary>
  public class Simplex
  {
    Random rnd = new Random();      // multi use random number

    /// <summary>
    /// Name of Simplex
    /// </summary>
    public string Name;

    /// <summary>
    /// Log for simplex
    /// </summary>
    public SimpleLogger SimplexLog;

    /// <summary>
    /// Ordered list of best values
    /// </summary>
    public List<double> BestList;

    /// <summary>
    /// Class containing a Value(double) / Parameter(double[]) pair
    /// </summary>
    public class ValueParameter : IComparable<ValueParameter>
    {
      /// <summary>
      /// Owner of this
      /// </summary>
      public string Name;

      /// <summary>
      /// Value of the evaluation of Parameters
      /// </summary>
      public double Value;

      /// <summary>
      /// Parameters yielding Value
      /// </summary>
      public double[] Parameters;

      /// <summary>
      /// Constructs empty VP
      /// </summary>
      /// <param name="parameterLength">number of parameters</param>
      public ValueParameter(int parameterLength)
      {
        Name = null;
        Value = double.NaN;
        Parameters = new double[parameterLength];
      }

      /// <summary>
      /// Constructs a copy of a VP
      /// </summary>
      /// <param name="vp">VP to copy</param>
      public ValueParameter(ValueParameter vp)
      {
        Name = vp.Name;
        Value = vp.Value;
        Parameters = vp.Parameters;
      }

      /// <summary>
      /// Constructs from a name, value, and parameter
      /// </summary>
      /// <param name="name">name of this</param>
      /// <param name="v">value</param>
      /// <param name="p">parameters</param>
      public ValueParameter(string name, double v, double[] p)
      {
        Name = name;
        Value = v;
        Parameters = p;
      }

      /// <summary>
      /// Compares based on value
      /// </summary>
      /// <param name="other">other VP</param>
      /// <returns>-1 if this is less than other</returns>
      public int CompareTo(ValueParameter other)
      {
        if (this.Value < other.Value)
          return -1;
        else if (this.Value > other.Value)
          return 1;
        else
          return 0;
      }
    }

    /// <summary>
    /// List of the vertices defining the current state of the simplex
    /// </summary>
    public List<ValueParameter> Vertices;

    /// <summary>
    /// List of value/parameters requiring evaluation
    /// </summary>
    public List<ValueParameter> EvaluationQueue;

    private double[] pSum;                  // Sum of vertices of the simplex
    private int iterationCount;             // running number of iterations
    private int evaluationCount;            // running number of evaluations
    private int numberOfParameters;         // dimensions of parameter space
    private int maxEvaluations;             // maximum evaluations to make

    private double shrinkFactor, 
      growFactor;                         // shrink and grow factors
    private double[] upperBounds,
      lowerBounds;                        // upper / lower bounds of parameter space

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="dimensions">number of parameters</param>
    public Simplex(int dimensions)
    {
      numberOfParameters = dimensions;
      Vertices = new List<ValueParameter>();
      pSum = new double[numberOfParameters];
      EvaluationQueue = new List<ValueParameter>();

      maxEvaluations = 2 * (dimensions + 1);
      MinRelSize = 0;

      shrinkFactor = 0.5;
      growFactor = 2;

      upperBounds = new double[numberOfParameters];
      lowerBounds = new double[numberOfParameters];
      for (int i = 0; i < numberOfParameters; i++)
        {
          upperBounds[i] = (double)int.MaxValue;
          lowerBounds[i] = (double)int.MinValue;
        }

      ForceBoundaryConditions = true;
      BoundaryCondition = BoundaryConditionsType.Sticky;

      Name = "Simplex";
      BestList = new List<double>();
    }

#region Functions and Events

    /// <summary>
    /// Simplex Finished Event
    /// </summary>
    public event EventHandler Finished;

    /// <summary>
    /// Thrown when stopping condition is met
    /// </summary>
    public void OnFinished()
    {
      Vertices.Sort();

      SimplexLog.Add(1, "{0} Finished with {1}",
                     Name, Vertices[0].Value);
      SimplexLog.Add(2, "{0} Euclidean Size {1} \n Relative Average Size {2}",
                     Name, EuclidianSize, RelativeAverageSize);

      if (Finished != null)
        this.Finished(this, EventArgs.Empty);   // no event args needed here.     
    }

    /// <summary>
    /// Purging Event Argument
    /// </summary>
    public class PurgeQueueEventArgs : EventArgs
    {
      public PurgeQueueEventArgs(EventHandler m)
      {
        Task = m;
      }

      public EventHandler Task;
    }

    /// <summary>
    /// EventHandler for Evaluation Queue Purging
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    public delegate void PurgeQueueHandler(object sender, PurgeQueueEventArgs e);

    /// <summary>
    /// Simplex Requesting Evaluations Event
    /// </summary>
    public event PurgeQueueHandler PurgeQueue;      // Event Handling Queue Purging 
    void OnPurgeQueue(EventHandler nextTask)
    {
      if (PurgeQueue != null)
        this.PurgeQueue(this, new PurgeQueueEventArgs(nextTask));
      else
        throw new System.Exception("No handling method specified for evaluation");
    }     // Method Triggering Event

#endregion

#region Main Algorithm

    /// <summary>
    /// Starts the algorithm
    /// </summary>
    public void FindMinimum(object sender, EventArgs e)
    {
      Initialize(this, EventArgs.Empty);
    }

#region The 3 Operations

    /// <summary>
    /// Reflects the Simplex
    /// </summary>
    private void reflect(object sender, EventArgs e)
    {
      if (finishCheck())
        OnFinished();
      else
        {
          Vertices.Sort();                    // sorts the vertices
          BestList.Add(Vertices[0].Value);    // adds the best to the history

          iterationCount++;                  

          EvaluationQueue.Clear();            // adds the new parameter set to the evaluation queue
          EvaluationQueue.Add(new ValueParameter(this.Name, double.NaN, extrapolateVertex(numberOfParameters, -1)));

          // add next operation: Expand or Contract
          OnPurgeQueue(expandContract);       
          evaluationCount++;

          // logs data
          SimplexLog.Add(1, "{0} Reflecting {1} \n Best: {2}",
                         Name, Vertices[numberOfParameters].Value, Vertices[0].Value);
          SimplexLog.Add(2, "{0} Euclidean Size {1} \n Relative Average Size {2}",
                         Name, EuclidianSize, RelativeAverageSize);
        }
    }

    /// <summary>
    /// Expands or contracts the simplex along a single dimension
    /// </summary>
    private void expandContract(object sender, EventArgs e)
    {
      // ***** NOTE: EvaluationQueue will always hold last eval so do not clear until needed

      // if extrapolation was better, replace
      updateLastEvaluation(this, EventArgs.Empty);

      // if the reflection is better than the current best then expand (ooze) in that direction
      if (EvaluationQueue[0].Value <= Vertices[0].Value)
        {
          EvaluationQueue.Clear();
          EvaluationQueue.Add(new ValueParameter(this.Name, double.NaN, extrapolateVertex(numberOfParameters, growFactor)));

          // add next operation: Update & Reflect
          EventHandler tempTasks = new EventHandler(updateLastEvaluation);
          tempTasks += reflect;
          OnPurgeQueue(tempTasks);
          evaluationCount++;

          // log
          SimplexLog.Add(1, "{0} Growing {1}",
                         Name, Vertices[numberOfParameters].Value);
          SimplexLog.Add(2, "{0} Euclidean Size {1} \n Relative Average Size {2}",
                         Name, EuclidianSize, RelativeAverageSize);

        }

      // OR if the reflection still didn't beat the next worse try a contraction
      //      note: depending on the value of the reflection it will either contract the 
      //          post or prior vertex
      else if (EvaluationQueue[0].Value >= Vertices[numberOfParameters - 1].Value)
        {
          EvaluationQueue.Clear();
          EvaluationQueue.Add(new ValueParameter(this.Name, double.NaN, extrapolateVertex(numberOfParameters, shrinkFactor)));

          // add next operation: Contract All
          OnPurgeQueue(contractAll);
          evaluationCount++;

          // log
          SimplexLog.Add(1, "{0} Shrinking {1}",
                         Name, Vertices[numberOfParameters].Value);
          SimplexLog.Add(2, "{0} Euclidean Size {1} \n Relative Average Size {2}",
                         Name, EuclidianSize, RelativeAverageSize);
        }
      else
        reflect(this, EventArgs.Empty);
    }

    /// <summary>
    /// Contracts the simplex along all dimensions
    /// </summary>
    private void contractAll(object sender, EventArgs e)
    {
      // if extrapolation was better, replace and reflect
      if (EvaluationQueue[0].Value < Vertices[numberOfParameters].Value)
        {
          for (int i = 0; i < numberOfParameters; ++i)
            pSum[i] += EvaluationQueue[0].Parameters[i] - Vertices[numberOfParameters].Parameters[i];
          Vertices[numberOfParameters].Value = EvaluationQueue[0].Value;
          Vertices[numberOfParameters].Parameters = EvaluationQueue[0].Parameters;

          // reflection
          if (finishCheck())
            OnFinished();
          else
            {
              Vertices.Sort();                    // sorts the vertices
              BestList.Add(Vertices[0].Value);    // adds the best to the history

              iterationCount++;

              EvaluationQueue.Clear();            // adds the new parameter set to the evaluation queue
              EvaluationQueue.Add(new ValueParameter(this.Name, double.NaN, extrapolateVertex(numberOfParameters, -1)));

              // add next operation: Expand or Contract
              OnPurgeQueue(expandContract);
              evaluationCount++;

              // log
              SimplexLog.Add(1, "{0} Reflecting {1} \n Best: {2}",
                             Name, Vertices[numberOfParameters].Value, Vertices[0].Value);
              SimplexLog.Add(2, "{0} Euclidean Size {1} \n Relative Average Size {2}",
                             Name, EuclidianSize, RelativeAverageSize);
            }
        }

      // OR we failed, then contract all around best point
      else
        {
          // this loop is simply a more robust extrapolation
          EvaluationQueue.Clear();
          for (int i = 1; i < numberOfParameters + 1; ++i)
            {
              double[] tempParameters = new double[numberOfParameters];
              for (int j = 0; j < numberOfParameters; ++j)
                tempParameters[j] = shrinkFactor *
                  (Vertices[i].Parameters[j] + Vertices[0].Parameters[j]);

              if (ForceBoundaryConditions)
                forceBoundary(ref tempParameters);

              Vertices[i].Parameters = tempParameters;
              EvaluationQueue.Add(new ValueParameter(this.Name, double.NaN, tempParameters));
              evaluationCount++;
            }

          // add next operation: Update & Reflect
          EventHandler tempTasks = new EventHandler(updateChangedEvaluations);
          tempTasks += reflect;
          OnPurgeQueue(tempTasks);

          // log
          SimplexLog.Add(1, "{0} Shrinking around {1}",
                         Name, Vertices[0].Value);
          SimplexLog.Add(2, "{0} Euclidean Size {1} \n Relative Average Size {2}",
                         Name, EuclidianSize, RelativeAverageSize);
        }
    }
#endregion

#region Evaluation Update

    /// <summary>
    /// Updates any changed vertices
    /// </summary>       
    private void updateChangedEvaluations(object sender, EventArgs e)
    {
      for (int i = 0; i < EvaluationQueue.Count; i++)
        for(int j = 0; j < Vertices.Count; j++)
          if (EvaluationQueue[i].Parameters == Vertices[j].Parameters)
            {
              Vertices[j].Value = EvaluationQueue[i].Value;
              break;
            }

      updateCenter();     // update center
    }

    /// <summary>
    /// Updates last evaluation if better
    /// </summary>
    private void updateLastEvaluation(object sender, EventArgs e)
    {
      // note: last eval is 0, worse vertex is at end (post sort)
      if (EvaluationQueue[0].Value < Vertices[numberOfParameters].Value)
        {   
          // update center manually
          for (int i = 0; i < numberOfParameters; ++i)
            pSum[i] += EvaluationQueue[0].Parameters[i] - Vertices[numberOfParameters].Parameters[i];

          // replace value / parameters
          Vertices[numberOfParameters].Value = EvaluationQueue[0].Value;
          Vertices[numberOfParameters].Parameters = EvaluationQueue[0].Parameters;
        }
    }
#endregion

#region Misc Operations

    /// <summary>
    /// Checks if algorithm is finished
    /// </summary>
    /// <returns>true if any one condition is met</returns>
    private bool finishCheck()
    {
      if (evaluationCount >= maxEvaluations)
        return true;
      if (RelativeAverageSize <= MinRelSize)
        return true;

      // add more conditions if needed

      return false;
    }

    /// <summary>
    /// Does a transformation along one dimension of the simplex (reflection, contraction, expansion)
    /// </summary>
    /// <param name="index">index of dimension to work on</param>
    /// <param name="factor">factor to transform by (-1 reflect, &lt; 1 contract, &gt; 1 expand)</param>
    /// <returns>new trial point</returns>
    private double[] extrapolateVertex(int index, double factor)
    {
      updateCenter();     // updates the simplex center

      double[] trialParameters = new double[numberOfParameters];
      double averageWeightingFactor = (1 - factor) / numberOfParameters;
      double indexWeightingFactor = factor - averageWeightingFactor;

      for (int i = 0; i < numberOfParameters; ++i)
        trialParameters[i] = pSum[i] * averageWeightingFactor
          + Vertices[index].Parameters[i] * indexWeightingFactor;

      // forces parameters within bounds if on
      if (ForceBoundaryConditions)
        forceBoundary(ref trialParameters);

      return trialParameters;
    }

    /// <summary>
    /// Updates the center of the simplex (average of all vectors spanning simplex)
    /// </summary>
    private void updateCenter()
    {
      for (int i = 0; i < numberOfParameters; ++i)
        {
          double sum = 0;
          for (int j = 0; j < numberOfParameters + 1; ++j)
            sum += Vertices[j].Parameters[i];
          pSum[i] = sum;
        }
    }

    /// <summary>
    /// Procedure to use if parameter is out of bound
    /// <para>Random = randomly select parameter within bounds</para>
    /// <para>Sticky = sticks to closest boundary</para>
    /// <para>Periodic = wraps parameter around one boundary to the next</para>
    /// <para>Reflective = reflects parameter against boundary</para>
    /// </summary>
    public enum BoundaryConditionsType { Random, Sticky, Periodic, Reflective }
      public BoundaryConditionsType BoundaryCondition = BoundaryConditionsType.Periodic;

    /// <summary>
    /// Method to move out of bounds arguments to within boundary
    /// </summary>
    /// <param name="parameters">parameters to check and change</param>
    private void forceBoundary(ref double[] parameters)
    {
      for (int i = 0; i < numberOfParameters; i++)
        {

          switch (BoundaryCondition)
            {
            case BoundaryConditionsType.Periodic:
              if (parameters[i] > upperBounds[i])
                {
                  double newP = parameters[i] - upperBounds[i] + lowerBounds[i];

                  // loops to check for multiple wraps
                  //      note: there is a faster mathematical method
                  //              additionally if parameter is far outside, 
                  //              this methode may still yield a parameter outside.                            
                  for (int j = 0; j < 100; j++)
                    {
                      if (newP < upperBounds[i])
                        break;
                      newP = newP - upperBounds[i] + lowerBounds[i];
                    }
                  SimplexLog.Add(9, "{0} out of bounds, setting to {1}", parameters[i], newP);
                  parameters[i] = newP;
                }
              else if (parameters[i] < lowerBounds[i])
                {
                  double newP = parameters[i] - lowerBounds[i] + upperBounds[i];

                  // loops to check for multiple wraps
                  //      note: there is a faster mathematical method
                  //              additionally if parameter is far outside, 
                  //              this methode may still yield a parameter outside.
                  for (int j = 0; j < 100; j++)
                    {
                      if (newP < lowerBounds[i])
                        break;
                      newP = newP - lowerBounds[i] + upperBounds[i];
                    }
                  SimplexLog.Add(9, "{0} out of bounds, setting to {1}", parameters[i], newP);
                  parameters[i] = newP;
                }
              break;


            case BoundaryConditionsType.Random:
              // Randomly set to inside range
              if (parameters[i] > upperBounds[i] || parameters[i] < lowerBounds[i])
                {
                  //double center = upperBounds[i] - (parameters[i] - upperBounds[i]);
                  double center = rnd.NextDouble() * (upperBounds[i] - lowerBounds[i]) + lowerBounds[i];
                  SimplexLog.Add(9, "{0} out of bounds, setting to {1}", parameters[i], center);
                  parameters[i] = center;
                }
              break;


            case BoundaryConditionsType.Reflective:
              if (parameters[i] > upperBounds[i] || parameters[i] < lowerBounds[i])
                {
                  double newP;
                  if (parameters[i] > upperBounds[i])
                    newP = 2 * upperBounds[i] - parameters[i];
                  else
                    newP = 2 * lowerBounds[i] - parameters[i];

                  // loops to check for multiple reflections
                  //      note: there is a faster mathematical method
                  //              additionally if parameter is far outside, 
                  //              this methode may still yield a parameter outside.
                  for (int j = 0; j < 1000; j++)
                    {
                      if (newP > upperBounds[i] || newP < lowerBounds[i])
                        {
                          if (newP > upperBounds[i])
                            newP = 2 * upperBounds[i] - newP;
                          else
                            newP = 2 * lowerBounds[i] - newP;
                        }
                      else
                        break;

                      newP = newP - upperBounds[i] + lowerBounds[i];
                    }

                  SimplexLog.Add(9, "{0} out of bounds, setting to {1}", parameters[i], newP);
                  parameters[i] = newP;
                }
              break;


            case BoundaryConditionsType.Sticky:
              // Forces to Boundary
              if (parameters[i] > upperBounds[i])
                {
                  double center = upperBounds[i];
                  SimplexLog.Add(9, "{0} out of bounds, setting to {1}", parameters[i], center);
                  parameters[i] = center;
                }
              else if (parameters[i] < lowerBounds[i])
                {
                  double center = lowerBounds[i];
                  SimplexLog.Add(9, "{0} out of bounds, setting to {1}", parameters[i], center);
                  parameters[i] = center;
                }
              break;
            }

        }
    }

#endregion

#endregion

#region Initialization Methods

    /// <summary>
    /// Quick, no fuss initialization of simplex from a single point.
    /// </summary>
    /// <param name="initialPoint">single point to create others from</param>
    /// <param name="unitVectorScale">offset from initial point</param>
    /// <remarks>
    /// Simply creates a set of vectors that span the parameter
    /// space of "unitVectorScale" size, centered around "initialPoint"
    /// </remarks>
    public void MakeInitialVectors(double[] initialPoint, double unitVectorScale)
    {
      this.Vertices.Clear();
      this.Vertices.Add(new ValueParameter(this.Name, double.NaN, initialPoint));

      for (int i = 0; i < numberOfParameters; ++i)
        {
          double[] newPoint = initialPoint;
          newPoint[i] += unitVectorScale;
          this.Vertices.Add(new ValueParameter(this.Name, double.NaN, newPoint));
        }
    }

    /// <summary>
    /// Quick, no fuss initialization of simplex from a single point.
    /// </summary>
    /// <param name="initialPoint">single point to create others from</param>
    /// <param name="unitVectorScales">array of equal dimension to offset point from</param>
    public void MakeInitialVectors(double[] initialPoint, double[] unitVectorScales)
    {
      if (unitVectorScales.Length != numberOfParameters)
        throw new ArgumentException("unitVectorScales is of incorrect size");

      this.Vertices.Clear();
      this.Vertices.Add(new ValueParameter(this.Name, double.NaN, initialPoint));

      for (int i = 0; i < numberOfParameters; ++i)
        {
          double[] newPoint = initialPoint;
          newPoint[i] += unitVectorScales[i];
          this.Vertices.Add(new ValueParameter(this.Name, double.NaN, newPoint));
        }
    }

    /// <summary>
    /// Randomly creates initial vectors within parameter domain
    /// </summary>
    public void MakeInitialVectors(int seed)
    {
      this.Vertices.Clear();

      double[] domainSize = new double[numberOfParameters];
      for (int i = 0; i < numberOfParameters; i++)
        domainSize[i] = upperBounds[i] - lowerBounds[i];

      Random rand = new Random(seed);

      EvaluationQueue.Clear();
      for (int j = 0; j < numberOfParameters + 1; j++)
        {
          double[] newPoint = new double[numberOfParameters];
          for (int i = 0; i < numberOfParameters; i++)
            newPoint[i] = rand.NextDouble() * domainSize[i] + lowerBounds[i];

          Vertices.Add(new ValueParameter(this.Name, double.NaN, newPoint));
        }

    }

    /// <summary>
    /// ReEvaluates each vertex, used for manual vertex entry
    /// </summary>
    public void Initialize(object sender, EventArgs e)
    {
      evaluationCount = 0;
      iterationCount = 0;

      EvaluationQueue.Clear();

      foreach(ValueParameter vp in Vertices)
        {
          if (ForceBoundaryConditions)
            forceBoundary(ref vp.Parameters);

          EvaluationQueue.Add(new ValueParameter(this.Name, double.NaN, vp.Parameters));
          evaluationCount++;
        }

      EventHandler tempTasks = new EventHandler(updateChangedEvaluations);
      tempTasks += reflect;
      OnPurgeQueue(tempTasks);

      SimplexLog = new SimpleLogger(Name);
      SimplexLog.Add(1, "{0} Initialized", Name);

    }

#endregion

#region Properties

    /// <summary>
    /// Yields the Best Evaluation of the function once found
    /// </summary>
    public double BestEvaluation
    {
      get
        {
          //Vertices.Sort();
          return Vertices[0].Value;
        }
    }

    /// <summary>
    /// Yields an array of the best parameters once found
    /// </summary>
    public double[] BestParameters
    {
      get
        {
          //Vertices.Sort();
          return Vertices[0].Parameters;
        }
    }

    /// <summary>
    /// Array of upper bounds of parameter
    /// </summary>
    public double[] UpperBounds
    {
      get { return upperBounds; }
      set
        {
          if (value.Length != numberOfParameters)
            throw new ArgumentException("Value is of incorrect length");

          for (int i = 0; i < numberOfParameters; i++)
            if (value[i] <= lowerBounds[i])
              throw new ArgumentException("Upper bounds is less than lower bounds");

          upperBounds = value;
        }
    }

    /// <summary>
    /// Array of lower bounds of parameters
    /// </summary>
    public double[] LowerBounds
    {
      get { return lowerBounds; }
      set
        {
          if (value.Length != numberOfParameters)
            throw new ArgumentException("Value is of incorrect length");

          for (int i = 0; i < numberOfParameters; i++)
            if (value[i] >= upperBounds[i])
              throw new ArgumentException("Lower bounds is greater than upper bounds");

          lowerBounds = value;
        }
    }

    /// <summary>
    /// Factor to grow the simplex by (default = 2)
    /// </summary>
    public double GrowFactor
    {
      get { return growFactor; }
      set
        {
          if (value < 1)
            throw new ArgumentException("must be > 1");
          else
            growFactor = value;
        }
    }

    /// <summary>
    /// Factor to shrink the simplex by (default = 0.5)
    /// </summary>
    public double ShrinkFactor
    {
      get { return shrinkFactor; }
      set
        {
          if (value > 1)
            throw new ArgumentException("must be < 1");
          else
            shrinkFactor = value;
        }
    }

    /// <summary>
    /// Gets the size of the simplex via average((best - center) / domain)
    /// </summary>
    public double RelativeAverageSize
    {
      get
        {
          double sum = 0;

          for (int i = 0; i < BestParameters.Length; i++)
            sum += Math.Abs(BestParameters[i] - Center[i]) / (upperBounds[i] - lowerBounds[i]);

          return sum / (numberOfParameters + 1);
        }
    }

    /// <summary>
    /// Gets the Euclidian size via |best - center|
    /// </summary>
    public double EuclidianSize
    {
      get
        {
          double sum = 0;

          for (int i = 0; i < BestParameters.Length; i++)
            sum += Math.Pow(BestParameters[i] - Center[i], 2);

          return Math.Sqrt(sum);
        }
    }

    /// <summary>
    /// Returns the center of the simplex
    /// </summary>
    public double[] Center
    {
      get
        {
          double[] temp = pSum;

          for (int i = 0; i < numberOfParameters; i++)
            temp[i] /= numberOfParameters;

          return temp;
        }
    }

    /// <summary>
    /// Gets the number of iteration
    /// </summary>
    public int IterationCount
    {
      get { return iterationCount; }
    }

    /// <summary>
    /// Gets the number of evaluations
    /// </summary>
    public int EvaluationCount
    {
      get { return evaluationCount; }
    }

    /// <summary>
    /// Gets the number of parameters
    /// </summary>
    public int NumberOfParameters
    {
      get { return numberOfParameters; }
    }

    /// <summary>
    /// Maximum Evaluations simplex will run (fuzzy) (default = 2 * (parameters + 1))
    /// </summary>
    public int MaxEvaluations
    {
      get { return maxEvaluations; }
      set
        {
          if (value <= numberOfParameters + 1)
            throw new System.ArgumentException(
                                               "Max Evaluations must be greater than number of Parameters + 1");
          else
            maxEvaluations = value;
        }
    }

    /// <summary>
    /// Minimum Relative Size simplex shrink to before stopping (default = 0)
    /// </summary>
    public double MinRelSize;

    /// <summary>
    /// Forces boundary conditions (default = false)
    /// </summary>
    public bool ForceBoundaryConditions;

#endregion
  }
}

