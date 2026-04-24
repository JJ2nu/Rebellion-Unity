using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rebellion.Utils
{
    /// <summary>
    /// Collection of general-purpose static helper methods used across the project.
    /// </summary>
    public static class Helpers
    {
        /// <summary>Remap a value from one range to another.</summary>
        public static float Remap(float value, float fromMin, float fromMax, float toMin, float toMax)
        {
            return toMin + (value - fromMin) * (toMax - toMin) / (fromMax - fromMin);
        }

        /// <summary>Returns a random element from a list.</summary>
        public static T RandomElement<T>(IList<T> list)
        {
            if (list == null || list.Count == 0) return default;
            return list[Random.Range(0, list.Count)];
        }

        /// <summary>Shuffle a list in-place using Fisher-Yates algorithm.</summary>
        public static void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[j], list[i]) = (list[i], list[j]);
            }
        }

        /// <summary>Clamp an angle to [-180, 180] range.</summary>
        public static float ClampAngle(float angle, float min, float max)
        {
            angle = Mathf.Repeat(angle + 180f, 360f) - 180f;
            return Mathf.Clamp(angle, min, max);
        }

        /// <summary>Returns a direction Vector2 pointing from <paramref name="from"/> to <paramref name="to"/>.</summary>
        public static Vector2 Direction2D(Vector2 from, Vector2 to)
        {
            return (to - from).normalized;
        }

        /// <summary>Check if a layer is included in a LayerMask.</summary>
        public static bool IsInLayerMask(int layer, LayerMask mask)
        {
            return ((1 << layer) & mask) != 0;
        }

        /// <summary>Destroy all children of a Transform.</summary>
        public static void DestroyChildren(Transform parent)
        {
            foreach (Transform child in parent)
                Object.Destroy(child.gameObject);
        }
    }
}
