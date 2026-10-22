#!/usr/bin/env python3
"""Add read-only phase clocks to an extracted M10 baseline for CPU comparison.
Only run against the throwaway directory created by profile_m10_comparison.sh.
Assertions intentionally require the exact approved M10 source; no physics changes.
"""
from pathlib import Path
import sys
root = Path(sys.argv[1]).resolve()
if root == Path(__file__).resolve().parents[2] or (root / ".git").exists():
    raise ValueError("Instrument only an extracted throwaway M10 archive, never a Git checkout.")
def edit(p,a,b):
 f=root/p;s=f.read_text();assert a in s,(p,a[:100]);f.write_text(s.replace(a,b))
b='UnityProject/Packages/com.crowdsim.core/Runtime/'
p=b+'Engine/Simulation.cs';edit(p,'        public double LastFurnitureMilliseconds { get; private set; }','''        public double LastFurnitureMilliseconds { get; private set; }
        /// <summary>Exclusive phase timings of the last completed tick; diagnostic only.</summary>
        public double LastGridMilliseconds { get; private set; }
        public double LastHazardMilliseconds { get; private set; }
        public double LastResponseMilliseconds { get; private set; }
        public double LastPreparationMilliseconds { get; private set; }
        public double LastMovementMilliseconds { get; private set; }
        public double LastContactsMilliseconds { get; private set; }
        public double LastExitMilliseconds { get; private set; }
        /// <summary>Nested subsets of movement/response, not additional exclusive phases.</summary>
        public double LastSfmMilliseconds { get; internal set; }
        public double LastCollisionMilliseconds { get; private set; }''')
p=b+'Engine/Simulation.Step.cs'
edit(p,'            RebuildAgentGrid();','''            LastSfmMilliseconds = LastCollisionMilliseconds = 0d;
            long phaseStarted = stepStarted;
            RebuildAgentGrid();
            LastGridMilliseconds = ReferenceClock.ElapsedMilliseconds(phaseStarted);
            phaseStarted = System.Diagnostics.Stopwatch.GetTimestamp();''')
edit(p,'            EvacuationSystem.UpdatePanic(this);','''            LastHazardMilliseconds = ReferenceClock.ElapsedMilliseconds(phaseStarted);
            phaseStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            EvacuationSystem.UpdatePanic(this);''')
edit(p,'            long furnitureStarted = System.Diagnostics.Stopwatch.GetTimestamp();','''            LastResponseMilliseconds = ReferenceClock.ElapsedMilliseconds(phaseStarted);
            long furnitureStarted = System.Diagnostics.Stopwatch.GetTimestamp();''')
edit(p,'            EvacuationSystem.BeginNaturalNavigationFrame(this);','''            phaseStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            EvacuationSystem.BeginNaturalNavigationFrame(this);''')
edit(p,'            HybridMovement.BeginFrame(this);','''            HybridMovement.BeginFrame(this);
            LastPreparationMilliseconds = ReferenceClock.ElapsedMilliseconds(phaseStarted);
            phaseStarted = System.Diagnostics.Stopwatch.GetTimestamp();''')
edit(p,'            ResolveCrowdContacts();\n            EvacuationSystem.ApplyExitFlows(this);','''            LastMovementMilliseconds = ReferenceClock.ElapsedMilliseconds(phaseStarted);
            phaseStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            ResolveCrowdContacts();
            LastContactsMilliseconds = ReferenceClock.ElapsedMilliseconds(phaseStarted);
            phaseStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            EvacuationSystem.ApplyExitFlows(this);''')
edit(p,'            FrameIndex++;','            LastExitMilliseconds = ReferenceClock.ElapsedMilliseconds(phaseStarted);\n            FrameIndex++;')
p=b+'Engine/Systems/SfmEngine.cs'
edit(p,'        public Vector2 ComputeForces(Simulation simulation, Agent agent, Vector2 desiredDirection)\n        {','''        public Vector2 ComputeForces(Simulation simulation, Agent agent, Vector2 desiredDirection)
        {
            long profileStarted = System.Diagnostics.Stopwatch.GetTimestamp();''')
edit(p,'            return total;','''            simulation.LastSfmMilliseconds += ReferenceClock.ElapsedMilliseconds(profileStarted);
            return total;''')
p=b+'Engine/Simulation.Collisions.cs'
# Small wrapper times every path, including early returns, without reordering body operations.
edit(p,'        public void MoveAgent(Agent agent)\n        {','''        public void MoveAgent(Agent agent)
        {
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            MoveAgentCore(agent);
            LastCollisionMilliseconds += ReferenceClock.ElapsedMilliseconds(started);
        }

        private void MoveAgentCore(Agent agent)
        {''')
p=b+'Diagnostics/DiagnosticFrame.cs'
edit(p,'        public double FurnitureMilliseconds { get; }','''        public double FurnitureMilliseconds { get; }
        public double GridMilliseconds { get; }
        public double HazardMilliseconds { get; }
        public double ResponseMilliseconds { get; }
        public double PreparationMilliseconds { get; }
        public double MovementMilliseconds { get; }
        public double ContactsMilliseconds { get; }
        public double ExitMilliseconds { get; }
        public double SfmMilliseconds { get; }
        public double CollisionMilliseconds { get; }''')
edit(p,'            PhysicsMilliseconds = model.LastStepMilliseconds; FurnitureMilliseconds = model.LastFurnitureMilliseconds;','''            PhysicsMilliseconds = model.LastStepMilliseconds; FurnitureMilliseconds = model.LastFurnitureMilliseconds;
            GridMilliseconds = model.LastGridMilliseconds; HazardMilliseconds = model.LastHazardMilliseconds;
            ResponseMilliseconds = model.LastResponseMilliseconds; PreparationMilliseconds = model.LastPreparationMilliseconds;
            MovementMilliseconds = model.LastMovementMilliseconds; ContactsMilliseconds = model.LastContactsMilliseconds;
            ExitMilliseconds = model.LastExitMilliseconds; SfmMilliseconds = model.LastSfmMilliseconds;
            CollisionMilliseconds = model.LastCollisionMilliseconds;''')
