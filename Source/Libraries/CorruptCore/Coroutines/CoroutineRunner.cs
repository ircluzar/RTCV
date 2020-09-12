namespace RTCV.CorruptCore.Coroutines
{
    using System.Collections.Generic;

    /// <summary>
    /// Used to manage and run coroutines
    /// </summary>
    public class CoroutineRunner
    {
        LinkedList<Coroutine> coroutines = new LinkedList<Coroutine>();
        public void StopAndClearAll()
        {
            foreach (var cor in coroutines)
            {
                cor.Stop();
            }
            coroutines.Clear();
        }

        public bool RemoveCoroutine(Coroutine coroutine)
        {
            return coroutines.Remove(coroutine);
        }
        public void AddCoroutine(Coroutine coroutine)
        {
            coroutines.AddLast(coroutine);
        }

        public void Update()
        {
            var curCoroutineNode = coroutines.First;
            while (curCoroutineNode != null)
            {
                Coroutine curCoroutine = curCoroutineNode.Value;
                curCoroutine.DoCycle();
                var nextNode = curCoroutineNode.Next;
                if (curCoroutine.IsComplete)
                {
                    curCoroutineNode.Value.Dispose();
                    curCoroutineNode.Value = null;
                    coroutines.Remove(curCoroutineNode);
                }
                curCoroutineNode = nextNode;
            }
        }
    }
}
