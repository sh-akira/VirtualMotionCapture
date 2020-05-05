using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TrackingWatcher : MonoBehaviour {

    public bool ok = false; //デバッグ用
    public bool action = false;//デバッグ用

    Action<float> SetWeightAction = null;

    //Tracker Handlerから呼び出される
    public void IsOK(bool ok)
    {
        this.ok = ok;

        SetWeightAction?.Invoke(ok?1f:0f);
    }

    public void SetActionOfSetWeight(Action<float> action)
    {
        this.SetWeightAction = action;
        this.action = true;
    }

    public void Clear()
    {
        this.SetWeightAction = null;
        this.action = false;

        Debug.Log(transform.name + " : Clear!");
    }

    /*
	void Start () {
		
	}
	
	void Update () {
		
	}
    */
}
