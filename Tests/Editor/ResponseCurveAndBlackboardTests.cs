using NUnit.Framework;
using UnityEngine;
using UtilityAIWorkbench.Runtime.Core;

namespace UtilityAIWorkbench.Tests
{
    public sealed class ResponseCurveAndBlackboardTests
    {
        [Test]
        public void ResponseCurve_EvaluatesAllParametricTypesWithinUnitRange()
        {
            var linear = ResponseCurve.Linear(slope: 1f);
            Assert.AreEqual(0f, linear.Evaluate(0f), 0.001f);
            Assert.AreEqual(0.5f, linear.Evaluate(0.5f), 0.001f);
            Assert.AreEqual(1f, linear.Evaluate(1f), 0.001f);

            var inverted = ResponseCurve.Linear(slope: 1f, invert: true);
            Assert.AreEqual(0.75f, inverted.Evaluate(0.25f), 0.001f);

            var poly = ResponseCurve.Polynomial(exponent: 2f);
            Assert.AreEqual(0.25f, poly.Evaluate(0.5f), 0.001f);

            var logistic = ResponseCurve.Logistic(slope: 10f, midpoint: 0.5f);
            Assert.AreEqual(0.5f, logistic.Evaluate(0.5f), 0.001f);
            Assert.Greater(logistic.Evaluate(0.8f), 0.9f);
            Assert.Less(logistic.Evaluate(0.2f), 0.1f);

            var step = ResponseCurve.Step(threshold: 0.6f);
            Assert.AreEqual(0f, step.Evaluate(0.59f), 0.001f);
            Assert.AreEqual(1f, step.Evaluate(0.60f), 0.001f);

            // Clamping & NaN safety
            Assert.AreEqual(0f, linear.Evaluate(float.NaN), 0.001f);
            Assert.AreEqual(1f, linear.Evaluate(2.5f), 0.001f);
        }

        [Test]
        public void UtilityBlackboard_StoresTypedFactsAndCapturesDebugEntries()
        {
            var bb = new UtilityBlackboard();
            var changedKeys = 0;
            bb.OnKeyChanged += _ => changedKeys++;

            bb.SetFloat("Health", 72.5f);
            bb.SetInt("Wave", 3);
            bb.SetBool("HasTarget", true);
            bb.SetString("Role", "Guard");
            bb.SetVector3("HomePos", new Vector3(1f, 2f, 3f));

            Assert.AreEqual(5, changedKeys);
            Assert.AreEqual(72.5f, bb.GetFloat("health"), 0.001f); // case-insensitive
            Assert.AreEqual(3f, bb.GetFloat("Wave"), 0.001f);       // int readable as float
            Assert.IsTrue(bb.GetBool("hastarget"));
            Assert.AreEqual("Guard", bb.GetString("Role"));
            Assert.AreEqual(new Vector3(1f, 2f, 3f), bb.GetVector3("HomePos"));

            var entries = bb.CaptureDebugEntries();
            Assert.AreEqual(5, entries.Count);

            Assert.IsTrue(bb.Remove("Role"));
            Assert.IsFalse(bb.TryGetString("Role", out _));
        }

        [Test]
        public void DecisionRingBuffer_WrapsChronologicallyAtCapacity()
        {
            var ring = new DecisionRingBuffer(capacity: 3);
            for (var i = 1; i <= 5; i++)
            {
                ring.Push(new DecisionSnapshot(i, i * 0.2f, "Guard", "Brain", $"b{i}", $"B{i}", 0.5f, false, false, null));
            }

            Assert.AreEqual(3, ring.Count);
            Assert.AreEqual(5, ring.Latest.TickNumber);

            var list = ring.ToChronologicalList();
            Assert.AreEqual(3, list.Count);
            Assert.AreEqual(3, list[0].TickNumber);
            Assert.AreEqual(4, list[1].TickNumber);
            Assert.AreEqual(5, list[2].TickNumber);
        }
    }
}
