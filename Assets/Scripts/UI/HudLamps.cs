using LastLight.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace LastLight.UI
{
    /// <summary>
    /// The lamps at stake, under the score during one of the twelve nights: all three lit at dusk.
    /// The third goes out the moment a ship first loses its way or is lured, the second at the first
    /// wreck, with a short note naming the ship, so the keeper knows what's still in play.
    /// </summary>
    public sealed partial class Hud
    {
        /// <summary>The trailer's shoot turns the lamps off, so a re-shoot matches the released cut.</summary>
        public static bool LampsAtStakeShown = true;

        RectTransform lampRow;
        readonly Image[] stakeLamps = new Image[3];
        Text lampNote;
        float lampNoteLife;
        int lampsAtStake = 3;
        bool lampsOn;

        static readonly Color StakeLit = new Color(1f, 0.82f, 0.5f, 0.95f);
        static readonly Color StakeOut = new Color(1f, 1f, 1f, 0.28f);
        const float NoteTime = 6f;

        void BuildLamps()
        {
            lampRow = UiKit.Rect("Lamps", scoreBlock).Pin(new Vector2(1, 1), new Vector2(1, 1), new Vector2(0, -86), new Vector2(300, 34));
            for (int i = 0; i < 3; i++)
            {
                stakeLamps[i] = UiKit.Image("Lamp", lampRow, SpriteFactory.Lamp, StakeLit);
                stakeLamps[i].rectTransform.Pin(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-(2 - i) * 34f, 0), new Vector2(34, 34));
            }
            lampNote = UiKit.Text("Note", lampRow, "", UiKit.BodyMedium, 19, UiKit.Paper, TextAnchor.UpperRight).Shadowed(0.8f, 2f);
            lampNote.rectTransform.Pin(new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, -4), new Vector2(280, 50));
            lampNote.lineSpacing = 1f;
        }

        void BindLamps(MissionDef def)
        {
            lampsOn = !def.endless && LampsAtStakeShown;
            lampRow.gameObject.SetActive(lampsOn);
            lampsAtStake = 3;
            for (int i = 0; i < 3; i++)
            {
                stakeLamps[i].sprite = SpriteFactory.Lamp;
                stakeLamps[i].color = StakeLit;
            }
            lampNote.text = "";
            lampNoteLife = 0f;
        }

        /// <summary>The lamps a night can still earn as it stands: none once it has failed, one after
        /// a wreck, two once any ship has lost its way or been lured, otherwise three. At dawn this is
        /// the night's <see cref="SimWorld.Lamps"/>.</summary>
        public static int LampsAtStake(SimWorld w)
        {
            if (w.Outcome == MissionOutcome.Failed) return 0;
            if (w.Wrecks > 0) return 1;
            foreach (var s in w.Ships) if (!s.SteadyHand) return 2;
            return 3;
        }

        void UpdateLamps(SimWorld w, float dt)
        {
            if (!lampsOn) return;
            int now = LampsAtStake(w);
            if (now < lampsAtStake)
            {
                for (int i = now; i < lampsAtStake; i++)
                {
                    stakeLamps[i].sprite = SpriteFactory.LampEmpty;
                    stakeLamps[i].color = StakeOut;
                    Tween.Punch(stakeLamps[i].transform, 0.45f, 0.5f);
                }
                string note = LampNote(w, now);
                if (note != "") { lampNote.text = note; lampNoteLife = NoteTime; }
            }
            lampsAtStake = now;
            if (Time.timeScale > 0f) lampNoteLife = Mathf.Max(0f, lampNoteLife - dt);   // the note waits while paused
            var c = lampNote.color;
            c.a = Mathf.Clamp01(lampNoteLife / 0.8f);
            lampNote.color = c;
        }

        /// <summary>Why a lamp went out, in two short lines: "STEADY HAND GONE" over "the Kittiwake
        /// lost its way", or "SHIP LOST" over "the Little Auk".</summary>
        static string LampNote(SimWorld w, int now)
        {
            if (now == 0) return "";   // the night is over; the dawn card says why
            string Head(string words) => $"<size=15><color=#C9A35A>{UiKit.Spaced(words)}</color></size>\n";
            if (now == 1)
            {
                SimShip last = null;
                foreach (var s in w.Ships)
                    if (s.State == ShipState.Wrecked && (last == null || s.WreckTime > last.WreckTime)) last = s;
                return Head("A SHIP LOST") + (last != null ? $"the {last.Name}" : "");
            }
            SimShip who = null;
            foreach (var s in w.Ships)
                if (!s.SteadyHand && (who == null || s.State == ShipState.Lost || s.State == ShipState.Lured)) who = s;
            if (who == null) return "";
            bool lured = who.State == ShipState.Lured || (who.State != ShipState.Lost && who.LuredCount > 0);
            return Head("STEADY HAND GONE") + $"the {who.Name} {(lured ? "was lured" : "lost its way")}";
        }

        /// <summary>The lamps lit under the score and the note beside them; for tours.</summary>
        public (bool shown, int lit, string note) LampsShown
        {
            get
            {
                int lit = 0;
                foreach (var l in stakeLamps) if (l.sprite == SpriteFactory.Lamp) lit++;
                string note = System.Text.RegularExpressions.Regex.Replace(lampNote.text, "<[^>]+>", "").Replace('\n', ' ');
                return (lampRow.gameObject.activeInHierarchy, lit, lampNote.color.a > 0.05f ? note : "");
            }
        }
    }
}
