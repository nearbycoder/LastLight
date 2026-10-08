using System.Linq;
using LastLight.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

namespace LastLight.Tests
{
    /// <summary>The pad's buttons for focus and the horn: defaults, moving a button, the exchange
    /// when an action would lose its last button, Start and View refused, reset, a damaged or older
    /// save, and the prompts in the bound buttons' names. Works on its own PadBindings and
    /// SaveData, never on the save.</summary>
    public class PadBindingsTests
    {
        [Test]
        public void DefaultsAreTheReleasedButtons()
        {
            var p = new PadBindings();
            CollectionAssert.AreEqual(new[] { GamepadButton.RightTrigger, GamepadButton.LeftTrigger }, p.Buttons(KeeperAction.Focus).ToArray());
            CollectionAssert.AreEqual(new[] { GamepadButton.South }, p.Buttons(KeeperAction.Horn).ToArray());
            Assert.IsFalse(PadBindings.CanBind(KeeperAction.TurnLeft) || PadBindings.CanBind(KeeperAction.TurnRight), "the sticks turn the light");
        }

        [Test]
        public void BindingAButtonMovesItFromWhereItWas()
        {
            var p = new PadBindings();
            // RB onto the horn's empty second slot: nothing moves.
            Assert.IsTrue(p.Bind(KeeperAction.Horn, 1, GamepadButton.RightShoulder, out var from, out var swapped));
            Assert.IsNull(from);
            Assert.IsNull(swapped);
            CollectionAssert.AreEqual(new[] { GamepadButton.South, GamepadButton.RightShoulder }, p.Buttons(KeeperAction.Horn).ToArray());

            // LT moves from focus (which keeps RT) to the horn, in A's place.
            Assert.IsTrue(p.Bind(KeeperAction.Horn, 0, GamepadButton.LeftTrigger, out from, out swapped));
            Assert.AreEqual(KeeperAction.Focus, from);
            Assert.IsNull(swapped);
            CollectionAssert.AreEqual(new[] { GamepadButton.RightTrigger }, p.Buttons(KeeperAction.Focus).ToArray());
            CollectionAssert.AreEqual(new[] { GamepadButton.LeftTrigger, GamepadButton.RightShoulder }, p.Buttons(KeeperAction.Horn).ToArray());

            // Within one action a button moves between slots.
            Assert.IsTrue(p.Bind(KeeperAction.Horn, 0, GamepadButton.RightShoulder, out from, out _));
            Assert.IsNull(from);
            CollectionAssert.AreEqual(new[] { GamepadButton.RightShoulder }, p.Buttons(KeeperAction.Horn).ToArray());
        }

        [Test]
        public void AnActionNeverLosesItsLastButton()
        {
            var p = new PadBindings();
            // A is the horn's only button: taking it for focus gives the horn RT, focus's slot's button, in exchange.
            Assert.IsTrue(p.Bind(KeeperAction.Focus, 0, GamepadButton.South, out var from, out var swapped));
            Assert.AreEqual(KeeperAction.Horn, from);
            Assert.AreEqual(GamepadButton.RightTrigger, swapped);
            CollectionAssert.AreEqual(new[] { GamepadButton.RightTrigger }, p.Buttons(KeeperAction.Horn).ToArray());
            CollectionAssert.AreEqual(new[] { GamepadButton.South, GamepadButton.LeftTrigger }, p.Buttons(KeeperAction.Focus).ToArray());

            // With nothing to exchange (an empty slot), it's refused.
            Assert.IsTrue(p.Clear(KeeperAction.Focus, 1));
            Assert.IsFalse(p.Bind(KeeperAction.Horn, 1, GamepadButton.South, out _, out _), "A is focus's only button and the slot is empty");
            CollectionAssert.AreEqual(new[] { GamepadButton.South }, p.Buttons(KeeperAction.Focus).ToArray());

            // And the last button can't be emptied.
            Assert.IsFalse(p.Clear(KeeperAction.Focus, 0));
            Assert.IsFalse(p.Clear(KeeperAction.Horn, 0));
            Assert.AreEqual(GamepadButton.South, p.Get(KeeperAction.Focus, 0));
        }

        [Test]
        public void StartAndViewStayAsTheyAre()
        {
            var p = new PadBindings();
            foreach (var b in new[] { GamepadButton.Start, GamepadButton.Select })
            {
                Assert.IsFalse(p.Bind(KeeperAction.Horn, 1, b, out _, out _), b + " can't be bound");
                Assert.IsNull(p.Get(KeeperAction.Horn, 1));
            }
            Assert.IsFalse(p.Bind(KeeperAction.TurnLeft, 0, GamepadButton.North, out _, out _), "turning isn't on buttons");
        }

        [Test]
        public void ResettingAndRepairing()
        {
            var p = new PadBindings();
            p.Bind(KeeperAction.Horn, 0, GamepadButton.LeftShoulder, out _, out _);
            p.Reset();
            CollectionAssert.AreEqual(new[] { GamepadButton.South }, p.Buttons(KeeperAction.Horn).ToArray());
            CollectionAssert.AreEqual(new[] { GamepadButton.RightTrigger, GamepadButton.LeftTrigger }, p.Buttons(KeeperAction.Focus).ToArray());

            // Unknown, reserved and repeated buttons go; a third slot is dropped.
            var damaged = new PadBindings
            {
                focus = new[] { (int)GamepadButton.Start, (int)GamepadButton.RightShoulder, (int)GamepadButton.North },
                horn = new[] { 9999, (int)GamepadButton.North },
            };
            damaged.Validate();
            Assert.IsNull(damaged.Get(KeeperAction.Focus, 0), "Start is reserved");
            Assert.AreEqual(GamepadButton.RightShoulder, damaged.Get(KeeperAction.Focus, 1));
            CollectionAssert.AreEqual(new[] { GamepadButton.North }, damaged.Buttons(KeeperAction.Horn).ToArray(), "9999 is unknown; Y was past focus's second slot, so the horn keeps it");

            // An action left with nothing it could use brings the defaults back.
            var emptied = new PadBindings
            {
                focus = new[] { (int)GamepadButton.RightShoulder, PadBindings.Empty },
                horn = new[] { (int)GamepadButton.RightShoulder, (int)GamepadButton.Select },
            };
            emptied.Validate();
            CollectionAssert.AreEqual(new[] { GamepadButton.South }, emptied.Buttons(KeeperAction.Horn).ToArray());
            CollectionAssert.AreEqual(new[] { GamepadButton.RightTrigger, GamepadButton.LeftTrigger }, emptied.Buttons(KeeperAction.Focus).ToArray());

            var missing = new PadBindings { focus = null, horn = null };
            missing.Validate();
            CollectionAssert.AreEqual(new[] { GamepadButton.South }, missing.Buttons(KeeperAction.Horn).ToArray());
        }

        [Test]
        public void SurvivesTheSaveFormatAndOlderSavesGetTheDefaults()
        {
            var save = new SaveData();
            save.pad.Bind(KeeperAction.Horn, 1, GamepadButton.RightShoulder, out _, out _);
            save.pad.Bind(KeeperAction.Focus, 1, GamepadButton.DpadUp, out _, out _);
            var copy = new SaveData();
            Assert.IsTrue(SaveStore.TryParse(JsonUtility.ToJson(save), copy, out _));
            foreach (var a in PadBindings.Actions)
                for (int i = 0; i < PadBindings.Slots; i++)
                    Assert.AreEqual(save.pad.Get(a, i), copy.pad.Get(a, i), $"{a} slot {i}");
            Assert.AreEqual(GamepadButton.DpadUp, copy.pad.Get(KeeperAction.Focus, 1), "the d-pad's up (0) isn't an empty slot");

            // A save from before round 11 has no pad buttons: it reads with the defaults.
            var older = new SaveData();
            Assert.IsTrue(SaveStore.TryParse("{\"version\":1,\"unlocked\":5,\"keys\":{\"turnLeft\":[4,63,0],\"turnRight\":[7,64,0],\"focus\":[51,26,65],\"horn\":[1,0,0]}}", older, out _));
            Assert.AreEqual(5, older.unlocked);
            CollectionAssert.AreEqual(new[] { GamepadButton.South }, older.pad.Buttons(KeeperAction.Horn).ToArray());
            CollectionAssert.AreEqual(new[] { GamepadButton.RightTrigger, GamepadButton.LeftTrigger }, older.pad.Buttons(KeeperAction.Focus).ToArray());
        }

        [Test]
        public void TheNotesNameTheBoundButtons()
        {
            string Body(SaveData s, string id) => KeeperNotes.For(s, true).First(e => e.Id == id).Body;
            var save = new SaveData { unlocked = 12, padStyle = (int)PadStyle.Xbox };
            StringAssert.Contains("Hold the right trigger to focus", Body(save, "light"), "the defaults read as before");
            StringAssert.Contains("Press A to sound the foghorn", Body(save, "fog"));
            save.pad.Bind(KeeperAction.Horn, 0, GamepadButton.RightShoulder, out _, out _);
            save.pad.Bind(KeeperAction.Focus, 0, GamepadButton.LeftShoulder, out _, out _);
            StringAssert.Contains("Hold LB to focus", Body(save, "light"));
            StringAssert.Contains("Press RB to sound the foghorn", Body(save, "fog"));
            save.padStyle = (int)PadStyle.PlayStation;
            StringAssert.Contains("Hold L1 to focus", Body(save, "light"));
            StringAssert.Contains("Press R1 to sound the foghorn", Body(save, "fog"));
            save.padStyle = (int)PadStyle.Nintendo;
            StringAssert.Contains("Press R to sound the foghorn", Body(save, "fog"));
            Assert.AreEqual("D-pad ↑", PadButtons.Name(GamepadButton.DpadUp, PadStyle.Xbox));
            Assert.AreEqual("Triangle", PadButtons.Name(GamepadButton.North, PadStyle.PlayStation));
            Assert.AreEqual("ZL", PadButtons.Name(GamepadButton.LeftTrigger, PadStyle.Nintendo));
        }
    }
}
