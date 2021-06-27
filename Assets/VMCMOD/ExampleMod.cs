using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VMCMod;

[VMCPlugin(
    Name: "ExampleMOD", 
    Version:"1.0.0", 
    Author:"sh_akira", 
    Description:"サンプルMOD",
    AuthorURL:"https://twitter.com/sh_akira",
    PluginURL:"https://vmc.info/")]
public class ExampleMOD : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    [OnSetting]
    public void OnSetting()
    {

    }
}
