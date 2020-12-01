using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Zadatak1.SchedulerLibrary
{
    /// <summary>
    /// Graph stracture which consists of only the basic mechanism for cycle detection, 
    /// still, other properties can easily be added.
    /// </summary>
    internal class GraphStructure
    {
        private static int counterForMapping = 0;
        Dictionary<Task, int> taskToInt = new Dictionary<Task, int>();
        Dictionary<Object, int> resourceToInt = new Dictionary<Object, int>();
        Dictionary<int, List<int>> adjacencyList = new Dictionary<int, List<int>>();

        internal GraphStructure()
        {

        }

        private enum EdgeDirection
        {
            Normal,
            Inverted
        }

        internal void AddEdge(Task task, Object resource)
        {
            AddEdge(task, resource, EdgeDirection.Normal);
        }

        internal void AddEdge(Object resource, Task task)
        {
            AddEdge(task, resource, EdgeDirection.Inverted);
        }

        private void AddEdge(Task task, Object resource, EdgeDirection direction)
        {
            if (!taskToInt.ContainsKey(task))
            {
                adjacencyList.Add(counterForMapping, new List<int>());
                taskToInt.Add(task, counterForMapping++);
            }
            if (!resourceToInt.ContainsKey(resource))
            {
                adjacencyList.Add(counterForMapping, new List<int>());
                resourceToInt.Add(resource, counterForMapping++);
            }

            if (direction == EdgeDirection.Normal)
            {
                if (SimpleTaskScheduler.IsVerbose)
                    Console.WriteLine("Added edge task " + task.GetHashCode() + " to resource " + resource.GetHashCode());
                AddEdge(taskToInt[task], resourceToInt[resource]);
            }
            else if (direction == EdgeDirection.Inverted)
            {
                if (SimpleTaskScheduler.IsVerbose)
                    Console.WriteLine("Added edge resource " + resource.GetHashCode() + " to task " + task.GetHashCode());
                AddEdge(resourceToInt[resource], taskToInt[task]);
            }
        }

        private void AddEdge(int x, int y)
        {
            if (!adjacencyList[x].Contains(y))
                adjacencyList[x].Add(y);
        }

        internal void RemoveEdge(Task task, Object resource)
        {
            RemoveEdge(task, resource, EdgeDirection.Normal);
        }

        internal void RemoveEdge(Object resource, Task task)
        {
            RemoveEdge(task, resource, EdgeDirection.Inverted);
        }

        private void RemoveEdge(Task task, Object resource, EdgeDirection direction)
        {
            if (direction == EdgeDirection.Normal)
            {
                RemoveEdge(taskToInt[task], resourceToInt[resource]);
            }
            else if (direction == EdgeDirection.Inverted)
            {
                RemoveEdge(resourceToInt[resource], taskToInt[task]);
            }
        }

        private void RemoveEdge(int x, int y)
        {
            if (adjacencyList[x].Contains(y))
                adjacencyList[x].Remove(y);
        }

        /// <summary>
        /// Returns true if there exists a directed edge from task to resource
        /// </summary>
        internal bool EdgeExists(Task task, Object resource)
        {
            return EdgeExists(task, resource, EdgeDirection.Normal);
        }

        /// <summary>
        /// Returns true if there exists a directed edge from resource to task
        /// </summary>
        internal bool EdgeExists(Object resource, Task task)
        {
            return EdgeExists(task, resource, EdgeDirection.Inverted);
        }

        private bool EdgeExists(Task task, Object resource, EdgeDirection direction)
        {
            if (!taskToInt.ContainsKey(task))
                return false;
            if (!resourceToInt.ContainsKey(resource))
                return false;

            if (direction == EdgeDirection.Normal)
                return EdgeExists(taskToInt[task], resourceToInt[resource]);
            else if (direction == EdgeDirection.Inverted)
                return EdgeExists(resourceToInt[resource], taskToInt[task]);

            return false;
        }

        private bool EdgeExists(int x, int y)
        {
            return adjacencyList[x].Contains(y);
        }

        /// <summary>
        /// Checks if there exists a cycle in the graph
        /// </summary>
        /// <returns>True if there exists a cycle in the graph, and false otherwise</returns>
        internal bool CheckCycle()
        {
            List<int> visited = new List<int>(counterForMapping);

            for (int i = 0; i < counterForMapping; ++i)
            {
                visited.Add(0);
            }

            for (int i = 0; i < counterForMapping; ++i)
            {
                if (visited[i] == 0 && CheckCycle(i, visited))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Recursive method for searching a cycle.
        /// </summary>
        private bool CheckCycle(int index, List<int> visited)
        {
            visited[index] = 1;

            foreach (int next in adjacencyList[index])
            {
                if (visited[next] == 0 && CheckCycle(next, visited))
                    return true;
                else if (visited[next] == 1)
                    return true;
            }

            visited[index] = 2;
            return false;
        }
    }
}
