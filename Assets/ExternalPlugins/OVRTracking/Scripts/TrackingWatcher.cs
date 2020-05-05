using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TrackingWatcher : MonoBehaviour {

    public bool ok = false; //デバッグ用

    Action<float> SetWeightAction = null;

    //Tracker Handlerから呼び出される
    public void IsOK(bool ok)
    {
        this.ok = ok;
    }

    public void SetActionOfSetWeight(Action<float> action)
    {
        this.SetWeightAction = action;
    }

    /*
	void Start () {
		
	}
	
	void Update () {
		
	}
    */
}
