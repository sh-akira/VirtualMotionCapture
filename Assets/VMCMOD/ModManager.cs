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
        private string ModsPath;

        private Dictionary<VMCPluginAttribute, Component> LoadedMods = new Dictionary<VMCPluginAttribute, Component>();

        public Action OnBeforeModLoad;

        public bool IsModLoaded => LoadedMods.Any();

        private void Awake()
        {
            ModsPath = Application.dataPath + "/../Mods/";
            if (Directory.Exists(ModsPath) == false)
            {
                Directory.CreateDirectory(ModsPath);
            }
        }

        private void Start()
        {
            ImportMods();
        }

        private void ImportMods()
        {
            Debug.Log("Start Loading Mods");
            var attributeTypesList = new Dictionary<List<Type>, string>();
            foreach (var dllFile in Directory.GetFiles(ModsPath, "*.dll", SearchOption.AllDirectories))
            {
                try
                {
                    Assembly assembly = Assembly.LoadFrom(dllFile);
                    var attributeTypes = assembly.GetTypes().Where(x => x.IsPublic && x.IsDefined(typeof(VMCPluginAttribute)));
                    if (attributeTypes.Any())
                    {
                        attributeTypesList.Add(attributeTypes.ToList(), dllFile);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }

            if (attributeTypesList.Any())
            {
                OnBeforeModLoad?.Invoke();
            }

            foreach (var attributeTypes in attributeTypesList)
            {
                foreach (Type t in attributeTypes.Key)
                {
                    try
                    {
                        var attribute = (VMCPluginAttribute)Attribute.GetCustomAttribute(t, typeof(VMCPluginAttribute));
                        attribute.InstanceId = Guid.NewGuid().ToString();
                        attribute.AssemblyPath = attributeTypes.Value;
                        var component = gameObject.AddComponent(t);
                        attribute.OnSetting = new List<Action>();
                        foreach (MethodInfo method in t.GetMethods().Where(x => x.IsDefined(typeof(OnSettingAttribute))))
                        {
                            attribute.OnSetting.Add(() => method.Invoke(component, null));
                        }
                        LoadedMods[attribute] = component;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }
                }
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
                foreach (var settingAction in attribute.OnSetting)
                {
                    settingAction?.Invoke();
                }
            }
        }
    }
}