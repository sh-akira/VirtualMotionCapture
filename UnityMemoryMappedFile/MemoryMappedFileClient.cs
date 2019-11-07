using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnityMemoryMappedFile
{
    public class MemoryMappedFileClient : MemoryMappedFileBase
    {
        public void Start(string pipeName)
        {
            StartInternal(pipeName, false);
        }
    }
}
