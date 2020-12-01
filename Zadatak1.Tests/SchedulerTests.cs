using Microsoft.VisualStudio.TestTools.UnitTesting;
using Zadatak1.SchedulerLibrary;
using System;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;

namespace Zadatak1.Tests
{
    [TestClass]
    public class SchedulerTests
    {
        [TestMethod]
        public void TestCreationOfPreemptiveSimpleTaskSchedulerWithThreeThreads()
        {
            int numberOfThreads = 3;
            SimpleTaskScheduler st = SimpleTaskScheduler.CreatePreemptive(numberOfThreads);

            Assert.AreEqual(numberOfThreads, st.GetMaxParallelTasks());
            Assert.AreEqual(true, st.IsSchedulerPreemptive());
        }

        [TestMethod]
        public void TestCreationOfNonPreemptiveSimpleTaskSchedulerWithTwoThreads()
        {
            int numberOfThreads = 2;
            SimpleTaskScheduler st = SimpleTaskScheduler.CreateNonPreemptive(numberOfThreads);

            Assert.AreEqual(numberOfThreads, st.GetMaxParallelTasks());
            Assert.AreEqual(false, st.IsSchedulerPreemptive());
        }

        [TestMethod]
        public void TestCreationOfNonPreemptiveSimpleTaskSchedulerWithZeroThreads()
        {
            int numberOfThreads = 0;

            try
            {
                SimpleTaskScheduler st = SimpleTaskScheduler.CreateNonPreemptive(numberOfThreads);
                Assert.Fail();
            }
            catch (ArgumentOutOfRangeException ex) { }
        }

        [TestMethod]
        public void TestCreationOfPreemptiveSimpleTaskSchedulerWithMinusThreeThreads()
        {
            int numberOfThreads = -3;

            try
            {
                SimpleTaskScheduler st = SimpleTaskScheduler.CreatePreemptive(numberOfThreads);
                Assert.Fail();
            }
            catch (ArgumentOutOfRangeException ex) { }
        }

        private static Task CreateTask(int id, SimpleTaskScheduler.ExecutionToken token)
        {
            Task t = new Task(() =>
            {
                int twoHundredMilliseconds = 200;
                for (int i = 0; i < 10; ++i)
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

        private static Task CreateTaskWithResourceLock(int id, SimpleTaskScheduler.ExecutionToken token, Object resource)
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
                        token.LockResource(resource);
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

        [TestMethod]
        public void TestTwoTasksWithNoResourceLocking()
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
            Task.Delay(500).Wait();
            t2.Start(taskScheduler);
            Assert.AreEqual(taskScheduler.CurrentlyExecutingTasksCount(), 1);
        }

        [TestMethod]
        public void TestTwoTasksWithOneResourceLockingAndGettingMaxPriority()
        {
            int numOfThreads = 1;
            SimpleTaskScheduler taskScheduler = SimpleTaskScheduler.CreatePreemptive(numOfThreads);
            SimpleTaskScheduler.ExecutionToken token1 = taskScheduler.GetExecutionToken();
            SimpleTaskScheduler.ExecutionToken token2 = taskScheduler.GetExecutionToken();
            Task t1 = CreateTaskWithResourceLock(id: 1, token: token1, resource: new object()); ;
            Task t2 = CreateTask(id: 2, token: token2);
            taskScheduler.Register(t1, 5, 2500, token1);
            taskScheduler.Register(t2, 4, 2500, token2);

            t1.Start(taskScheduler);
            Task.Delay(500).Wait();
            t2.Start(taskScheduler);
            Task.Delay(2000).Wait();

            Assert.IsTrue(taskScheduler.IsTaskCurrentlyExecuting(t2));
        }

        [TestMethod]
        public void TestTwoTasksPreemptingEachOtherWithSameResource()
        {
            int numOfThreads = 2;
            SimpleTaskScheduler taskScheduler = SimpleTaskScheduler.CreatePreemptive(numOfThreads);
            SimpleTaskScheduler.ExecutionToken token1 = taskScheduler.GetExecutionToken();
            SimpleTaskScheduler.ExecutionToken token2 = taskScheduler.GetExecutionToken();
            Object resource = new object();
            Task t1 = CreateTaskWithResourceLock(id: 1, token: token1, resource: resource);
            Task t2 = CreateTaskWithResourceLock(id: 2, token: token2, resource: resource);
            taskScheduler.Register(t1, 5, 2500, token1);
            taskScheduler.Register(t2, 4, 2500, token2);

            t1.Start(taskScheduler);
            Task.Delay(500).Wait();
            t2.Start(taskScheduler);
            Task.Delay(5000).Wait();
            Assert.AreEqual(taskScheduler.CurrentlyExecutingTasksCount(), 0);
        }

        [TestMethod]
        public void TestThreeTasksOnTwoThreadsPreemptingEachOther()
        {
            int numOfThreads = 2;
            SimpleTaskScheduler taskScheduler = SimpleTaskScheduler.CreatePreemptive(numOfThreads);
            SimpleTaskScheduler.ExecutionToken token1 = taskScheduler.GetExecutionToken();
            SimpleTaskScheduler.ExecutionToken token2 = taskScheduler.GetExecutionToken();
            SimpleTaskScheduler.ExecutionToken token3 = taskScheduler.GetExecutionToken();
            Object resource = new object();
            Task t1 = CreateTaskWithResourceLock(id: 1, token: token1, resource: resource);
            Task t2 = CreateTaskWithResourceLock(id: 2, token: token2, resource: resource);
            Task t3 = CreateTask(id: 3, token: token3);
            taskScheduler.Register(t1, 5, 2500, token1);
            taskScheduler.Register(t2, 4, 2500, token2);
            taskScheduler.Register(t3, 3, 2500, token3);

            t1.Start(taskScheduler);
            Task.Delay(500).Wait();
            t2.Start(taskScheduler);
            t3.Start(taskScheduler);

            Task.Delay(5000).Wait();
            Assert.AreEqual(taskScheduler.CurrentlyExecutingTasksCount(), 0);
        }

        /// <summary>
        /// The important part here is that the scheduled tasks finish
        /// </summary>
        [TestMethod]
        public void Test10TasksOn5ThreadsPreemptingEachOther()
        {
            int numOfThreads = 5;
            SimpleTaskScheduler taskScheduler = SimpleTaskScheduler.CreatePreemptive(numOfThreads);
            SimpleTaskScheduler.ExecutionToken token1 = taskScheduler.GetExecutionToken();
            SimpleTaskScheduler.ExecutionToken token2 = taskScheduler.GetExecutionToken();
            SimpleTaskScheduler.ExecutionToken token3 = taskScheduler.GetExecutionToken();
            SimpleTaskScheduler.ExecutionToken token4 = taskScheduler.GetExecutionToken();
            SimpleTaskScheduler.ExecutionToken token5 = taskScheduler.GetExecutionToken();
            SimpleTaskScheduler.ExecutionToken token6 = taskScheduler.GetExecutionToken();
            SimpleTaskScheduler.ExecutionToken token7 = taskScheduler.GetExecutionToken();
            SimpleTaskScheduler.ExecutionToken token8 = taskScheduler.GetExecutionToken();
            SimpleTaskScheduler.ExecutionToken token9 = taskScheduler.GetExecutionToken();
            SimpleTaskScheduler.ExecutionToken token10 = taskScheduler.GetExecutionToken();
            Object resource = new object();
            Task t1 = CreateTaskWithResourceLock(id: 1, token: token1, resource: resource);
            Task t2 = CreateTaskWithResourceLock(id: 2, token: token2, resource: resource);
            Task t3 = CreateTaskWithResourceLock(id: 3, token: token3, resource: resource);
            Task t4 = CreateTaskWithResourceLock(id: 4, token: token4, resource: resource);
            Task t5 = CreateTaskWithResourceLock(id: 5, token: token5, resource: resource);
            Task t6 = CreateTaskWithResourceLock(id: 6, token: token6, resource: resource);
            Task t7 = CreateTaskWithResourceLock(id: 7, token: token7, resource: resource);
            Task t8 = CreateTaskWithResourceLock(id: 8, token: token8, resource: resource);
            Task t9 = CreateTaskWithResourceLock(id: 9, token: token9, resource: resource);
            Task t10 = CreateTaskWithResourceLock(id: 10, token: token10, resource: resource);
            taskScheduler.Register(t1, 1, 2500, token1);
            taskScheduler.Register(t2, 2, 2500, token2);
            taskScheduler.Register(t3, 3, 2500, token3);
            taskScheduler.Register(t4, 4, 2500, token4);
            taskScheduler.Register(t5, 5, 2500, token5);
            taskScheduler.Register(t6, 6, 2500, token6);
            taskScheduler.Register(t7, 7, 2500, token7);
            taskScheduler.Register(t8, 8, 2500, token8);
            taskScheduler.Register(t9, 9, 2500, token9);
            taskScheduler.Register(t10, 10, 2500, token10);

            t1.Start(taskScheduler);
            t2.Start(taskScheduler);
            t3.Start(taskScheduler);
            t4.Start(taskScheduler);
            t5.Start(taskScheduler);
            t6.Start(taskScheduler);
            t7.Start(taskScheduler);
            t8.Start(taskScheduler);
            t9.Start(taskScheduler);
            t10.Start(taskScheduler);
            Task.Delay(15000).Wait();
            Assert.IsTrue(t1.IsCompleted);
            Assert.IsTrue(t2.IsCompleted);
            Assert.IsTrue(t3.IsCompleted);
            Assert.IsTrue(t4.IsCompleted);
            Assert.IsTrue(t5.IsCompleted);
            Assert.IsTrue(t6.IsCompleted);
            Assert.IsTrue(t7.IsCompleted);
            Assert.IsTrue(t8.IsCompleted);
            Assert.IsTrue(t9.IsCompleted);
            Assert.IsTrue(t10.IsCompleted);
        }

        private static Task CreateTaskWithResourceLock(int id, SimpleTaskScheduler.ExecutionToken token, Object resource1, Object resource2, List<int> listCount)
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
                        int val = token.LockResource(resource1);
                        if (val == 0)
                        {
                            listCount.Add(0);
                        }
                        else if (val == 1)
                        {
                            return;
                        }
                        else if (val == 2)
                        {
                        }
                    }
                    if (i == 3)
                    {
                        int val = token.LockResource(resource2);
                        if (val == 0)
                        {
                            listCount.Add(0);
                        }
                        else if (val == 1)
                        {
                            return;
                        }
                        else if (val == 2)
                        {
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

        [TestMethod]
        public void TestTwoTasksRequestingResourcesBothShouldSucceed()
        {
            int numOfThreads = 2;
            SimpleTaskScheduler taskScheduler = SimpleTaskScheduler.CreatePreemptive(numOfThreads);
            SimpleTaskScheduler.ExecutionToken token1 = taskScheduler.GetExecutionToken();
            SimpleTaskScheduler.ExecutionToken token2 = taskScheduler.GetExecutionToken();
            Object resource1 = new object();
            Object resource2 = new object();
            List<int> list1 = new List<int>();
            List<int> list2 = new List<int>();
            Task t1 = CreateTaskWithResourceLock(id: 1, token: token1, resource1: resource1, resource2: resource2, list1);
            Task t2 = CreateTaskWithResourceLock(id: 2, token: token2, resource1: resource2, resource2: resource1, list2);
            taskScheduler.Register(t1, 5, 5000, token1);
            taskScheduler.Register(t2, 4, 5000, token2);

            t1.Start(taskScheduler);
            Task.Delay(500).Wait();
            t2.Start(taskScheduler);

            Task.Delay(5000).Wait();
            Assert.AreEqual(list1.Count, 2);
            Assert.AreEqual(list2.Count, 2);
        }

        [TestMethod]
        public void TestTwoTasksRequestingResourcesOneRequestShouldFailDeadlockDetection()
        {
            int numOfThreads = 2;
            SimpleTaskScheduler taskScheduler = SimpleTaskScheduler.CreatePreemptive(numOfThreads);
            SimpleTaskScheduler.ExecutionToken token1 = taskScheduler.GetExecutionToken();
            SimpleTaskScheduler.ExecutionToken token2 = taskScheduler.GetExecutionToken();
            Object resource1 = new object();
            Object resource2 = new object();
            List<int> list1 = new List<int>();
            List<int> list2 = new List<int>();
            Task t1 = CreateTaskWithResourceLock(id: 1, token: token1, resource1: resource1, resource2: resource2, list1);
            Task t2 = CreateTaskWithResourceLock(id: 2, token: token2, resource1: resource2, resource2: resource1, list2);
            taskScheduler.Register(t1, 5, 5000, token1);
            taskScheduler.Register(t2, 4, 5000, token2);

            t1.Start(taskScheduler);
            t2.Start(taskScheduler);

            Task.Delay(5000).Wait();
            Assert.AreEqual(list1.Count, 2);
            Assert.AreEqual(list2.Count, 1);
        }

        [TestMethod]
        public void TestSchedulingOfPreemptiveUsingBaseClassShouldFailQueueing()
        {
            int numOfThreads = 2;
            SimpleTaskScheduler taskScheduler = SimpleTaskScheduler.CreatePreemptive(numOfThreads);
            TaskScheduler baseTaskScheduler = taskScheduler;
            Task t = CreateTask(id: 1, token: taskScheduler.GetExecutionToken());
            t.Start(baseTaskScheduler);

            Assert.IsFalse(taskScheduler.IsTaskCurrentlyExecuting(t));
        }

        [TestMethod]
        public void TestSchedulingOfNonPreemptiveUsingBaseClassShouldSucceedQueueing()
        {
            int numOfThreads = 2;
            SimpleTaskScheduler taskScheduler = SimpleTaskScheduler.CreateNonPreemptive(numOfThreads);
            TaskScheduler baseTaskScheduler = taskScheduler;
            Task t = CreateTask(id: 1, token: taskScheduler.GetExecutionToken());
            t.Start(baseTaskScheduler);

            Task.Delay(500).Wait();
            Assert.IsTrue(taskScheduler.IsTaskCurrentlyExecuting(t));
        }
    }
}
