using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnityMemoryMappedFile
{
    public class DataReceivedEventArgs : EventArgs
    {
        public Type CommandType;
        public string RequestId;
        public object Data;
        public DataReceivedEventArgs(Type commandType, string requestId, object data)
        {
            CommandType = commandType; RequestId = requestId; Data = data;
        }
    }
}
