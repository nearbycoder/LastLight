using System.Linq;
using LastLight.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LastLight.Tests
{
    /// <summary>Rebindable keys: defaults, moving a key, clearing, the reserved keys, reset and a
    /// damaged save. Works on its own KeyBindings, never on the save.</summary>
    public class KeyBindingsTests
    {
        [Test]
        public void DefaultsAreTheReleasedKeys()
        {
            var k = new KeyBindings();
            CollectionAssert.AreEqual(new[] { Key.A, Key.LeftArrow }, k.Keys(KeeperAction.TurnLeft).ToArray());
            CollectionAssert.AreEqual(new[] { Key.D, Key.RightArrow }, k.Keys(KeeperAction.TurnRight).ToArray());
            CollectionAssert.AreEqual(new[] { Key.LeftShift, Key.W, Key.UpArrow }, k.Keys(KeeperAction.Focus).ToArray());
            CollectionAssert.AreEqual(new[] { Key.Space }, k.Keys(KeeperAction.Horn).ToArray());
        }

        [Test]
        public void BindingAKeyMovesItFromWhereItWas()
        {
            var k = new KeyBindings();
            Assert.IsTrue(k.Bind(KeeperAction.Horn, 0, Key.W, out var from));
            Assert.AreEqual(KeeperAction.Focus, from, "W was a focus key");
            Assert.AreEqual(Key.W, k.Get(KeeperAction.Horn, 0));
            CollectionAssert.AreEqual(new[] { Key.LeftShift, Key.UpArrow }, k.Keys(KeeperAction.Focus).ToArray());
            CollectionAssert.DoesNotContain(k.Keys(KeeperAction.Horn).ToArray(), Key.Space, "Space was replaced in that slot");

            // Within one action a key moves between slots, and nothing else is reported.
            Assert.IsTrue(k.Bind(KeeperAction.TurnLeft, 2, Key.A, out from));
            Assert.IsNull(from);
            CollectionAssert.AreEqual(new[] { Key.LeftArrow, Key.A }, k.Keys(KeeperAction.TurnLeft).ToArray());
            Assert.AreEqual(Key.None, k.Get(KeeperAction.TurnLeft, 0));

            // Binding a slot to the key it already has changes nothing.
            Assert.IsTrue(k.Bind(KeeperAction.TurnLeft, 2, Key.A, out from));
            Assert.IsNull(from);
            Assert.AreEqual(Key.A, k.Get(KeeperAction.TurnLeft, 2));
        }

        [Test]
        public void EscAndPStayAsPause()
        {
            var k = new KeyBindings();
            foreach (var key in new[] { Key.Escape, Key.P, Key.None })
            {
                Assert.IsFalse(k.Bind(KeeperAction.Horn, 1, key, out _), key + " can't be bound");
                Assert.AreEqual(Key.None, k.Get(KeeperAction.Horn, 1));
            }
            Assert.IsTrue(KeyBindings.Reserved(Key.Escape) && KeyBindings.Reserved(Key.P));
        }

        [Test]
        public void ClearingAndResetting()
        {
            var k = new KeyBindings();
            k.Clear(KeeperAction.Horn, 0);
            Assert.IsEmpty(k.Keys(KeeperAction.Horn).ToArray());
            Assert.AreEqual("", k.First(KeeperAction.Horn), "an action with no keys has no name to show");
            k.Bind(KeeperAction.Focus, 0, Key.J, out _);
            k.Reset();
            CollectionAssert.AreEqual(new[] { Key.Space }, k.Keys(KeeperAction.Horn).ToArray());
            CollectionAssert.AreEqual(new[] { Key.LeftShift, Key.W, Key.UpArrow }, k.Keys(KeeperAction.Focus).ToArray());
        }

        [Test]
        public void ADamagedSaveIsRepaired()
        {
            var k = new KeyBindings
            {
                turnLeft = null,
                turnRight = new[] { (int)Key.D },
                focus = new[] { (int)Key.D, (int)Key.Escape, 99999, (int)Key.J, (int)Key.K },
                horn = new[] { (int)Key.P, (int)Key.Space, (int)Key.Space },
            };
            k.Validate();
            foreach (var a in KeyBindings.Actions) Assert.AreEqual(KeyBindings.Slots, k.Keys(a).Count() + Enumerable.Range(0, KeyBindings.Slots).Count(i => k.Get(a, i) == Key.None), a + " has three slots");
            Assert.IsEmpty(k.Keys(KeeperAction.TurnLeft).ToArray());
            CollectionAssert.AreEqual(new[] { Key.D }, k.Keys(KeeperAction.TurnRight).ToArray());
            CollectionAssert.AreEqual(new Key[0], k.Keys(KeeperAction.Focus).ToArray(), "D is taken, Esc reserved, 99999 unknown; J and K were past the third slot");
            CollectionAssert.AreEqual(new[] { Key.Space }, k.Keys(KeeperAction.Horn).ToArray(), "P is reserved and Space appears once");
        }

        [Test]
        public void SurvivesTheSaveFormat()
        {
            var k = new KeyBindings();
            k.Bind(KeeperAction.Horn, 1, Key.H, out _);
            k.Clear(KeeperAction.TurnLeft, 1);
            var copy = JsonUtility.FromJson<KeyBindings>(JsonUtility.ToJson(k));
            foreach (var a in KeyBindings.Actions)
                for (int i = 0; i < KeyBindings.Slots; i++)
                    Assert.AreEqual(k.Get(a, i), copy.Get(a, i), $"{a} slot {i}");
        }
    }
}
