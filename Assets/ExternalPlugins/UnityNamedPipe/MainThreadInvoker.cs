using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class MainThreadInvoker : MonoBehaviour {

    private class ActionInfo
    {
        private Action runAction;
        public bool Finished;
        public ActionInfo(Action action)
        {
            runAction = action;
        }
        public void Execute()
        {
            runAction();
            Finished = true;
        }
    }
    private ConcurrentQueue<ActionInfo> actionQueue = new ConcurrentQueue<ActionInfo>();

    void Start()
    {
        StartCoroutine(InvokeLoop());
    }

    public async Task InvokeAsync(Action action)
    {
        Debug.Log("InvokeAsync:" + action.GetType().ToString());
        var actionInfo = new ActionInfo(action);
        actionQueue.Enqueue(actionInfo);
        while(actionInfo.Finished == false)
        {
            await Task.Delay(10);
        }
    }

    public void BeginInvoke(Action action)
    {
        actionQueue.Enqueue(new ActionInfo(action));
    }

    IEnumerator InvokeLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(0.01f);

            ActionInfo actionInfo;
            while (actionQueue.TryDequeue(out actionInfo))
            {
                actionInfo.Execute();
            }
        }
    }
}
