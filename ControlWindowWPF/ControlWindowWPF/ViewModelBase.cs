using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VirtualMotionCaptureControlPanel
{
    /// <summary>
    /// ViewModelの基底クラス
    /// INotifyPropertyChangedを実装する
    /// </summary>
    public abstract class ViewModelBase : INotifyPropertyChanged
    {

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void RaisePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected virtual void RaisePropertyChanged(params string[] propertyNames)
        {
            foreach (var propertyName in propertyNames) RaisePropertyChanged(propertyName);
        }

        private Dictionary<string, object> PropertyDictionary = new Dictionary<string, object>();

        protected virtual void Setter(object value, [System.Runtime.CompilerServices.CallerMemberName] string PropertyName = "")
        {
            if (PropertyDictionary.ContainsKey(PropertyName))
            {
                if (PropertyDictionary[PropertyName] == value) return;
                PropertyDictionary[PropertyName] = value;
            }
            else
            {
                PropertyDictionary.Add(PropertyName, value);
            }
            RaisePropertyChanged(PropertyName);
        }

        protected virtual object Getter(string PropertyName)
        {
            object value;
            if (PropertyDictionary.TryGetValue(PropertyName, out value)) return value;
            return null;
        }

        protected virtual T Getter<T>([System.Runtime.CompilerServices.CallerMemberName] string PropertyName = "")
        {
            var ret = Getter(PropertyName);
            if (ret == null) return default(T);
            return (T)ret;
        }
    }
}
