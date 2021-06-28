
using System;
using System.Collections.Generic;

namespace VMCMod
{
    [AttributeUsage(AttributeTargets.Class)]
    public class VMCPluginAttribute : Attribute
    {
        public VMCPluginAttribute(string Name, string Version, string Author, string Description = null, string AuthorURL = null, string PluginURL = null)
        {
            this.Name = Name;
            this.Version = Version;
            this.Author = Author;
            this.Description = Description;
            this.AuthorURL = AuthorURL;
            this.PluginURL = PluginURL;
        }

        public string Name { get; }
        public string Version { get; }
        public string Author { get; }
        public string AuthorURL { get; }
        public string Description { get; }
        public string PluginURL { get; }

        public string InstanceId { get; set; }
        public string AssemblyPath { get; set; }
        internal List<Action> OnSetting { get; set; }
    }
}