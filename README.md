# Simple Task Scheduler

- [Simple Task Scheduler](#simple-task-scheduler)
    - [Features](#features)
  - [Examples](#examples)
    - [Task with execution token](#task-with-execution-token)
    - [Basic scheduler usage](#basic-scheduler-usage)
    - [Preemptive scheduler usage](#preemptive-scheduler-usage)
    - [Non-preemptive scheduler usage](#non-preemptive-scheduler-usage)
    - [Task without an execution token](#task-without-an-execution-token)
    - [Using STS with the base class](#using-sts-with-the-base-class)
    - [Task locking a resource](#task-locking-a-resource)
    - [Priority inversion](#priority-inversion)
  - [Execution token](#execution-token)
    - [Overview](#overview)
    - [Methods](#methods)
      - [LockResource](#lockresource)
      - [UnlockResource](#unlockresource)
    - [Fields](#fields)
      - [IsPaused](#ispaused)
      - [IsCanceled](#iscanceled)
      - [TaskPaused](#taskpaused)
      - [TaskContinued](#taskcontinued)
  - [Simple Task Scheduler (STS)](#simple-task-scheduler-sts)
    - [Overview](#overview-1)
    - [Static methods](#static-methods)
      - [CreateNonPreemptive](#createnonpreemptive)
      - [CreatePreemptive](#createpreemptive)
    - [Methods](#methods-1)
      - [GetExecutionToken](#getexecutiontoken)
      - [Register](#register)
      - [CurrentlyExecutingTasksCount](#currentlyexecutingtaskscount)
      - [IsTaskCurentlyExecuting](#istaskcurentlyexecuting)
      - [FinishScheduling](#finishscheduling)
    - [Fields](#fields-1)
    - [Scheduling tasks](#scheduling-tasks)

With the Simple Task Scheduler (STS) you can preemptively and non-preemptively schedule tasks for execution.

### Features

- Specifying the number of threads used by the scheduler
- Protection against the Priority Inversion Problem
- Automatic deadlock detection and handling
- Specifying maximum duration of execution for each task
- Real time scheduling
- Synchronization between different tasks

## Examples
***
### Task with execution token
```c#
Task CreateTask(int id, SimpleTaskScheduler.ExecutionToken token)
{
    const int NUM_ITERATIONS = 10;
    int twoHundredMilliseconds = 200;
    Task t = new Task(() =>
    {
        for (int i = 0; i < NUM_ITERATIONS; ++i)
        {
            Task.Delay(twoHundredMilliseconds).Wait();
            Console.WriteLine($"id: {id}, iteration: {i}");

            if (token.IsCanceled)
                return;
            if (token.IsPaused)
            {
                EventWaitHandle.SignalAndWait(token.TaskPaused, token.TaskContinued);
            }
        }
    });

    return t;
}
```
This method can be used for creating tasks. It expects an id which is used for identification, and it also expects an ExecutionToken from which you can get information if the task is paused or if the task has completed. It is advisable to check if the task is canceled or paused every so often for best scheduler performance. Not more than 200ms should pass before each check.

***
### Basic scheduler usage
```c#
//Use one thread
int numOfThreads = 1;

//Initialize the scheduler
SimpleTaskScheduler taskScheduler = SimpleTaskScheduler.CreatePreemptive(numOfThreads);

//Get the execution token
SimpleTaskScheduler.ExecutionToken token = taskScheduler.GetExecutionToken();

//Create and register the task
Task t = CreateTask(id: 1, token: token);
taskScheduler.Register(t, 5, 2500, token);

//Start the task
t.Start(taskScheduler);

//Give a signal to the scheduler to finish
taskScheduler.FinishScheduling();
```
This is an example of basic usage of the scheduler. 

Initialization of the scheduler is done by calling the *CreatePreemptive* or *CreateNonPreemptive* factory methods and specifying the number of threads the scheduler should use.

Afterwards you have to get the execution token with which you will control the execution and which will provide you with execution information of your task (e.g. is it paused, completed), and some other methods. You should use this execution token in your task.

The method [CreateTask](#task-with-execution-token) is used for task creation. Afterwards, we register our task for execution. By registering the task you specify the priority and duration of your task. In this example, the priority(range 1-20) is set to 5 and the duration to 2.5 seconds. Tasks with lower priority values have higher priority.

The task is then started by calling the *Start(taskScheduler)* method.

We also have to stop the scheduler, and that is done by calling the *FinishScheduling()* method. Once it's called, a signal is given to the scheduler to stop execution. The scheduler will wait for all tasks to complete before finishing.

***
### Preemptive scheduler usage
```c#
int numOfThreads = 1;
SimpleTaskScheduler taskScheduler = SimpleTaskScheduler.CreatePreemptive(numOfThreads);

SimpleTaskScheduler.ExecutionToken token1 = taskScheduler.GetExecutionToken();
SimpleTaskScheduler.ExecutionToken token2 = taskScheduler.GetExecutionToken();

Task t1 = CreateTask(id: 1, token: token1);
Task t2 = CreateTask(id: 2, token: token2);

taskScheduler.Register(t1, 5, 2500, token1);
taskScheduler.Register(t2, 4, 2500, token2);

t1.Start(taskScheduler);
Task.Delay(1000).Wait();
t2.Start(taskScheduler);

taskScheduler.FinishScheduling();
```
In this example, usage of preemptive scheduling on a single thread is shown. There are examples with multiple threads in the Demo class, but a single thread is used here for simplicity. 

Two tasks are [created](#task-with-execution-token) and registered, t1 with priority 5, and t2 with priority 4. We can see that t2 has bigger priority than t1.

The scheduler first starts execution of t1. After a second, t2 is scheduled for execution. The scheduler recognizes this, and because t2 has bigger priority, it pauses t1 and starts execution of t2. When t2 completes execution, the scheduler continues executing t1.

***
### Non-preemptive scheduler usage
```c#
int numOfThreads = 1;
SimpleTaskScheduler taskScheduler = SimpleTaskScheduler.CreateNonPreemptive(numOfThreads);

SimpleTaskScheduler.ExecutionToken token1 = taskScheduler.GetExecutionToken();
SimpleTaskScheduler.ExecutionToken token2 = taskScheduler.GetExecutionToken();

Task t1 = CreateTask(id: 1, token: token1);
Task t2 = CreateTask(id: 2, token: token2);

taskScheduler.Register(t1, 5, 2500, token1);
taskScheduler.Register(t2, 4, 2500, token2);

t1.Start(taskScheduler);
Task.Delay(1000).Wait();
t2.Start(taskScheduler);

taskScheduler.FinishScheduling();
```
In this example, usage of non-preemptive scheduling on a single thread is shown. There are examples with multiple threads in the Demo class, but a single thread is used here for simplicity. 

Two tasks are [created](#task-with-execution-token) and registered, t1 with priority 5, and t2 with priority 4. We can see that t2 has bigger priority than t1.

The scheduler first starts execution of t1. After a second, t2 is scheduled for execution. The scheduler recognizes this, and even though t2 has bigger priority, it continues executing t1 until it is completed. Once t1 is completed, the scheduler starts execution of t2.

***
### Task without an execution token
```c#
Task CreateTask(int id)
{
    const int NUM_ITERATIONS = 10;
    int twoHundredMilliseconds = 200;
    Task t = new Task(() =>
    {
        for (int i = 0; i < NUM_ITERATIONS; ++i)
        {
            Task.Delay(twoHundredMilliseconds).Wait();
            Console.WriteLine($"id: {id}, iteration: {i}");
        }
    });

    return t;
}
```
This is an example of a task which could be used for execution. 
Since there is no cooperation with the scheduler it should not be used, as we can't stop it or pause it. Either way, it is shown for demonstration purposes.

***
### Using STS with the base class
```c#
int numOfThreads = 1;
TaskScheduler taskScheduler = SimpleTaskScheduler.CreateNonPreemptive(numOfThreads);
Task t = CreateTask(id: 1);
t.Start(taskScheduler);

((SimpleTaskScheduler)taskScheduler).FinishScheduling();
```

This is an example of using the base class for scheduling tasks. You can use the baseScheduler for all activities related to the non-preempting scheduler, but you can't use it for the preempting scheduler as when using the preempting scheduler you have to register the tasks first.

The default execution period is 5 seconds and the default priority is 10 so all the tasks have the same priority. You do need to cast the base class to STS and call the *FinishScheduling* method to finish execution.

***
### Task locking a resource
```c#
private static Task CreateTaskWithResourceLock(int id, SimpleTaskScheduler.ExecutionToken token, Object resource)
{
    const int NUM_ITERATIONS = 10;
    int twoHundredMilliseconds = 200;
    Task t = new Task(() =>
    {
        for (int i = 0; i < NUM_ITERATIONS; ++i)
        {
            Task.Delay(twoHundredMilliseconds).Wait();
            Console.WriteLine($"id: {id}, iteration: {i}");

            if (i == 1)
            {
                int val = token.LockResource(resource);
                if (val == 0)
                {
                    Console.WriteLine("Task with id: " + id + " succeeded getting the resource ");
                }
                else if (val == 1)
                {
                    Console.WriteLine("Task with id: " + id + " completed while requesting a resource ");
                    return;
                }
                else if (val == 2)
                {
                    Console.WriteLine("Task with id: " + id + " couldn't lock a resource, would've deadlocked ");
                }
            }

            if (token.IsCanceled)
                return;
            if (token.IsPaused)
            {
                EventWaitHandle.SignalAndWait(token.TaskPaused, token.TaskContinued);
            }
        }

        token.UnlockResource(resource);
    });

    return t;
}
```

Here, usage of resource locking is shown. A task can lock a resource by calling the *token.LockResource(resource)* method. Depending on which value this method returns, there are several possibilites. In case the task has finished execution while waiting for a resource, value 1 is returned. In case the task got the resource, 0 is returned and in case the task would've deadlocked, 2 is returned.

In case of a deadlock, you can try to lock the resource multiple times with the following snippet.

```c#
int val;
do
{
    val = token.LockResource();
} while(val == 2);
```

To combat the priority inversion problem, once some task locks a resource it gets assigned priority 1. In case the task does not release the resource and ends execution, the scheduler will release the resource automatically for the task.

***
### Priority inversion
```c#
int numOfThreads = 1;
SimpleTaskScheduler taskScheduler = SimpleTaskScheduler.CreatePreemptive(numOfThreads);

SimpleTaskScheduler.ExecutionToken token1 = taskScheduler.GetExecutionToken();
SimpleTaskScheduler.ExecutionToken token2 = taskScheduler.GetExecutionToken();

Object resource = new Object();
Task t1 = CreateTaskWithResourceLock(id: 1, token: token1, resource);
Task t2 = CreateTaskWithResourceLock(id: 2, token: token2, resource);

taskScheduler.Register(t1, 5, 2500, token1);
taskScheduler.Register(t2, 4, 2500, token2);

t1.Start(taskScheduler);
Task.Delay(1000).Wait();
t2.Start(taskScheduler);

taskScheduler.FinishScheduling();
```
This is an illustration of how a task behaves once it locks a resource. Once the task locks a resource, it's priority is set to 1 (maximum priority) and it's no longer possible to preempt that task until it releases all of its resources. This is called the Non-Preemption Protocol (NPP).

In this example, two tasks are created and registered, t1 with priority 5, and t2 with priority 4.

Before t2 is started, t1 acquires the lock of a resource and it's priority is set to maximum. Then, once t2 starts, scheduler notices that, but it will not start executing t2 since t1 is now a higher priority task. Once t1 finishes with its execution, scheduler starts executing t2.

## Execution token
### Overview
Execution token's main purpose is to provide a way for the task and scheduler to communicate. The scheduler can signal whether some task is paused or completed, and it does so over the execution token. A task can try to lock or unlock resources by calling methods from the execution token.

To get an execution token, you have to call the *taskScheduler.GetExecutionToken()* method, and afterwards you can use this execution token in your task. One more thing you have to do before scheduling the task is register the token. You can do that by calling the *taskScheduler.Register()* method. This is done so that the scheduler can map which task uses which token.
### Methods
***

#### **LockResource**
Locks a resource specified by the caller. In case the resource is already locked, the task will block waiting for it. A special value is returned in case this particular invocation would've caused a deadlock. There is a chance that the task ends while waiting to lock a resource in which case another special value is returned. If a task already holds this resource, the locking will be successful.

***Parameters***

*Object resource* - Resource to be locked, should be non-null.

***Return value***

Returns an int which has value 2 in case deadlock would've occurred by this invocation.
Returns 1 in case the task ended while waiting for the resource. Return value is 0 in case the acquiring has been successful and -1 in case the token is not registered, or resource is null.

***Remarks***

You can proceed to use the resource only if this method returns the value 0. If this method returns the value 1, you have to end the task upon returning from the method.

Once a task locks a resource, it's priority changes to 1 (maximum priority). It is advisable to free the resource by calling the [UnlockResource](#unlock-resource) method as soon as you don't need it so that the priority is lowered to the previous one.

In case you don't unlock the resource and the task ends, STS will unlock the resource automatically.

In case this resource was already locked by the same task, access will be granted (Reentrant Synchronization).

***Example usage***

Standard usage in conjuction with [UnlockResource](#unlock-resource)
```c#
Object resource = new Object();
int returnValue = token.LockResource(resource);
if (returnValue == 0)
{
    //Use the resource
    int unlockReturnValue = token.UnlockResource(resource);
    if (unlockReturnValue == 1)
    {
        cleanData();
        return;
    }
}
else if (returnValue == 1)
{
    //Task is being stopped
    cleanData();
    return;
}
```

Multiple tries because of a deadlock situation occurring.
```c#
int resource = 2;
int returnValue = -1;
do
{
    returnValue = token.LockResource();
} while (returnValue == 2);
if (returnValue == 0)
{
    //Use the resource
    int unlockReturnValue = token.UnlockResource(resource);
    if (unlockReturnValue == 1)
    {
        cleanData();
        return;
    }
}
else if (returnValue == 1)
{
    cleanData();
    return;
}
```
***
#### **UnlockResource**
Unlocks the resource specified by the caller. Does nothing in case the resource is not already locked. If the token is not registered or resource is null, the method returns a special value. There is a chance that the task ends while waiting to unlock a resource in which case another special value is returned.

***Parameters***

*Object resource* - Resource to be unlocked, should be non null.

***Return value***

Returns 1 in case the task completed while waiting to unlock the resource. Return value is 0 in case the unlocking has been successful, or if a task was not locking this resource and -1 in case the token is not registered, or a resource is null.

***Remarks***

If this method returns the value 1, you have to end the task upon returning from the method.

In case you don't unlock a resource and the task ends, STS will unlock the resource automatically.

***Example usage***

Standard usage in conjuction with [LockResource](#lockresource)
```c#
Object resource = new Object();
int returnValue = token.LockResource(resource);
if (returnValue == 0)
{
    //Use the resource
    int unlockReturnValue = token.UnlockResource(resource);
    if (unlockReturnValue == 1)
    {
        cleanData();
        return;
    }
}
else if (returnValue == 1)
{
    //Task is being stopped
    cleanData();
    return;
}
```
### Fields
***

#### **IsPaused**
*type* - bool

Specifies whether the task registered with this token is paused. The executing task should check if this value is true every ~200ms for good performance of the scheduler. Once the scheduler signals that this task should be paused, it will wait for the task to actually pause.

Once the task has paused, it signals that to the scheduler and waits for the scheduler to signal the task that it can continue. This is done by using the [TaskContinued](#taskcontinued) and [TaskPaused](#taskpaused) fields and calling the static SignalAndWait method of class EventWaitHandle.

***Example usage***

Standard usage
```c#
if (token.IsPaused)
{
    EventWaitHandle.SignalAndWait(token.TaskPaused, token.TaskContinued);
}
```

Standard usage in a real task
```c#
Task t = new Task(() =>
{
    for (int i = 0; i < NUM_ITERATIONS; ++i)
    {
        Task.Delay(twoHundredMilliseconds).Wait();
        Console.WriteLine($"id: {id}, iteration: {i}");

        if (token.IsCanceled)
            return;
        if (token.IsPaused)
        {
            EventWaitHandle.SignalAndWait(token.TaskPaused, token.TaskContinued);
        }
    }
});
```

***
#### **IsCanceled**
*type* - bool

Specifies whether the task registered with this token is canceled. The executing task should check if this value is true every ~200ms for good performance of the scheduler. Once the scheduler signals that this task should be canceled, it will wait for the task to actually cancel it's execution.

Once the task is cancelled, the scheduler recognizes that automatically.

***Example usage***

Standard usage
```c#
if (token.IsCanceled)
{
    clearData();
    terminate();
}
```

Standard usage in a real task
```c#
Task t = new Task(() =>
{
    for (int i = 0; i < NUM_ITERATIONS; ++i)
    {
        Task.Delay(twoHundredMilliseconds).Wait();
        Console.WriteLine($"id: {id}, iteration: {i}");

        if (token.IsCanceled)
            return;
        if (token.IsPaused)
        {
            EventWaitHandle.SignalAndWait(token.TaskPaused, token.TaskContinued);
        }
    }
});
```

***
#### **TaskPaused**
*type* - EventWaitHandle

Used for signalling to the scheduler that the task has stopped. Used in conjuction with the [TaskContinued](#taskcontinued) and [IsPaused](#ispaused) fields.

***Example usage***

Standard usage
```c#
if (token.IsPaused)
{
    EventWaitHandle.SignalAndWait(token.TaskPaused, token.TaskContinued);
}
```

Standard usage in a real task
```c#
Task t = new Task(() =>
{
    for (int i = 0; i < NUM_ITERATIONS; ++i)
    {
        Task.Delay(twoHundredMilliseconds).Wait();
        Console.WriteLine($"id: {id}, iteration: {i}");

        if (token.IsCanceled)
            return;
        if (token.IsPaused)
        {
            EventWaitHandle.SignalAndWait(token.TaskPaused, token.TaskContinued);
        }
    }
});
```
***
#### **TaskContinued**
*type* - EventWaitHandle

Used for signalling by the scheduler that the task should continue. Used in conjuction with the [TaskPaused](#taskpaused) and [IsPaused](#ispaused) fields.

***Example usage***

Standard usage
```c#
if (token.IsPaused)
{
    EventWaitHandle.SignalAndWait(token.TaskPaused, token.TaskContinued);
}
```

Standard usage in a real task
```c#
Task t = new Task(() =>
{
    for (int i = 0; i < NUM_ITERATIONS; ++i)
    {
        Task.Delay(twoHundredMilliseconds).Wait();
        Console.WriteLine($"id: {id}, iteration: {i}");

        if (token.IsCanceled)
            return;
        if (token.IsPaused)
        {
            EventWaitHandle.SignalAndWait(token.TaskPaused, token.TaskContinued);
        }
    }
});
```

## Simple Task Scheduler (STS)
### Overview
STS is a priority-based task scheduler which can be preemptive or non-preemptive. [Features](#features) are listed at the start of the document. It is best used for a small number of tasks, preferrably under 10.

You can also specify the maximum execution duration of the task, and STS will only count in the time that the task has spent executing. The time while the task is paused will not be counted in the duration.

### Static methods
***
#### **CreateNonPreemptive**
Creates a Simple Task Scheduler object that uses non-preemptive priority scheduling.

***Parameters***

*int maxParallelTasks* - Represents the maximum number of tasks able to run in parallel at one moment in time.

***Return value***

Returns a Simple Task Scheduler object that uses non-preemptive priority scheduling.

***Exceptions***

*ArgumentOutOfRangeException* - in case number of maxParallelTasks is not positive

***Remarks***

This is a factory method used for creating a Simple Task Scheduler instance which uses non-preemptive scheduling.

This type of scheduler can be used by the base TaskScheduler class in which no task registration is needed. In this case, the task's default priority is set to 10 and maximum duration is set to 5 seconds.

***Example usage***

Standard usage for creating a scheduler
```c#
int numOfThreads = 1;
SimpleTaskScheduler taskScheduler = SimpleTaskScheduler.CreateNonPreemptive(numOfThreads);

SimpleTaskScheduler.ExecutionToken token = taskScheduler.GetExecutionToken();
Task t = CreateTask(id: 1, token: token);
taskScheduler.Register(t, 5, 2500, token);

t.Start(taskScheduler);

taskScheduler.FinishScheduling();
```
***
#### **CreatePreemptive**
Creates a Simple Task Scheduler object that uses preemptive priority scheduling.

***Parameters***

*int maxParallelTasks* - Represents the maximum number of tasks able to run in parallel at one moment in time.

***Return value***

Returns a Simple Task Scheduler object that uses preemptive priority scheduling.

***Exceptions***

*ArgumentOutOfRangeException* - in case number of maxParallelTasks is not positive

***Remarks***

This is a factory method used for creating a Simple Task Scheduler instance.

***Example usage***

Standard usage for creating a scheduler
```c#
int numOfThreads = 1;
SimpleTaskScheduler taskScheduler = SimpleTaskScheduler.CreatePreemptive(numOfThreads);

SimpleTaskScheduler.ExecutionToken token = taskScheduler.GetExecutionToken();
Task t = CreateTask(id: 1, token: token);
taskScheduler.Register(t, 5, 2500, token);

t.Start(taskScheduler);

taskScheduler.FinishScheduling();
```

### Methods
***
#### **GetExecutionToken**
Generates an execution token instance which is used for communication between the scheduler and the task. You can find more information about the execution token [here](#execution-token).

***Parameters***

None

***Return value***

Returns an execution token instance.

***Remarks***

This method is used for generating an execution token. Once an instance is generated, it can be used in the function of a task for communication between the task and the scheduler.

***Example usage***

Standard usage when scheduling a task for execution
```c#
int numOfThreads = 1;
SimpleTaskScheduler taskScheduler = SimpleTaskScheduler.CreateNonPreemptive(numOfThreads);

SimpleTaskScheduler.ExecutionToken token = taskScheduler.GetExecutionToken();
Task t = CreateTask(id: 1, token: token);
taskScheduler.Register(t, 5, 2500, token);

t.Start(taskScheduler);

taskScheduler.FinishScheduling();
```

***
#### **Register**
Sets the task's priority, duration and execution token. Afterwards you can start the task.

***Parameters***

*Task task* - The task which will be executed. The task object shouldn't be active(running) or previously registered. In case the task object was previously registered, you should wait for the task to finish befure queueing it again. The task has to be non null.

*int priority* - Represents the priority of the task. Range of priorities is 1-20 with 1 having the biggest precedence and 20 the lowest.

*int durationIinMilliseconds* - Represents the maximum duration for which the task will be allowed to execute. The task will be stopped after this durations elapses. The duration has to be positive.

*ExecutionToken executionToken* - Represents the execution token which was used for the task. The execution token shouldn't be mapped to any active, or previously registered non-completed task. The token has to be non-null.

***Return value***

Returns a boolean value. The boolen value is true if the task was registered correctly, and false if any of the parameter conditions were not fulfilled.

***Remarks***

This method is used for setting the priority, duration and execution token of the task. You must call this method before scheduling a task when using a preemptive Simple Task Scheduler, but you don't have to when using a non-preemptive Simple Task Scheduler.

***Example usage***

Standard usage when scheduling a task for execution
```c#
int numOfThreads = 1;
SimpleTaskScheduler taskScheduler = SimpleTaskScheduler.CreateNonPreemptive(numOfThreads);

SimpleTaskScheduler.ExecutionToken token = taskScheduler.GetExecutionToken();
Task t = CreateTask(id: 1, token: token);

if (taskScheduler.Register(t, 5, 2500, token))
{
    Console.WriteLine("Successfully registered a task!");
    t.Start(taskScheduler);
}
else
{
    Console.WriteLine("A problem occurred while registering a task!");
}

taskScheduler.FinishScheduling();
```
***
#### **CurrentlyExecutingTasksCount**
Retrieves the number of tasks which are currently executing.

***Parameters***

None

***Return value***

Returns an int value which represents the number of tasks which are currently executing.

***Remarks***

None

***Example usage***

Checking if a task has started execution
```c#
int numOfThreads = 1;
SimpleTaskScheduler taskScheduler = SimpleTaskScheduler.CreateNonPreemptive(numOfThreads);

SimpleTaskScheduler.ExecutionToken token = taskScheduler.GetExecutionToken();
Task t = CreateTask(id: 1, token: token);

taskScheduler.Register(t, 5, 2500, token);
t.Start(taskScheduler);

Task.Delay(500).Wait();
if (taskScheduler.CurrentlyExecutingTasksCount() == 1)
{
    Console.WriteLine("Successfully started a task!");
}

taskScheduler.FinishScheduling();
```
***
#### **IsTaskCurentlyExecuting**
Checks whether the task is currently executing on scheduler threads.

***Parameters***

*Task task* - Represents a task for which the check is being made

***Return value***

Returns a boolean value. The returned value is true if the task is currently executing, or blocked while executing, and false if it's paused or not scheduled.

***Remarks***

None

***Example usage***

Checking if a task has started execution
```c#
int numOfThreads = 1;
SimpleTaskScheduler taskScheduler = SimpleTaskScheduler.CreateNonPreemptive(numOfThreads);

SimpleTaskScheduler.ExecutionToken token = taskScheduler.GetExecutionToken();
Task t = CreateTask(id: 1, token: token);

taskScheduler.Register(t, 5, 2500, token);
t.Start(taskScheduler);

Task.Delay(500).Wait();
if (taskScheduler.IsTaskCurrentlyExecuting(t))
{
    Console.WriteLine("Successfully started a task!");
}

taskScheduler.FinishScheduling();
```
***
#### **FinishScheduling**
Stops the scheduler from working. Scheduler will wait for all tasks to finish execution and for the internal event queue to empty before finishing.

***Parameters***

None

***Return value***

None

***Remarks***

None

***Example usage***

Standard usage when scheduling a task for execution
```c#
int numOfThreads = 1;
SimpleTaskScheduler taskScheduler = SimpleTaskScheduler.CreateNonPreemptive(numOfThreads);

SimpleTaskScheduler.ExecutionToken token = taskScheduler.GetExecutionToken();
Task t = CreateTask(id: 1, token: token);

taskScheduler.Register(t, 5, 2500, token);
t.Start(taskScheduler);

Task.Delay(500).Wait();
if (taskScheduler.IsTaskCurrentlyExecuting(t))
{
    Console.WriteLine("Successfully started a task!");
}

taskScheduler.FinishScheduling();
```
***
### Fields
None
***
### Scheduling tasks
You should only schedule tasks using the *task.Start(SimpleTaskScheduler)* method. It is not guaranteed that the scheduler will work correctly by using any other methods for scheduling.

The only way you can communicate with the scheduler is over the execution token, and no other ways of communication are available.

[Here](#basic-scheduler-usage) you can find detailed information on how you should start the scheduler, schedule tasks and terminate the scheduler execution.
