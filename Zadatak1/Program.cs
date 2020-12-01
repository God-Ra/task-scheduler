using System;
using System.Threading;
using System.Threading.Tasks;
using Zadatak1.SchedulerLibrary;

namespace Zadatak1.Demo
{
    class Program
    {
        private static Task CreateTask(int id)
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
        private static Task CreateTask(int id, SimpleTaskScheduler.ExecutionToken token)
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
        private static Task CreateTaskWithResourceLock(int id, SimpleTaskScheduler.ExecutionToken token, Object resource1, Object resource2)
        {
            Task t = new Task(() =>
            {
                int twoHundredMilliseconds = 200;
                for (int i = 0; i < 10; ++i)
                {
                    Task.Delay(twoHundredMilliseconds).Wait();
                    Console.WriteLine($"id: {id}, iteration: {i}");

                    if (i == 1)
                    {
                        Console.WriteLine("Task with id: " + id + " trying to get a resource");
                        int val = token.LockResource(resource1);
                        if (val == 0)
                        {
                            Console.WriteLine("Task with id: " + id + " succeeded getting resource ");
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
                    if (i == 3)
                    {
                        Console.WriteLine("Task with id: " + id + " trying to get a resource");
                        int val = token.LockResource(resource2);
                        if (val == 0)
                        {
                            Console.WriteLine("Task with id: " + id + " succeeded getting resource ");
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
            });

            return t;
        }

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
            });

            return t;
        }

        private static Task CreateTaskWithResourceLockAndUnlockResource(int id, SimpleTaskScheduler.ExecutionToken token, Object resource)
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
                    if (i == 7)
                    {
                        token.UnlockResource(resource);
                    }

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

        private static void DeadlockDetectionExample()
        {
            int numOfThreads = 2;
            SimpleTaskScheduler taskScheduler = SimpleTaskScheduler.CreatePreemptive(numOfThreads);
            SimpleTaskScheduler.ExecutionToken token1 = taskScheduler.GetExecutionToken();
            SimpleTaskScheduler.ExecutionToken token2 = taskScheduler.GetExecutionToken();
            SimpleTaskScheduler.ExecutionToken token3 = taskScheduler.GetExecutionToken();
            SimpleTaskScheduler.ExecutionToken token4 = taskScheduler.GetExecutionToken();
            Object resource1 = new object();
            Object resource2 = new object();
            Task t1 = CreateTaskWithResourceLock(id: 1, token: token1, resource1: resource1, resource2: resource2);
            Task t2 = CreateTaskWithResourceLock(id: 2, token: token2, resource1: resource2, resource2: resource1);
            Task t3 = CreateTaskWithResourceLock(id: 3, token: token3, resource1: resource1, resource2: resource2);
            Task t4 = CreateTaskWithResourceLock(id: 4, token: token4, resource1: resource2, resource2: resource1);
            taskScheduler.Register(t1, 5, 2500, token1);
            taskScheduler.Register(t2, 4, 2500, token2);
            taskScheduler.Register(t3, 3, 2500, token3);
            taskScheduler.Register(t4, 2, 2500, token4);

            t1.Start(taskScheduler);
            t2.Start(taskScheduler);
            t3.Start(taskScheduler);
            t4.Start(taskScheduler);
        }

        private static void PreemptingExample()
        {
            int numOfThreads = 2;
            SimpleTaskScheduler taskScheduler = SimpleTaskScheduler.CreatePreemptive(numOfThreads);
            SimpleTaskScheduler.ExecutionToken token1 = taskScheduler.GetExecutionToken();
            SimpleTaskScheduler.ExecutionToken token2 = taskScheduler.GetExecutionToken();
            SimpleTaskScheduler.ExecutionToken token3 = taskScheduler.GetExecutionToken();
            Task t1 = CreateTask(id: 1, token: token1);
            Task t2 = CreateTask(id: 2, token: token2);
            Task t3 = CreateTask(id: 3, token: token3);
            taskScheduler.Register(t1, 5, 2500, token1);
            taskScheduler.Register(t2, 4, 2500, token2);
            taskScheduler.Register(t3, 3, 2500, token3);

            t1.Start(taskScheduler);
            t2.Start(taskScheduler);
            Task.Delay(1000).Wait();
            t3.Start(taskScheduler);
        }

        /// <summary>
        /// Also shows how execution time is calculated for both threads
        /// </summary>
        private static void SingleThreadPreemptingExample()
        {
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
        }

        private static void SingleThreadPreemptingExampleNonPreemptingScheduler()
        {
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
        }

        private static void PreemptingExampleNonPreemptingSchedulerMultipleTasks()
        {
            int numOfThreads = 2;
            SimpleTaskScheduler taskScheduler = SimpleTaskScheduler.CreateNonPreemptive(numOfThreads);
            SimpleTaskScheduler.ExecutionToken token1 = taskScheduler.GetExecutionToken();
            SimpleTaskScheduler.ExecutionToken token2 = taskScheduler.GetExecutionToken();
            SimpleTaskScheduler.ExecutionToken token3 = taskScheduler.GetExecutionToken();
            SimpleTaskScheduler.ExecutionToken token4 = taskScheduler.GetExecutionToken();
            Task t1 = CreateTask(id: 1, token: token1);
            Task t2 = CreateTask(id: 2, token: token2);
            Task t3 = CreateTask(id: 3, token: token3);
            Task t4 = CreateTask(id: 4, token: token4);
            taskScheduler.Register(t1, 5, 2500, token1);
            taskScheduler.Register(t2, 4, 2500, token2);
            taskScheduler.Register(t3, 3, 2500, token3);
            taskScheduler.Register(t4, 2, 2500, token4);

            t1.Start(taskScheduler);
            t2.Start(taskScheduler);
            Task.Delay(1000).Wait();
            t3.Start(taskScheduler);
            t4.Start(taskScheduler);

            taskScheduler.FinishScheduling();
        }

        private static void PriorityInversionExample1()
        {
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
        }

        private static void PriorityInversionExample2()
        {
            int numOfThreads = 1;
            SimpleTaskScheduler taskScheduler = SimpleTaskScheduler.CreatePreemptive(numOfThreads);
            SimpleTaskScheduler.ExecutionToken token1 = taskScheduler.GetExecutionToken();
            SimpleTaskScheduler.ExecutionToken token2 = taskScheduler.GetExecutionToken();
            Object resource = new Object();
            Task t1 = CreateTaskWithResourceLockAndUnlockResource(id: 1, token: token1, resource);
            Task t2 = CreateTaskWithResourceLockAndUnlockResource(id: 2, token: token2, resource);
            taskScheduler.Register(t1, 5, 2500, token1);
            taskScheduler.Register(t2, 4, 2500, token2);

            t1.Start(taskScheduler);
            Task.Delay(1000).Wait();
            t2.Start(taskScheduler);
            taskScheduler.FinishScheduling();
        }

        private static void BaseClassExample()
        {
            int numOfThreads = 1;
            TaskScheduler taskScheduler = SimpleTaskScheduler.CreateNonPreemptive(numOfThreads);
            Task t = CreateTask(id: 1);
            t.Start(taskScheduler);

            ((SimpleTaskScheduler)taskScheduler).FinishScheduling();
        }

        static void Main(string[] args)
        {
            PriorityInversionExample2();
        }
    }
}
