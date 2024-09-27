using System;
using System.Collections.Generic;
using System.Text;
using Logging;

namespace Optimization
{
  // 01/21/08
  /* ********************************************************************************
   *                                 SimplexWorkColony
   * ********************************************************************************
   * 
   * Summary:
   *      This class provides a colony of simplex workers to do the users bidding.
   *      Once initialized and created, each worker moves in parallel with all 
   *      messages being pooled within this class.  The colony will trigger an event
   *      to evaluate a chunk if a worker requires it or if the chunk has reached a
   *      particular size (hence, efficiently using resources).  Note, however, it
   *      is less efficient for the chunk size to be larger than the population since
   *      it is rare for a worker to request a multiple evaluations per pass.
   * 
   * Implimentation:      
   * 
   * Properties and Fields:
   *      int EvaluationChunkSize:
   *          - maximum size of EvaluationChunk; note that it will try to 
   *              efficiently meet this size; additionally it is more efficient to
   *              have this less than the population size
   *      bool LazyWorkers:
   *          - will stop all workers after first individual stops if true (default = true)
   * 
   *      double[] UpperBounds:
   *          - upper bounds of parameter domain (> LowerBounds)
   *      double[] LowerBounds:
   *          - lower bounds of parameter domain (< UpperBounds)
   *      int MaxIterations:
   *          - inclusive maximimum iterations to do
   *      int MaxEvaluations:
   *          - inclusive maximimum evaluations to do (fuzzy)
   *      double GrowFactor:
   *          - factor to grow simplex (default 2); must be greater than 1
   *      double ShrinkFactor:
   *          - factor to shrink simplex (default 1/2); must be within [0,1]
   *
   * Copyright John P. Hilbert 2008
   *
   * ********************************************************************************
   */

  /// <summary>
  /// Colony of Simplexs
  /// </summary>
  public class SimplexWorkColony
  {
#region Individual Worker Event Handling Methods

    int workersDone = 0;                    // number of workers finished

    /// <summary>
    /// Method to handle each indiviual worker's purge event
    /// </summary>
    private void workerRequestPurge(object sender, System.EventArgs e)
    {
      Simplex s = (Simplex)sender;
      Simplex.PurgeQueueEventArgs nextTask = (Simplex.PurgeQueueEventArgs)e;

      // add the worker's evaluation queue to the colony's
      foreach (Simplex.ValueParameter vp in s.EvaluationQueue)
        colonyEvaluationQueue.Enqueue(vp);

      TaskList.Enqueue(nextTask.Task);    // add worker's task to the colony's
    }

    /// <summary>
    /// Method to handle finished worker
    /// </summary>
    private void workerFinished(object sender, EventArgs e)
    {
      workersDone++;
      if (LazyWorkers && Workers.Count > 1)
        {
          foreach (Simplex worker in Workers)
            {
              worker.Finished -= workerFinished;
              worker.OnFinished();
              worker.Finished += workerFinished;
            }
          OnColonyFinished();
        }
    }

#endregion

    /// <summary>
    /// Colony's task list
    /// </summary>
    public Queue<EventHandler> TaskList;

    /// <summary>
    /// Workers in colony
    /// </summary>
    public List<Simplex> Workers;

    /// <summary>
    /// Colony's Best Value List (shallow copy of workers BestList)
    /// </summary>
    public List<List<double>> BestList;

    /// <summary>
    /// Colony Finished event
    /// </summary>
    public event EventHandler ColonyFinished;
    private void OnColonyFinished()
    {
      TaskList.Clear();
      colonyEvaluationQueue.Clear();

      ColonyLog.Add(11, "Colony finished");

      foreach (Simplex s in Workers)
        ColonyLog.Merge(s.SimplexLog);                

      // trigger events
      if (ColonyFinished != null)
        ColonyFinished(this, EventArgs.Empty);

      if (BestList == null)
        {
          BestList = new List<List<double>>();
          foreach (Simplex worker in Workers)
            BestList.Add(worker.BestList);
        }

      int maxSize = 0;
      foreach (List<double> list in BestList)
        if (list.Count > maxSize)
          maxSize = list.Count;

      // pads each list to make equal length
      foreach (List<double> list in BestList)
        while (list.Count < maxSize)
          list.Add(list[list.Count - 1]);            
    }

    private Queue<Simplex.ValueParameter> colonyEvaluationQueue;    // Colony's Evaluation Queue

    /// <summary>
    /// Runs the Colony until the evaluation chunk size has been reached.
    /// </summary>
    /// <remarks>
    /// The main purpose of this is to keep track of the TaskList and EvaluationQueue
    /// while running each simplex in parallel.  Its complexity is due to maximizing the
    /// EvaluationChunk whilst running all simplexs not requiring evaluations (this has 
    /// been moved to purgeQueueCheck).
    /// </remarks>
    public void Run()
    {           
      int j = 0;

      // checks if each worker has adaquate vertices
      foreach (Simplex worker in Workers)
        if (worker.Vertices.Count < worker.NumberOfParameters + 1)
          worker.MakeInitialVectors(2 * j++);

      EvaluationChunk.Clear();

      // dequeue task list until evaluation is requested or task list is empty
      while (!purgeQueueCheck() && TaskList.Count > 0)
        TaskList.Dequeue()(null, EventArgs.Empty);

      if (TaskList.Count > 0)
        OnEvaluationRequest();
    }

#region Evaluation Purging

    /// <summary>
    /// Maximum size of Evaluations to request at one time
    /// </summary>
    public int EvaluationChunkSize;

    private bool purgeQueueCheck()
    {
      // note: if next task is equal to next eval they should be in sync

      // if there are no evaluations in queue, either at the beginning or end of algorithm
      if (colonyEvaluationQueue.Count == 0)
        {
          // if there is no evaluation AND no tasks to do we are done
          if (TaskList.Count == 0)
            OnColonyFinished();
          return false;
        }


      // checks if next task still has requested evaluations in queue
      if (((Simplex)TaskList.Peek().Target).Name == colonyEvaluationQueue.Peek().Name)
        {
          string workerNeedingPurge = colonyEvaluationQueue.Peek().Name;
          //Console.WriteLine("  I Must Purge for " + workerNeedingPurge);

          // process all of evals worker requested
          while (colonyEvaluationQueue.Count > 0
                 && colonyEvaluationQueue.Peek().Name == workerNeedingPurge)
            {
              EvaluationChunk.Add(colonyEvaluationQueue.Dequeue());

              // if as processing worker's requests we created a full chunk, flush it
              if (EvaluationChunk.Count == EvaluationChunkSize)
                return true;
            }

          // if we have space in the chunk, fill with other worker's requests
          while (EvaluationChunk.Count < EvaluationChunkSize && colonyEvaluationQueue.Count > 0)
            EvaluationChunk.Add(colonyEvaluationQueue.Dequeue());

          return true;
        }


      // If I can process full chunks, do it
      while (colonyEvaluationQueue.Count >= EvaluationChunkSize)
        {
          // build a chunk
          while (EvaluationChunk.Count < EvaluationChunkSize)
            EvaluationChunk.Add(colonyEvaluationQueue.Dequeue());

          return true;
        }

      return false;
    }

    /// <summary>
    /// List of parameters needed evaluating
    /// </summary>
    public List<Simplex.ValueParameter> EvaluationChunk;

    /// <summary>
    /// Requesting Evaluation Event 
    /// (not needed since it is assumed evaluations are needed after each Run)
    /// </summary>
    public event EventHandler EvaluationRequest;

    private void OnEvaluationRequest()
    {
      if (EvaluationRequest != null)
        EvaluationRequest(this, EventArgs.Empty);
    }

#endregion

    /// <summary>
    /// Constructor
    /// </summary>
    public SimplexWorkColony()
    {
      EvaluationChunkSize = 1;

      colonyEvaluationQueue = new Queue<Simplex.ValueParameter>();

      EvaluationChunk = new List<Simplex.ValueParameter>();

      TaskList = new Queue<EventHandler>();

      LazyWorkers = true;

    }

    /// <summary>
    /// Creates a colony
    /// </summary>
    /// <param name="populationSize">number of simplexes in colony</param>
    /// <param name="parameterCount">number of parameters for each simplex</param>
    public void CreateColony(int populationSize, int parameterCount)
    {
      Workers = new List<Simplex>(populationSize);

      for (int i = 0; i < populationSize; i++)
        {
          Simplex worker = new Simplex(parameterCount);

          worker.Name = "worker " + i.ToString();     // each simplex is named "worker #"

          worker.PurgeQueue += workerRequestPurge;    // set the methods handling events
          worker.Finished += workerFinished;

          Workers.Add(worker);                        // add the worker to the list
        }

      Restart();
    }

    /// <summary>
    /// Restarts the Workers (adds FindMinimum and purgeQueueCheck to task list)
    /// </summary>
    public void Restart()
    {
      ColonyLog = new SimpleLogger("Colony");

      ColonyLog.Add(11, "Colony starting");

      foreach (Simplex worker in Workers)
        TaskList.Enqueue(worker.FindMinimum);       // add the FindMinimum event
    }

    /// <summary>
    /// Contains the colony's log which includes each worker's log
    /// </summary>
    public SimpleLogger ColonyLog;

#region Global Worker Properies

    /// <summary>
    /// Forces Colony to stop on first worker is finished (default = true)
    /// </summary>
    public bool LazyWorkers;

    /// <summary>
    /// Upper Bounds of all workers
    /// </summary>
    public double[] UpperBounds
    {
      get { return Workers[0].UpperBounds; }
      set
        {
          foreach (Simplex s in Workers)
            s.UpperBounds = value;
        }
    }

    /// <summary>
    /// Lower Bounds of all workers
    /// </summary>
    public double[] LowerBounds
    {
      get { return Workers[0].LowerBounds; }
      set
        {
          foreach (Simplex s in Workers)
            s.LowerBounds = value;
        }
    }

    /// <summary>
    /// Maximum evaluations of all workers
    /// </summary>
    public int MaxEvaluations
    {
      get { return Workers[0].MaxEvaluations; }
      set
        {
          foreach (Simplex s in Workers)
            s.MaxEvaluations = value;
        }
    }

    /// <summary>
    /// Maximum Relative Size of all workers
    /// </summary>
    public double MaxRelSize
    {
      get { return Workers[0].MinRelSize; }
      set
        {
          foreach (Simplex s in Workers)
            s.MinRelSize = value;
        }
    }

    /// <summary>
    /// Shrink Factor of all workers (default  = 0.5) 
    /// </summary>
    public double ShrinkFactor
    {
      get { return Workers[0].ShrinkFactor; }
      set
        {
          foreach (Simplex s in Workers)
            s.ShrinkFactor = value;
        }
    }

    /// <summary>
    /// Grow Factor of all workers (default = 2)
    /// </summary>
    public double GrowFactor
    {
      get { return Workers[0].GrowFactor; }
      set
        {
          foreach (Simplex s in Workers)
            s.GrowFactor = value;
        }
    }

    /// <summary>
    /// Forces Boundary Conditions for all workes (default = false)
    /// </summary>
    public bool ForceBoundaryConditions
    {
      get { return Workers[0].ForceBoundaryConditions; }
      set
        {
          foreach (Simplex s in Workers)
            s.ForceBoundaryConditions = value;
        }
    }

    /// <summary>
    /// Type of Boundary (ie how to handle parameters out of bounds) (default = Sticky)
    /// </summary>
    public Simplex.BoundaryConditionsType BoundaryCondition
    {
      get { return Workers[0].BoundaryCondition; }
      set
        {
          foreach (Simplex s in Workers)
            s.BoundaryCondition = value;
        }
    }

#endregion
  }
}

