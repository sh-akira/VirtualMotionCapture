using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace VMCMod
{
    public class ModManager : MonoBehaviour
    {
        private readonly string ModsPath = Application.dataPath + "/../Mods/";

        private Dictionary<VMCPluginAttribute, Component> LoadedMods = new Dictionary<VMCPluginAttribute, Component>();

        private void Start()
        {
            ImportMods();
        }

        private void ImportMods()
        {

            foreach (var dllFile in Directory.GetFiles(ModsPath, "*.dll", SearchOption.AllDirectories))
            {
                try
                {
                    Assembly assembly = Assembly.LoadFrom(dllFile);
                    foreach (Type t in assembly.GetTypes().Where(x => x.IsPublic && x.IsDefined(typeof(VMCPluginAttribute))))
                    {
                        var attribute = (VMCPluginAttribute)Attribute.GetCustomAttribute(t, typeof(VMCPluginAttribute));
                        attribute.InstanceId = Guid.NewGuid().ToString();
                        var component = gameObject.AddComponent(t);
                        attribute.OnSetting = new List<Action>();
                        foreach (MethodInfo method in t.GetMethods().Where(x => x.IsDefined(typeof(OnSettingAttribute))))
                        {
                            attribute.OnSetting.Add(() => method.Invoke(component, null));
                        }
                        LoadedMods[attribute] = component;
                    }
                }
                catch { }
            }
        }

        public List<VMCPluginAttribute> GetModsList()
        {
            return LoadedMods.Keys.ToList();
        }

        public void InvokeSetting(string instanceId)
        {
            var attribute = LoadedMods.Keys.FirstOrDefault(x => x.InstanceId == instanceId);
            if (attribute != null)
            {
                foreach(var settingAction in attribute.OnSetting)
                {
                    settingAction?.Invoke();
                }
            }
        }
    }
}