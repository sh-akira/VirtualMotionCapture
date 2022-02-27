using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DVRSDK.Utilities
{
    public class StoreTransform
    {
        public Vector3 Position { get; set; } = Vector3.zero;
        public Quaternion Rotation { get; set; } = Quaternion.identity;
        public StoreTransform() { }

        public StoreTransform(Vector3 position) : this()
        {
            SetPosition(position);
        }

        public StoreTransform(Quaternion rotation) : this()
        {
            SetRotation(rotation);
        }

        public StoreTransform(Vector3 position, Quaternion rotation) : this()
        {
            SetPosition(position);
            SetRotation(rotation);
        }

        public void Set(Vector3 position, Quaternion rotation)
        {
            SetPosition(position);
            SetRotation(rotation);
        }

        public void SetPosition(Vector3 position) => Position = position;
        public void SetRotation(Quaternion rotation) => Rotation = rotation;
    }
}
