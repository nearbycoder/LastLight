using LastLight.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace LastLight.UI
{
    /// <summary>
    /// The score to beat, under the score: "BEST 880" when the night (or the Night Watch) has a best
    /// already, which turns the moment the score passes it: "NEW BEST" in a watch, which is always
    /// kept, and "PAST YOUR BEST" on one of the twelve nights, which counts only if it's kept.
    /// Nothing shows the first time a night is played.
    /// </summary>
    public sealed partial class Hud
    {
        /// <summary>The trailer's shoot turns it off, so a re-shoot matches the released cut.</summary>
        public static bool BestShown = true;

        Text bestLabel;
        int bestToBeat;
        bool bestPassed, bestEndless;
        const float BestRow = 24f;   // how far it pushes the lamps and the horn down

        static readonly Color BestColor = new Color(0.65f, 0.7f, 0.76f, 0.9f);

        void BuildBest()
        {
            bestLabel = UiKit.Text("Best", scoreBlock, "", UiKit.BodyBold, 17, BestColor, TextAnchor.UpperRight).Shadowed();
            bestLabel.rectTransform.Pin(new Vector2(1, 1), new Vector2(1, 1), new Vector2(0, -84), new Vector2(300, 24));
            bestLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            bestLabel.text = "";
        }

        /// <summary>The best this night or watch has to beat (0: none yet, nothing shows).</summary>
        public void SetBest(int best, bool endless)
        {
            bestToBeat = BestShown ? best : 0;
            bestEndless = endless;
            bestPassed = false;
            bestLabel.text = bestToBeat > 0 ? UiKit.Spaced($"BEST {bestToBeat:N0}") : "";
            bestLabel.color = BestColor;
            bestLabel.transform.localScale = Vector3.one;
            // The lamps at stake (and the horn under them) make room for the line.
            float row = bestToBeat > 0 ? BestRow : 0f;
            lampRow.anchoredPosition = new Vector2(0, -86 - row);
            hornPanel.anchoredPosition = new Vector2(-50, (endless || !LampsAtStakeShown ? -130 : -212) - row);
        }

        void UpdateBest(SimWorld w)
        {
            if (bestToBeat <= 0 || bestPassed || w.Score <= bestToBeat) return;
            bestPassed = true;
            bestLabel.text = UiKit.Spaced(bestEndless ? "NEW BEST" : "PAST YOUR BEST");
            bestLabel.color = UiKit.BrassBright;
            Tween.Punch(bestLabel.transform, 0.35f, 0.5f);
        }

        /// <summary>For tours: where the line, the lamps at stake and the horn sit, and which show.</summary>
        public (Rect best, Rect lamps, bool lampsOn, Rect horn, bool hornOn) TourBestRects =>
            (CanvasRect(bestLabel.rectTransform), CanvasRect(lampRow), lampRow.gameObject.activeInHierarchy, CanvasRect(hornPanel), hornPanel.gameObject.activeSelf);

        /// <summary>The line under the score as shown, and the best it's measuring against; for tours.</summary>
        public (string text, int best, bool passed) BestLine => (System.Text.RegularExpressions.Regex.Replace(bestLabel.text, @"\s", ""), bestToBeat, bestPassed);
    }
}
