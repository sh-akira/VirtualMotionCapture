using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnityMemoryMappedFile
{
    public partial class PipeCommands
    {
        public class mocopi_GetSetting { }
        public class mocopi_SetSetting
        {
            public bool enable { get; set; }
            public int port { get; set; }

            public bool ApplyRootPosition { get; set; }
            public bool ApplyRootRotation { get; set; }
            public bool ApplyChest { get; set; }
            public bool ApplySpine { get; set; }
            public bool ApplyHead { get; set; }
            public bool ApplyLeftArm { get; set; }
            public bool ApplyRightArm { get; set; }
            public bool ApplyLeftHand { get; set; }
            public bool ApplyRightHand { get; set; }
            public bool ApplyLeftLeg { get; set; }
            public bool ApplyRightLeg { get; set; }
            public bool ApplyLeftFoot { get; set; }
            public bool ApplyRightFoot { get; set; }
        }
        public class mocopi_Recenter { }
    }
}
