
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace Logging
{

  /* ******************************************************************************************
   *                                          SimpleLogger
   * ******************************************************************************************
   * 
   * Summary:
   *      Ancient and simple log for diagnostic purposes.  Cut from Utility Library with 
   *      limited comments.  See library for updated commented version.
   * 
   * ******************************************************************************************

   */

  public class SimpleLogger
  {
    [Flags]
      public enum Priority
      {
        Info = 0x0003,
        Info1 = 0x0001,
        Info2 = 0x0002,

        Warning = 0x0030,
        Warning1 = 0x0010,
        Warning2 = 0x0020,

        Error = 0x0300,
        Error1 = 0x0100,
        Error2 = 0x0200,

        Fatal = 0x3000,            
        Fatal1 = 0x1000,
        Fatal2 = 0x2000,

        All = 0x3333,
        All1 = 0x1111,
        All2 = 0x2222            
      }

      /// <summary>
      /// Single log message
      /// </summary>
      public class LogMessage : EventArgs, IComparable<LogMessage>
      {
        public int MessagePriority;
        public StringBuilder MessageString;
        public DateTime MessageTime;

        /// <summary>
        /// Constructs a simple log message
        /// </summary>
        /// <param name="messagePriority">priority of message</param>
        /// <param name="message">info associated with message</param>
        public LogMessage(int messagePriority, string message)
        {
          MessagePriority = messagePriority;
          MessageString = new StringBuilder(message);
          MessageTime = DateTime.Now;
        }

        /// <summary>
        /// Constructs a complicated log message
        /// </summary>
        /// <param name="messagePriority">priority of message</param>
        /// <param name="message">info associated with message</param>
        /// <param name="msgArgs">optional arguments inserted into info</param>
        public LogMessage(int messagePriority, string message, params object[] msgArgs)
        {
          MessagePriority = messagePriority;
          MessageString = new StringBuilder();
          MessageString.AppendFormat(message, msgArgs);
          MessageTime = DateTime.Now;
        }

        public int CompareTo(LogMessage other)
        {
          return MessageTime.CompareTo(other.MessageTime);
        }


        /// <summary>
        /// Converts the message to a string
        /// </summary>
        /// <returns>yyyyMMdd HH:mm:ss fff [priority] text</returns>
        public override string ToString()
        {
          return MessageTime.ToString("HH:mm:ss fff")
            + "\t[" + MessagePriority.ToString() + "]\t" + MessageString;                
        }

        public string ToString(DateTime startTime)
        {
          return (MessageTime - startTime).ToString()
            + "\t[" + MessagePriority.ToString() + "]\t" + MessageString;
        }



      }

    /// <summary>
    /// Adds a message to the queue if priority is acceptable
    /// </summary>
    /// <param name="priority">priority of message</param>
    /// <param name="message">info associated with message</param>
    public void Add(int priority, string message)
    {
      if (priorityList.Contains(priority))
        {
          LogMessage msg = new LogMessage(priority, message);
          if (ConsoleOutput)
            Console.WriteLine(msg);
          log.Add(msg);

          if (BufferSize > 0 && log.Count >= BufferSize)
            this.PurgeLog();
        }
    }


    /// <summary>
    /// Adds a message to the queue if priority is acceptable
    /// </summary>
    /// <param name="priority">priority of message</param>
    /// <param name="message">info associated with message</param>
    /// <param name="messageArgs">optional text to be inserted</param>
    public void Add(int priority, string message, params object[] messageArgs)
    {
      if (priorityList.Contains(priority))
        {
          LogMessage msg = new LogMessage(priority, message, messageArgs);
          if (ConsoleOutput)
            Console.WriteLine(msg);
          log.Add(msg);

          if (BufferSize > 0 && log.Count >= BufferSize)
            this.PurgeLog();
        }
    }

    /// <summary>
    /// Adds a message via eventhandler
    /// </summary>
    /// <param name="logArgs">LogMessage containing info</param>
    public void Add(object sender, LogMessage logArgs)
    {
      if (priorityList.Contains(logArgs.MessagePriority))
        {
          if (ConsoleOutput)
            Console.WriteLine(logArgs);
          log.Add(logArgs);

          if (BufferSize > 0 && log.Count >= BufferSize)
            this.PurgeLog();
        }
    }

    /// <summary>
    /// Event handler for logging
    /// </summary>
    /// <param name="e">LogMessage to pass</param>
    public delegate void LogEventHandler(object sender, LogMessage e);

    private List<LogMessage> log;

    /// <summary>
    /// Purges the Log Queue to a file (appends if neccisary)
    /// </summary>
    public void PurgeLog()
    {
      StreamWriter fileOut = new StreamWriter(logPath, !firstPurge);

      if (firstPurge)
        fileOut.WriteLine("{0} log started at {1}", Name, 
                          startTime.ToString("yyyyMMdd HH:mm:ss"));

      foreach(LogMessage m in log)
        fileOut.WriteLine(m);

      fileOut.Close();
      log.Clear();
      firstPurge = false;
    }

    private bool firstPurge = true;

    /// <summary>
    /// file path to store log
    /// </summary>
    private string logPath;
    private DateTime startTime;

    /// <summary>
    /// Output messages to console if true
    /// </summary>
    public bool ConsoleOutput;

    /// <summary>
    /// Max size of log before autopurging (negative values turns off autopurging)
    /// </summary>
    public int BufferSize;

    /// <summary>
    /// Name of log
    /// </summary>
    public string Name;

    /// <summary>
    /// Priority of log
    /// </summary>
    public string LogPriority
    {
      set
        {
          priorityList = new List<int>();

          string[] splitValue = value.Split(',');

          for(int i = 0; i < splitValue.Length; i++)
            {
              if(splitValue[i].Contains("["))
                {
                  int start;
                  if(!int.TryParse(splitValue[i].Replace("[", ""), out start))
                    throw new SystemException("error parsing");

                  i++;

                  int end;
                  if(!int.TryParse(splitValue[i].Replace("]", ""), out end))
                    throw new SystemException("error parsing");

                  for(int j = start; j <= end; j++)
                    if(!priorityList.Contains(j))
                      priorityList.Add(j);                        
                }
              else
                {
                  int start;
                  if(!int.TryParse(splitValue[i], out start))
                    throw new SystemException("error parsing");

                  if(!priorityList.Contains(start))
                    priorityList.Add(start);
                }
            }
        }
    }

    List<int> priorityList;

    /// <summary>
    /// Creates a log using the current directory
    /// </summary>
    public SimpleLogger(string logName)
    {
      log = new List<LogMessage>();
      ConsoleOutput = false;
      LogPriority = "[0, 100]";
      Name = logName;
      logPath = Directory.GetCurrentDirectory() + @"\" + Name + " Log.txt";
      startTime = DateTime.Now;
      BufferSize = -1;
    }

    /// <summary>
    /// Creates a log using the given path
    /// </summary>
    /// <param name="Path">File path of log</param>
    public SimpleLogger(string logName, string directory)
    {
      log = new List<LogMessage>();
      ConsoleOutput = false;
      LogPriority = "[0, 100]";
      Name = logName;

      if (!Directory.Exists(directory))
        throw new System.ArgumentException("Directory does not exist");

      if (directory[directory.Length - 1] == '\\')
        logPath = directory + Name + " Log.txt";
      else
        logPath = directory + @"\" + Name + " Log.txt";

      startTime = DateTime.Now;
      BufferSize = -1;
    }

    /// <summary>
    /// Merges another log with this one
    /// </summary>
    /// <param name="logger">other logger to merge</param>
    public void Merge(SimpleLogger logger)
    {
      foreach(LogMessage msg in logger.log)
        if (priorityList.Contains(msg.MessagePriority))
          log.Add(msg);

      //log.AddRange(logger.log);
      log.Sort();

      if (BufferSize > 0 && log.Count >= BufferSize)
        this.PurgeLog();
    }

    /// <summary>
    /// Purge on Destruction
    /// </summary>
    ~SimpleLogger()
      {
        if (BufferSize > 0)
          this.PurgeLog();
      }
  }
}

