using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace Zadatak1.SchedulerLibrary
{
    public class SimpleTaskScheduler : TaskScheduler
    {
        internal const bool IsVerbose = false;
        private readonly int MaxParallelTasks;
        private readonly bool IsPreemptive;

        ////////////////////////////////////////////////////////////////////////////////////////////////
        #region ExecutionToken

        /// <summary>
        /// In this class, we only do stuff that's related to communicating with the user, administrative stuff of the scheduler is
        /// done in other parts
        /// </summary>
        public class ExecutionToken
        {
            internal SimpleTaskScheduler Scheduler;

            /// <summary>
            /// Specifies whether the task registered with this token is paused.
            /// </summary>
            public bool IsPaused { get; private set; } = false;

            /// <summary>
            /// Specifies whether the task registered with this token is canceled.
            /// </summary>
            public bool IsCanceled { get; private set; } = false;

            /// <summary>
            /// Used for signalling to the scheduler that the task has stopped.
            /// </summary>
            public EventWaitHandle TaskPaused { get; private set; } = new EventWaitHandle(false, EventResetMode.AutoReset);

            /// <summary>
            /// Used for signalling by the scheduler that the task should continue.
            /// </summary>
            public EventWaitHandle TaskContinued { get; private set; } = new EventWaitHandle(false, EventResetMode.AutoReset);

            internal EventWaitHandle waitingOnResponse { get; private set; }
            internal bool deadlockOccurred { get; private set; } = false;
            internal bool isRegistered { get; private set; } = false;
            internal ExecutionToken(SimpleTaskScheduler scheduler)
            {
                this.Scheduler = scheduler;
            }

            internal void PauseNoBlock()
            {
                IsPaused = true;
            }

            internal void Pause()
            {
                IsPaused = true;
                TaskPaused.WaitOne();
            }

            internal void Continue()
            {
                IsPaused = false;
                TaskContinued.Set();
            }

            internal void Cancel()
            {
                IsCanceled = true;
            }

            /// <summary>
            /// Locks a resource specified by the caller. In case the resource is already taken, the task will block
            /// waiting for it. A special value is returned in case this particular invocation would've caused a deadlock.
            /// There is a chance that the task ends while waiting to lock a resource in which case another special value is
            /// returned. If a task already holds this resource, the locking will be successful.
            /// </summary>
            /// <param name="resource">Resource to be locked, should be non null.</param>
            /// <returns>2 in case deadlock would've occurred by this invocation. 1 In case the task ended
            ///         while waiting for the resource. 0 In case the acquiring has been successful and -1 in case
            ///         the token is not registered, or resource is null.</returns>
            public int LockResource(Object resource)
            {
                if (!isRegistered || resource == null)
                    return -1;

                EventWaitHandle waitingOnResponse = new EventWaitHandle(false, EventResetMode.AutoReset);
                this.waitingOnResponse = waitingOnResponse;
                Scheduler.eventQueue.Add(new TaskWantsToLockResourceEvent(Scheduler, this, waitingOnResponse, resource));
                if (IsCanceled)
                    return 1;
                if (IsPaused)
                    WaitHandle.SignalAndWait(TaskPaused, TaskContinued);

                waitingOnResponse.WaitOne();
                if (deadlockOccurred)
                {
                    SetDeadlockOccurredFalse();
                    return 2;
                }

                if (IsCanceled)
                    return 1;
                if (IsPaused)
                    WaitHandle.SignalAndWait(TaskPaused, TaskContinued);
                return 0;
            }

            /// <summary>
            /// Unlocks the resource specified by the caller. Does nothing in case the resource is not already locked. If
            /// the token is not registered or resource is null, the method returns a special value. It is possible
            /// for the task to finish execution while calling this method, in which case another special value is returned.
            /// </summary>
            /// <param name="resource">Resource to be unlocked, should be non null.</param>
            /// <returns>1 In case the task completed while waiting to unlock the resource. 0 in case the unlocking has 
            ///         been successful, or if a task was not locking this resource and -1 in case
            ///         the token is not registered, or a resource is null.</returns>
            public int UnlockResource(Object resource)
            {
                if (!isRegistered || resource == null)
                    return -1;
                EventWaitHandle waitingOnResponse = new EventWaitHandle(false, EventResetMode.AutoReset);
                this.waitingOnResponse = waitingOnResponse;
                Scheduler.eventQueue.Add(new TaskWantsToUnlockResourceEvent(Scheduler, this, waitingOnResponse, resource));
                if (IsCanceled)
                    return 1;
                if (IsPaused)
                    WaitHandle.SignalAndWait(TaskPaused, TaskContinued);

                waitingOnResponse.WaitOne();
                return 0;
            }

            internal void SetDeadlockOccurredFalse()
            {
                deadlockOccurred = false;
            }

            internal void SetDeadlockOccurredTrue()
            {
                deadlockOccurred = true;
            }

            internal void SetRegisteredTrue()
            {
                isRegistered = true;
            }

            internal void SetRegisteredFalse()
            {
                isRegistered = false;
            }
        }

        #endregion ExecutionToken

        /// <summary>
        /// Generates an execution token instance
        /// </summary>
        /// <returns>An execution token.</returns>
        public ExecutionToken GetExecutionToken() => new ExecutionToken(this);

        ////////////////////////////////////////////////////////////////////////////////////////////////
        #region Constructors
        /// <summary>
        /// Creates a simple task scheduler with the specified number of threads.
        /// </summary>
        /// <param name="maxParallelTasks">Maximum number of tasks able to run in parallel at one moment in time.</param>
        /// <param name="isPreemptive">Specifies whether scheduler is preemptive.</param>
        private SimpleTaskScheduler(int maxParallelTasks, bool isPreemptive)
        {
            MaxParallelTasks = maxParallelTasks;
            IsPreemptive = isPreemptive;
            schedulingThread = new Thread(new ThreadStart(SchedulingThreadExecution));
            if (!schedulingThread.IsAlive)
            {
                schedulingThread.Start();
            }
        }

        /// <summary>
        /// Creates a simple task scheduler object that uses preemptive priority scheduling.
        /// </summary>
        /// <param name="maxParallelTasks">Maximum number of tasks able to run in parallel at one moment in time.</param>
        /// <returns>A simple task scheduler object that uses preemptive priority scheduling.</returns>
        /// <exception cref="ArgumentOutOfRangeException">If number of maxParallelTasks is not positive</exception>
        public static SimpleTaskScheduler CreatePreemptive(int maxParallelTasks)
        {
            EnsureValidArguments(maxParallelTasks);

            return new SimpleTaskScheduler(maxParallelTasks: maxParallelTasks, isPreemptive: true);
        }

        /// <summary>
        /// Creates a simple task scheduler object that uses non-preemptive priority scheduling.
        /// </summary>
        /// <param name="maxParallelTasks">Maximum number of tasks able to run in parallel at one moment in time.</param>
        /// <returns>A simple task scheduler object that uses non-preemptive priority scheduling.</returns>
        /// <exception cref="ArgumentOutOfRangeException">If number of maxParallelTasks is not positive</exception>
        public static SimpleTaskScheduler CreateNonPreemptive(int maxParallelTasks)
        {
            EnsureValidArguments(maxParallelTasks);

            return new SimpleTaskScheduler(maxParallelTasks: maxParallelTasks, isPreemptive: false);
        }

        /// <summary>
        /// Ensures all arguments passed in factory method are valid. Throws an ArgumentOutOfRangeException in case
        /// max parallel tasks count is not positive.
        /// </summary>
        /// <param name="maxParallelTasks">Maximum number of parallel tasks</param>
        /// <exception cref="ArgumentOutOfRangeException">If number of maxParallelTasks is not positive</exception>
        private static void EnsureValidArguments(int maxParallelTasks)
        {
            if (maxParallelTasks <= 0)
                throw new ArgumentOutOfRangeException("Number of specified parallel tasks should be positive");
        }

        #endregion

        ////////////////////////////////////////////////////////////////////////////////////////////////
        #region Getters and setters
        /// <summary>
        /// Gets the number of tasks scheduler is able to run in parallel.
        /// </summary>
        /// <returns>An int representing the number of tasks scheduler is able to run in parallel</returns>
        public int GetMaxParallelTasks() { return MaxParallelTasks; }

        /// <summary>
        /// Checks whether scheduler is preemptive.
        /// </summary>
        /// <returns>True if scheduler is preemptive, false otherwise.</returns>
        public bool IsSchedulerPreemptive() { return IsPreemptive; }
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////////////////
        #region Public API

        /// <summary>
        /// Sets the task's priority, duration and execution token. Afterwards you can start the task.
        /// </summary>
        /// <param name="task">Task to be executed. The task object shouldn't be active(running) or previously registered. In case the task object was previously registered, you should wait for the task to finish befure queueing it again. Task has to be non null.</param>
        /// <param name="priority">Priority of the task, range 1-20 with 1 having biggest precedence and 20 the lowest.</param>
        /// <param name="durationInMilliseconds">Maximum duration of the task, task will be stopped after this durations elapses. Duration has to be positive. </param>
        /// <param name="executionToken">Execution token used for the task. Execution token shouldn't be mapped to any active, or previously registered non-completed task. Has to be non-null.</param>
        /// <returns>True if the task is registered correctly, false if any of the conditions are not fulfilled.</returns>
        public bool Register(Task task, int priority, int durationInMilliseconds, ExecutionToken executionToken)
        {
            if (priority < 1 || priority > 20
                || durationInMilliseconds <= 0
                || task == null
                || executionToken == null)
                return false;

            lock (resourcesLock)
            {
                if (taskToToken.ContainsKey(task) || tokenToTask.ContainsKey(executionToken))
                    return false;
                taskToToken.Add(task, executionToken);
                tokenToTask.Add(executionToken, task);

                TaskMetadata taskMetadata = new TaskMetadata(duration: durationInMilliseconds, priority: priority, taskState: TaskMetadata.TaskState.Paused);
                taskToMetadata.Add(task, taskMetadata);

                taskHoldingResources.Add(task, new List<Object>());
                executionToken.SetRegisteredTrue();

                return true;
            }
        }

        /// <summary>
        /// Retrieves the number of tasks which are currently executing.
        /// </summary>
        /// <returns>The number of tasks which are currently executing.</returns>
        public int CurrentlyExecutingTasksCount()
        {
            lock (resourcesLock)
            {
                return tasks.FindAll(t => taskToMetadata[t].IsExecuting()).Count;
            }
        }

        /// <summary>
        /// Checks whether the task is currently executing on scheduler threads.
        /// </summary>
        /// <param name="task">Task for which the check is made</param>
        /// <returns>True if task is currently executing, or blocked while executing, and false if it's paused or not scheduled.</returns>
        public bool IsTaskCurrentlyExecuting(Task task)
        {
            lock (resourcesLock)
            {
                if (!taskToMetadata.ContainsKey(task))
                    return false;

                return taskToMetadata[task].taskState == TaskMetadata.TaskState.BlockedExecuting
                        || taskToMetadata[task].taskState == TaskMetadata.TaskState.Executing;
            }
        }

        /// <summary>
        /// Stops the scheduler from working. Scheduler will wait for all tasks to finish execution and 
        /// for the internal event queue to empty before finishing.
        /// </summary>
        public void FinishScheduling()
        {
            Task.Factory.StartNew(() =>
            {
                int oneSecondDelay = 1000;
                bool isSchedulingFinished = false;
                while (!isSchedulingFinished)
                {
                    Task.Delay(oneSecondDelay).Wait();
                    if (eventQueue.Count == 0 && CurrentlyExecutingTasksCount() == 0)
                    {
                        Task.Delay(oneSecondDelay).Wait();
                        eventQueue.Add(new DummyFinishingEvent());
                        isSchedulingFinished = true;
                        this.IsSchedulingFinished = true;
                    }
                }
            });
        }

        #endregion

        ////////////////////////////////////////////////////////////////////////////////////////////////
        #region Overridden methods

        /// <summary>
        /// Retrieves all scheduled tasks in scheduler
        /// </summary>
        /// <returns>An IEnumerable of scheduled tasks</returns>
        protected override IEnumerable<Task> GetScheduledTasks()
        {
            return tasks.ToArray();
        }

        /// <summary>
        /// Queues task for execution. This method will be called when user starts his task. It generates
        /// a new AddTaskEvent which will add the task to memory.
        /// </summary>
        /// <param name="task">Task to be queued. Does nothing if task is null.</param>
        protected override void QueueTask(Task task)
        {
            if (task == null)
                return;
            lock (resourcesLock)
            {
                if (tasks.Exists(t => t == task))
                {
                    return;
                }
                else if (!taskToToken.ContainsKey(task))
                {
                    if (IsPreemptive)
                        return;
                    else
                        Register(task: task, priority: 10, durationInMilliseconds: 5000, executionToken: GetExecutionToken());
                }

                eventQueue.Add(new AddTaskEvent(scheduler: this, taskToBeAdded: task));
            }
        }

        /// <summary>
        /// It is not possible to execute a task on the same thread, so this method always returns false
        /// </summary>
        /// <returns>false</returns>
        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            return false;
        }

        #endregion

        ////////////////////////////////////////////////////////////////////////////////////////////////
        #region Private Methods and variables

        private readonly Thread schedulingThread;
        private BlockingCollection<BaseEvent> eventQueue = new BlockingCollection<BaseEvent>();
        private bool IsSchedulingFinished = false;

        /// <summary>
        /// Executes events which come to the event list, everything that touches tasks should go over this queue
        /// </summary>
        private void SchedulingThreadExecution()
        {
            foreach (BaseEvent baseEvent in eventQueue.GetConsumingEnumerable())
            {
                if (IsVerbose)
                {
                    if (baseEvent.GetType() == typeof(AddTaskEvent))
                        Console.WriteLine("Adding task");
                    if (baseEvent.GetType() == typeof(ScheduleTasksEvent))
                        Console.WriteLine("Schedule tasks event");
                    if (baseEvent.GetType() == typeof(CancelTaskIfExistsEvent))
                        Console.WriteLine("Cancel task if exists event");
                    if (baseEvent.GetType() == typeof(TaskCompletedEvent))
                        Console.WriteLine("Task Completed");
                    if (baseEvent.GetType() == typeof(TaskWantsToLockResourceEvent))
                        Console.WriteLine("Task wants to lock resource");
                    if (baseEvent.GetType() == typeof(TaskWantsToUnlockResourceEvent))
                        Console.WriteLine("Task wants to unlock resource");
                }
                if (IsSchedulingFinished)
                    break;
                baseEvent.Execute();
            }
        }

        /// <summary>
        /// These mappings will be deleted after the task has finished execution
        /// </summary>
        private readonly Dictionary<Task, ExecutionToken> taskToToken = new Dictionary<Task, ExecutionToken>();
        private readonly Dictionary<ExecutionToken, Task> tokenToTask = new Dictionary<ExecutionToken, Task>();
        private readonly Dictionary<Task, TaskMetadata> taskToMetadata = new Dictionary<Task, TaskMetadata>();

        private readonly Dictionary<Object, Task> resourceToTask = new Dictionary<Object, Task>();
        private readonly Dictionary<Object, List<(Task, EventWaitHandle)>> tasksWaitingOnResources = new Dictionary<Object, List<(Task, EventWaitHandle)>>();
        private readonly Dictionary<Task, List<Object>> taskHoldingResources = new Dictionary<Task, List<Object>>();

        private readonly GraphStructure Graph = new GraphStructure();

        private readonly List<Task> tasks = new List<Task>();

        private Object resourcesLock = new object();


        /// <summary>
        /// Adds task into memory
        /// </summary>
        /// <param name="task">Task to be added to memory.</param>
        private void AddTask(Task task)
        {
            lock (resourcesLock)
            {
                tasks.Add(task);
            }
        }

        /// <summary>
        /// Pauses task, this method is not synchronized. This method is not synchronized and should only
        /// be called from the event queue.
        /// </summary>
        /// <param name="task">Task to be paused</param>
        private void PauseTask(Task task)
        {
            ExecutionToken taskToken;
            TaskMetadata taskMetadata;
            lock (resourcesLock)
            {
                taskToken = taskToToken[task];
                taskMetadata = taskToMetadata[task];
            }

            if (!task.IsCompleted)
            {
                //In case task is not blocked while executing, call pause and wait for task to stop, Token.Pause() should never be called on task in BlockedExecuting state
                if (taskMetadata.taskState == TaskMetadata.TaskState.Executing)
                {
                    //Update data locally
                    taskToken.Pause();
                    taskMetadata.taskState = TaskMetadata.TaskState.Paused;
                }
                else if (taskMetadata.taskState == TaskMetadata.TaskState.BlockedExecuting)
                {
                    //Update data locally
                    taskMetadata.taskState = TaskMetadata.TaskState.BlockedPaused;
                }
                //-1 represents that task is not executing, taking time on any thread
                taskMetadata.ThreadId = -1;
                taskMetadata.ExecutionTime += (int)(DateTime.Now - taskMetadata.TimeTaskStartedExecution).TotalMilliseconds;
            }
        }

        /// <summary>
        /// Continues task, or starts a new one, on the specified thread. This method is not synchronized and should only
        /// be called from the event queue.
        /// </summary>
        /// <param name="task">Task to be continued or started</param>
        /// <param name="threadId">Thread on which the task will be continued or started</param>
        private void ContinueStartTask(Task task, int threadId)
        {
            //Start the minprioritynonexecuting task;
            //get token
            ExecutionToken taskToken;
            TaskMetadata taskMetadata;

            lock (resourcesLock)
            {
                taskToken = taskToToken[task];
                taskMetadata = taskToMetadata[task];
            }

            //Start or continue task, depending on whether task already had a first start
            taskMetadata.ThreadId = threadId;
            if (!taskMetadata.ExecutedFirstStart)
            {
                Task.Factory.StartNew(() => TryExecuteTask(task));
                taskMetadata.ExecutedFirstStart = true;
                taskMetadata.taskState = TaskMetadata.TaskState.Executing;
            }
            else
            {
                if (taskMetadata.taskState == TaskMetadata.TaskState.Paused)
                {
                    taskMetadata.taskState = TaskMetadata.TaskState.Executing;
                    taskToken.Continue();
                }
                else if (taskMetadata.taskState == TaskMetadata.TaskState.BlockedPaused)
                {
                    taskMetadata.taskState = TaskMetadata.TaskState.BlockedExecuting;
                }
            }

            taskMetadata.TimeTaskStartedExecution = DateTime.Now;
            DateTime localTimeTaskStartedExecutioon = taskMetadata.TimeTaskStartedExecution;

            //start callback max duration
            Task.Factory.StartNew(() =>
            {
                Task.Delay(taskMetadata.DurationInMilliseconds - taskMetadata.ExecutionTime).Wait();
                if (taskMetadata.IsExecuting() && localTimeTaskStartedExecutioon == taskMetadata.TimeTaskStartedExecution)
                    eventQueue.Add(new CancelTaskIfExistsEvent(scheduler: this, taskToBeCancelled: task));
            });
            //Start callback right after task finishes
            Task.Factory.StartNew(() =>
            {
                task.Wait();
                eventQueue.Add(new TaskCompletedEvent(scheduler: this, completedTask: task));
            });
        }

        /// <summary>
        /// Erases task from memory of scheduler
        /// </summary>
        /// <param name="t">Task for which memory is being erased</param>
        private void EraseTaskFromMemory(Task t)
        {
            lock (resourcesLock)
            {
                if (taskToMetadata[t].waitingOnResource != null)
                {
                    Graph.RemoveEdge(t, taskToMetadata[t].waitingOnResource);
                    tasksWaitingOnResources[taskToMetadata[t].waitingOnResource].Remove((t, taskToToken[t].waitingOnResponse));
                }
                taskToToken[t].SetRegisteredFalse();
                tokenToTask.Remove(taskToToken[t]);
                taskToMetadata.Remove(t);
                taskToToken.Remove(t);
                tasks.Remove(t);
                //Clean graph
                foreach (Object o in taskHoldingResources[t])
                {
                    Graph.RemoveEdge(o, t);
                }
                taskHoldingResources.Remove(t);
            }
        }

        /// <summary>
        /// Finds the task with minimum priority which is not executing.
        /// </summary>
        /// <returns>Task with minimum priority not currently executing.</returns>
        private Task FindMinimumPriorityNonExecutingTask()
        {
            lock (resourcesLock)
            {
                return tasks.Where(t => taskToMetadata[t].IsFreeForScheduling())
                            .OrderBy(t => taskToMetadata[t].VirtualPriority)
                            .FirstOrDefault();
            }
        }

        /// <summary>
        /// Finds the task with maximum priority which is currently executing.
        /// </summary>
        /// <returns>Task with maximum priority and currently executing.</returns>
        private Task FindMaximumPriorityExecutingTask()
        {
            lock (resourcesLock)
            {
                return tasks.Where(t => taskToMetadata[t].IsExecuting())
                               .OrderByDescending(t => taskToMetadata[t].VirtualPriority)
                               .FirstOrDefault();
            }
        }

        /// <summary>
        /// Compares priorites of two tasks and returns a typical compare result. In case you want to change
        /// how two tasks are compared, you only have to change this method.
        /// </summary>
        /// <param name="lhs">Left hand side Task</param>
        /// <param name="rhs">Right hand side Task</param>
        /// <returns>Typcal compare result</returns>
        private int ComparePriorities(Task lhs, Task rhs)
        {
            int lhsPriority;
            int rhsPriority;
            lock (resourcesLock)
            {
                lhsPriority = taskToMetadata[lhs].VirtualPriority;
                rhsPriority = taskToMetadata[rhs].VirtualPriority;
            }
            if (lhsPriority < rhsPriority)
                return -1;
            else if (lhsPriority == rhsPriority)
                return 0;
            else if (lhsPriority > rhsPriority)
                return 1;

            return -1;
        }

        /// <summary>
        /// Returns thread Id on which the task is currently running
        /// </summary>
        /// <returns>An int which represents id of the thread on which the task is currently running.</returns>
        private int GetThreadOfTask(Task task)
        {
            lock (resourcesLock)
            {
                return taskToMetadata[task].ThreadId;
            }
        }

        /// <summary>
        /// Returns -1 in case no free thread is found, otherwise returns the first free thread
        /// </summary>
        /// <returns>An int representing the id of the first free thread. Returns -1 in case no free thread is found.</returns>
        private int FindFirstFreeThread()
        {
            for (int i = 0; i < MaxParallelTasks; ++i)
            {
                if (IsFreeThread(i))
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// Checks if there are any tasks running on the specified thread
        /// </summary>
        /// <param name="threadId">Id of the thread for which the check is being made</param>
        /// <returns>True if there are no tasks running on specified thread, false otherwise.</returns>
        private bool IsFreeThread(int threadId)
        {
            lock (resourcesLock)
            {
                return tasks.Where(t => taskToMetadata[t].IsExecuting() && taskToMetadata[t].ThreadId == threadId).Count() > 0;
            }
        }

        /// <summary>
        /// Returns task with minimum priority out of those waiting on the specified resource.
        /// </summary>
        private (Task, EventWaitHandle) FindLowestPriorityTaskWaitingOnResource(Object resource)
        {
            lock (resourcesLock)
            {
                (Task, EventWaitHandle) minimumVirtualPriority = (null, null);
                List<(Task, EventWaitHandle)> taskList = tasksWaitingOnResources[resource];
                foreach ((Task task, EventWaitHandle handle) it in taskList)
                {
                    if (minimumVirtualPriority == (null, null))
                        minimumVirtualPriority = it;
                    else if (taskToMetadata[it.task].VirtualPriority < taskToMetadata[minimumVirtualPriority.Item1].VirtualPriority)
                        minimumVirtualPriority = it;
                }

                return minimumVirtualPriority;
            }
        }

        #endregion

        ////////////////////////////////////////////////////////////////////////////////////////////////
        #region Event Classes
        /// <summary>
        /// This class represents base event class for all events. For all of the subclass events, the execute method
        /// will be called once they get time on the queue, hence each subclass event should override the Execute method.
        /// </summary>
        private abstract class BaseEvent
        {
            internal abstract void Execute();

            internal BaseEvent()
            {

            }
        }

        /// <summary>
        /// This event adds the task to memory.
        /// </summary>
        private class AddTaskEvent : BaseEvent
        {
            private SimpleTaskScheduler scheduler;
            private Task taskToBeAdded;
            internal override void Execute()
            {
                scheduler.AddTask(taskToBeAdded);
                scheduler.eventQueue.Add(new ScheduleTasksEvent(scheduler));
            }

            internal AddTaskEvent(SimpleTaskScheduler scheduler, Task taskToBeAdded)
            {
                this.scheduler = scheduler;
                this.taskToBeAdded = taskToBeAdded;
            }
        }

        /// <summary>
        /// Event which does preemptive or non-preemptive scheduling, depending on the scheduler type.
        /// </summary>
        private class ScheduleTasksEvent : BaseEvent
        {
            private SimpleTaskScheduler scheduler;
            internal override void Execute()
            {
                int currentlyExecutingTasksCount = scheduler.CurrentlyExecutingTasksCount();

                Task minPriorityNonExecutingTask = scheduler.FindMinimumPriorityNonExecutingTask();

                if (minPriorityNonExecutingTask == null)
                    return;

                if (currentlyExecutingTasksCount < scheduler.MaxParallelTasks)
                {
                    //start min priority non executing task, we have a free thread
                    scheduler.ContinueStartTask(task: minPriorityNonExecutingTask, threadId: scheduler.FindFirstFreeThread());
                }
                else if (scheduler.IsPreemptive)
                {
                    //search for task which could be stopped
                    Task maxPriorityExecutingTask = scheduler.FindMaximumPriorityExecutingTask();

                    if (scheduler.ComparePriorities(minPriorityNonExecutingTask, maxPriorityExecutingTask) < 0)
                    {
                        int threadId = scheduler.GetThreadOfTask(maxPriorityExecutingTask);
                        scheduler.PauseTask(maxPriorityExecutingTask);
                        scheduler.ContinueStartTask(minPriorityNonExecutingTask, threadId);
                    }
                }
            }

            internal ScheduleTasksEvent(SimpleTaskScheduler scheduler)
            {
                this.scheduler = scheduler;
            }
        }

        /// <summary>
        /// This events cancels a task which uses up all of it's specified duration.
        /// </summary>
        private class CancelTaskIfExistsEvent : BaseEvent
        {
            private SimpleTaskScheduler scheduler;
            private Task taskToBeCancelled;
            internal override void Execute()
            {
                TaskMetadata taskToBeCancelledMetadata;
                ExecutionToken taskToBeCancelledToken;

                lock (scheduler.resourcesLock)
                {
                    if (scheduler.taskToToken.ContainsKey(taskToBeCancelled))
                    {
                        taskToBeCancelledMetadata = scheduler.taskToMetadata[taskToBeCancelled];
                        taskToBeCancelledToken = scheduler.taskToToken[taskToBeCancelled];

                        //If task is holding some resources, queue the unlock resources event and then cancel task if exists event again
                        //This will unlock the first resource before trying to delete the task again
                        if (scheduler.taskHoldingResources[taskToBeCancelled].Count() > 0)
                        {
                            scheduler.eventQueue.Add(new TaskWantsToUnlockResourceEvent(scheduler, taskToBeCancelledToken, taskToBeCancelledToken.waitingOnResponse, scheduler.taskHoldingResources[taskToBeCancelled][0]));
                            scheduler.eventQueue.Add(new CancelTaskIfExistsEvent(scheduler, taskToBeCancelled));
                            return;
                        }

                        taskToBeCancelledToken.Cancel();
                        if (taskToBeCancelledMetadata.taskState == TaskMetadata.TaskState.BlockedExecuting)
                        {
                            taskToBeCancelledToken.waitingOnResponse.Set();
                        }
                        taskToBeCancelled.Wait();


                        scheduler.EraseTaskFromMemory(taskToBeCancelled);
                        scheduler.eventQueue.Add(new ScheduleTasksEvent(scheduler));
                    }
                }
            }

            internal CancelTaskIfExistsEvent(SimpleTaskScheduler scheduler, Task taskToBeCancelled)
            {
                this.scheduler = scheduler;
                this.taskToBeCancelled = taskToBeCancelled;
            }
        }

        /// <summary>
        /// Event which represents that a task has completed
        /// </summary>
        private class TaskCompletedEvent : BaseEvent
        {
            private SimpleTaskScheduler scheduler;
            private Task completedTask;

            internal override void Execute()
            {
                TaskMetadata taskToBeCancelledMetadata;
                ExecutionToken taskToBeCancelledToken;

                lock (scheduler.resourcesLock)
                {
                    if (scheduler.taskToToken.ContainsKey(completedTask))
                    {
                        taskToBeCancelledMetadata = scheduler.taskToMetadata[completedTask];
                        taskToBeCancelledToken = scheduler.taskToToken[completedTask];
                        //If task is holding some resources, queue the unlock resources event and then cancel task if exists event again
                        //This will unlock the first resource before trying to delete the task again
                        if (scheduler.taskHoldingResources[completedTask].Count() > 0)
                        {
                            scheduler.eventQueue.Add(new TaskWantsToUnlockResourceEvent(scheduler, taskToBeCancelledToken, taskToBeCancelledToken.waitingOnResponse, scheduler.taskHoldingResources[completedTask][0]));
                            scheduler.eventQueue.Add(new CancelTaskIfExistsEvent(scheduler, completedTask));
                            return;
                        }

                        scheduler.EraseTaskFromMemory(completedTask);
                        scheduler.eventQueue.Add(new ScheduleTasksEvent(scheduler));
                    }
                }
            }

            internal TaskCompletedEvent(SimpleTaskScheduler scheduler, Task completedTask)
            {
                this.scheduler = scheduler;
                this.completedTask = completedTask;
            }
        }

        /// <summary>
        /// Event which represents that a task wants to lock some resource.
        /// </summary>
        private class TaskWantsToLockResourceEvent : BaseEvent
        {
            private SimpleTaskScheduler scheduler;
            private ExecutionToken taskToken;
            private EventWaitHandle taskNotifier;
            private Object resource;

            internal override void Execute()
            {
                TaskMetadata taskMetadata;
                Task task;
                lock (scheduler.resourcesLock)
                {
                    if (!scheduler.tokenToTask.ContainsKey(taskToken))
                        return;

                    task = scheduler.tokenToTask[taskToken];
                    taskMetadata = scheduler.taskToMetadata[task];
                }

                if (IsVerbose)
                {
                    Console.WriteLine("Task with hash " + task.GetHashCode() + " wants to lock resource " + resource.GetHashCode());
                }

                taskToken.SetDeadlockOccurredFalse();
                //Check if task is free, in case the resource is free grant access and notify user, otherwise do nothing and save that this task is waiting for this resource
                if (!scheduler.resourceToTask.ContainsKey(resource))
                {
                    scheduler.resourceToTask.Add(resource, task);
                    if (taskMetadata.taskState == TaskMetadata.TaskState.Executing)
                    {
                        taskNotifier.Set();
                    }
                    else if (taskMetadata.taskState == TaskMetadata.TaskState.BlockedExecuting)
                    {
                        taskNotifier.Set();
                        taskMetadata.taskState = TaskMetadata.TaskState.Executing;
                    }
                    else if (taskMetadata.taskState == TaskMetadata.TaskState.BlockedPaused)
                    {
                        taskToken.PauseNoBlock();
                        taskNotifier.Set();
                        taskMetadata.taskState = TaskMetadata.TaskState.Paused;
                    }

                    taskMetadata.VirtualPriority = TaskMetadata.MAXIMUM_PRIORITY;
                    scheduler.taskHoldingResources[task].Add(resource);
                    if (scheduler.Graph.EdgeExists(task, resource))
                        scheduler.Graph.RemoveEdge(task, resource);
                    scheduler.Graph.AddEdge(resource, task);
                }
                else
                {
                    //Check if resource is already taken by the same task, if so do nothing and just notify the caller
                    if (scheduler.resourceToTask[resource] == task)
                    {
                        taskNotifier.Set();
                    }
                    else
                    {
                        scheduler.Graph.AddEdge(task, resource);
                        if (scheduler.Graph.CheckCycle())
                        {
                            scheduler.Graph.RemoveEdge(task, resource);
                            taskToken.SetDeadlockOccurredTrue();
                            taskNotifier.Set();
                            return;
                        }
                        //Remember which tasks are waiting on which resources
                        if (!scheduler.tasksWaitingOnResources.ContainsKey(resource))
                        {
                            scheduler.tasksWaitingOnResources.Add(resource, new List<(Task, EventWaitHandle)>());
                        }

                        scheduler.tasksWaitingOnResources[resource].Add((task, taskNotifier));
                        taskMetadata.taskState = TaskMetadata.TaskState.BlockedExecuting;
                        taskMetadata.waitingOnResource = resource;
                    }
                }
            }

            internal TaskWantsToLockResourceEvent(SimpleTaskScheduler scheduler, ExecutionToken taskToken, EventWaitHandle taskNotifier, object resource)
            {
                this.scheduler = scheduler;
                this.taskToken = taskToken;
                this.taskNotifier = taskNotifier;
                this.resource = resource;
            }
        }

        /// <summary>
        /// Event which represents that a task wants to unlock some resource.
        /// </summary>
        private class TaskWantsToUnlockResourceEvent : BaseEvent
        {
            private SimpleTaskScheduler scheduler;
            private ExecutionToken taskToken;
            private EventWaitHandle taskNotifier;
            private Object resource;

            internal override void Execute()
            {
                TaskMetadata taskMetadata;
                Task task;
                lock (scheduler.resourcesLock)
                {
                    if (!scheduler.tokenToTask.ContainsKey(taskToken))
                        return;
                    task = scheduler.tokenToTask[taskToken];
                    taskMetadata = scheduler.taskToMetadata[task];
                }

                if (!scheduler.resourceToTask.ContainsKey(resource))
                    return;

                //In case task is not locking the resource, do nothing
                if (scheduler.resourceToTask[resource] != task)
                    return;
                else if (scheduler.resourceToTask[resource] == task)
                {
                    //Change state of task
                    taskMetadata.taskState = TaskMetadata.TaskState.Executing;
                    //In case task is locking the resource, unlock it and notify the caller
                    scheduler.resourceToTask.Remove(resource);
                    //Give resource to first task waiting for it
                    if (scheduler.tasksWaitingOnResources.ContainsKey(resource) && scheduler.tasksWaitingOnResources[resource].Count > 0)
                    {
                        (Task taskWaitingOnResource, EventWaitHandle taskWaitingOnResourceNotifier) = scheduler.FindLowestPriorityTaskWaitingOnResource(resource);
                        scheduler.tasksWaitingOnResources[resource].Remove((taskWaitingOnResource, taskWaitingOnResourceNotifier));
                        lock (scheduler.resourcesLock)
                        {
                            if (scheduler.taskToToken.ContainsKey(taskWaitingOnResource))
                                scheduler.eventQueue.Add(new TaskWantsToLockResourceEvent(scheduler, scheduler.taskToToken[taskWaitingOnResource], taskWaitingOnResourceNotifier, resource));
                        }
                    }
                    //Set priority back to normal
                    taskMetadata.VirtualPriority = taskMetadata.InitialPriority;
                    taskMetadata.waitingOnResource = null;

                    scheduler.Graph.RemoveEdge(resource, task);
                    scheduler.taskHoldingResources[task].Remove(resource);
                    //Notify our caller that he has released the resource successfully
                    taskNotifier.Set();
                    //Queue new event for scheduling, because the virtual priority is dropping
                    scheduler.eventQueue.Add(new ScheduleTasksEvent(scheduler));
                }
            }

            internal TaskWantsToUnlockResourceEvent(SimpleTaskScheduler scheduler, ExecutionToken taskToken, EventWaitHandle taskNotifier, object resource)
            {
                this.scheduler = scheduler;
                this.taskToken = taskToken;
                this.taskNotifier = taskNotifier;
                this.resource = resource;
            }
        }

        /// <summary>
        /// Event used for ending execution of the main thread
        /// </summary>
        private class DummyFinishingEvent : BaseEvent
        {
            internal override void Execute()
            {
            }

            internal DummyFinishingEvent()
            {
            }
        }
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////////////////
        #region Additional classes

        /// <summary>
        /// Represents metadata info for a task
        /// </summary>
        internal class TaskMetadata
        {
            internal const int MAXIMUM_PRIORITY = 1;
            internal const int MINIMUM_PRIORITY = 20;
            internal int DurationInMilliseconds { get; private set; }
            internal int InitialPriority { get; private set; }
            //A task will get this priority once it acquires a resource, VirtualPriority will be =1 (maximum)
            internal int VirtualPriority { get; set; }
            internal int ExecutionTime { get; set; }
            internal DateTime TimeTaskStartedExecution { get; set; }
            internal TaskState taskState { get; set; }
            internal int ThreadId { get; set; }
            internal bool ExecutedFirstStart { get; set; } = false;
            internal Object waitingOnResource { get; set; }

            internal enum TaskState
            {
                Executing,
                Paused,
                BlockedPaused,
                BlockedExecuting
            }

            public bool IsExecuting() => taskState == TaskState.Executing || taskState == TaskState.BlockedExecuting;

            public bool IsFreeForScheduling() => taskState == TaskState.Paused || taskState == TaskState.BlockedPaused;

            internal TaskMetadata(int duration, int priority, TaskState taskState)
            {
                this.DurationInMilliseconds = duration;
                this.InitialPriority = priority;
                this.VirtualPriority = priority;
                this.taskState = taskState;
                this.ExecutionTime = 0;
            }
        }

    }
    #endregion
}
