using RootMotion.FinalIK;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VMC
{
    public class VRIKTimingManager : MonoBehaviour
    {
        private VRIK vrik;

        private void Awake()
        {
            StartCoroutine(AfterUpdateCoroutine());
        }

        private IEnumerator AfterUpdateCoroutine()
        {
            while (true)
            {
                yield return null;
                // run after Update()

                if (vrik == null) vrik = GetComponent<VRIK>();
                if (vrik == null) continue;
                if (vrik.enabled == false) continue;

                vrik.solver.FixTransforms();
                vrik.UpdateSolverExternal();

            }
        }
    }
}
