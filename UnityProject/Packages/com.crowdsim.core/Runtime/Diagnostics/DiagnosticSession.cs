#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CrowdSim.Core;
using CrowdSim.Core.Persistence;
using Newtonsoft.Json.Linq;

namespace CrowdSim.Diagnostics
{
    /// <summary>A bounded preset runner. Unity views receive detached data, never mutable engine entities.</summary>
    public sealed class DiagnosticSession
    {
        public const int LastTick = 120;
        public const int EventTick = 10;
        private readonly Simulation _simulation;
        private readonly JObject _baseline;
        private readonly List<string> _failures = new List<string>();
        private readonly FinalizedSceneCheckpoint _checkpoint;
        private readonly ReadOnlyCollection<string> _readOnlyFailures;

        public string Name { get; }
        public int Tick => _simulation.FrameIndex;
        public float StepSeconds => _simulation.Config.Agent.Dt;
        public bool Completed => Tick == LastTick;
        public int ComparedCheckpoints { get; private set; }
        public bool Passed => Completed && ComparedCheckpoints == 7 && _failures.Count == 0;
        public IReadOnlyList<string> Failures => _readOnlyFailures;
        public PresentationFrame Frame { get; private set; }
        public bool ReplayVerified { get; private set; }

        public DiagnosticSession(string preset, bool hybrid, string baselineJson, string? toxicFixtureJson)
        {
            Name = preset + (hybrid ? "-hybrid" : "-individual");
            _baseline = JObject.Parse(baselineJson);
            if (_baseline.Value<string>("Name") != Name) throw new ArgumentException("Wrong preset baseline.", nameof(baselineJson));
            _simulation = DiagnosticPresets.CreatePreset(preset, hybrid, toxicFixtureJson);
            _checkpoint = FinalizedSceneCheckpoint.Capture(_simulation, _simulation.Agents.Count, Name);
            _readOnlyFailures = _failures.AsReadOnly();
            ParityComparison.Compare(_baseline["InitialTraits"]!, DiagnosticPresets.Traits(_simulation), _failures, "InitialTraits");
            // Scheduling is paused by the caller. Engine Running stays true so observations
            // have the same query semantics and Running flag as the reference capture.
            _simulation.SetRunning(true);
            if (!_simulation.Running) throw new InvalidOperationException("Preset could not start.");
            CompareCheckpoint();
            Frame = PresentationFrame.Capture(_simulation);
        }

        public bool StepOnce()
        {
            if (Completed) return false;
            if (Tick == EventTick)
            {
                if (_simulation.ScenarioType == Core.Model.ScenarioType.ToxicFume)
                {
                    if (!_simulation.TriggerToxicFumeRelease()) throw new InvalidOperationException("Fume release rejected.");
                }
                else _simulation.ExplosionSystem.TriggerExplosion(_simulation, 28f, 17f);
            }
            int before = Tick;
            _simulation.Step();
            if (Tick != before + 1) throw new InvalidOperationException("Fixed-step driver did not advance once.");
            CompareCheckpoint();
            Frame = PresentationFrame.Capture(_simulation);
            if (Completed) VerifyReplay();
            return true;
        }

        private void CompareCheckpoint()
        {
            JToken? expected = ((JArray)_baseline["Checkpoints"]!).FirstOrDefault(c => c.Value<int>("FrameIndex") == Tick);
            if (expected == null) return;
            ParityComparison.Compare(expected, DiagnosticPresets.Observe(_simulation), _failures, "tick " + Tick);
            ComparedCheckpoints++;
        }

        private void VerifyReplay()
        {
            Simulation replay = _checkpoint.CreateReadySimulation();
            ParityComparison.Compare(_baseline["Replay"]!, DiagnosticPresets.Observe(replay), _failures, "Replay");
            ReplayVerified = replay.FrameIndex == 0 && !replay.Running &&
                !replay.Environment.HasExplosion() && !replay.ToxicFumeSystem.IsReleased;
            if (!ReplayVerified) _failures.Add("Replay did not restore the ready scene.");
        }
    }

    /// <summary>Render-cadence-independent fixed-step scheduling, retaining the reference catch-up limits.</summary>
    public sealed class FixedStepScheduler
    {
        private double _accumulator;
        public void Reset() => _accumulator = 0d;

        public int Advance(double elapsed, double step, double speed, Action tick, Func<bool> completed)
        {
            if (double.IsNaN(elapsed) || double.IsInfinity(elapsed) || elapsed < 0d) elapsed = 0d;
            if (step <= 0d || double.IsNaN(step) || double.IsInfinity(step)) throw new ArgumentOutOfRangeException(nameof(step));
            if (speed < 0.25d || speed > 4d || double.IsNaN(speed)) throw new ArgumentOutOfRangeException(nameof(speed));
            _accumulator += Math.Min(elapsed, step * 2d) * speed;
            int count = 0;
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            while (_accumulator >= step && count < 8 && !completed())
            {
                tick();
                _accumulator -= step;
                count++;
                if (Core.Compatibility.ReferenceClock.ElapsedMilliseconds(started) >= 12d)
                {
                    _accumulator = Math.Min(_accumulator, step);
                    break;
                }
            }
            if (count == 8 && _accumulator > step * 8d) _accumulator = step;
            return count;
        }
    }
}
