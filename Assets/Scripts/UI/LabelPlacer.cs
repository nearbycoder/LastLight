using System.Collections.Generic;
using UnityEngine;

namespace LastLight.UI
{
    /// <summary>
    /// Keeps names from sitting on each other: a label tries a short list of places near what it
    /// names, in order, and takes the first that clears everything already placed (and stays in
    /// bounds). The names on the water and the dawn chart both use it. Pure, so it can be tested.
    /// </summary>
    public static class LabelPlacer
    {
        /// <summary>Two rects overlap by more than a hair (touching edges don't count).</summary>
        public static bool Overlaps(Rect a, Rect b, float slack = 1f) =>
            a.xMin < b.xMax - slack && b.xMin < a.xMax - slack && a.yMin < b.yMax - slack && b.yMin < a.yMax - slack;

        static float OverlapArea(Rect a, Rect b)
        {
            float w = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
            float h = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
            return w > 0f && h > 0f ? w * h : 0f;
        }

        /// <summary>
        /// Picks an offset for <paramref name="rect"/> from <paramref name="offsets"/> (in order of
        /// preference), adds the placed rect to <paramref name="placed"/>, and returns the offset.
        /// When none is clear, the offset that covers least is taken (the first, on a tie).
        /// </summary>
        public static Vector2 Place(Rect rect, IReadOnlyList<Vector2> offsets, List<Rect> placed, Rect bounds)
        {
            Vector2 best = offsets.Count > 0 ? offsets[0] : Vector2.zero;
            float bestCover = float.MaxValue;
            for (int i = 0; i < offsets.Count; i++)
            {
                var r = rect;
                r.position += offsets[i];
                bool inside = r.xMin >= bounds.xMin && r.xMax <= bounds.xMax && r.yMin >= bounds.yMin && r.yMax <= bounds.yMax;
                float cover = 0f;
                foreach (var p in placed) cover += OverlapArea(r, p);
                if (!inside) cover += 1e6f;
                if (cover < bestCover - 0.5f) { bestCover = cover; best = offsets[i]; }
                if (cover <= 0.5f) break;
            }
            var done = rect;
            done.position += best;
            placed.Add(done);
            return best;
        }

        /// <summary>Offsets that step a label up and down, nearest first: 0, +step, -step, +2 step...
        /// (up first when <paramref name="upFirst"/>).</summary>
        public static List<Vector2> Vertical(float step, int each, bool upFirst = true)
        {
            var list = new List<Vector2> { Vector2.zero };
            float s = upFirst ? 1f : -1f;
            for (int i = 1; i <= each; i++)
            {
                list.Add(new Vector2(0f, s * step * i));
                list.Add(new Vector2(0f, -s * step * i));
            }
            return list;
        }
    }
}
