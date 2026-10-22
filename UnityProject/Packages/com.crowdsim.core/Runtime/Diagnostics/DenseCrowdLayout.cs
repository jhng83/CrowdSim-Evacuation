using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CrowdSim.Core;
using CrowdSim.Core.Model;

namespace CrowdSim.Diagnostics
{
    /// <summary>Reproducible furniture-heavy performance fixture: 800 people including 320 seated at 32 tables.</summary>
    public static class DenseCrowdLayout
    {
        public static Simulation Create(bool hybrid, ScenarioType scenario)
        {
            Simulation model = new Simulation(randomSeed: 731); model.Config.HybridEnabled = hybrid; model.SetScenarioType(scenario);
            foreach (float y in new[] { 7f, 28f })
                if (!model.Environment.AddExitFromClick(.3f, y, 5) || !model.Environment.AddExitFromClick(55.7f, y, 5))
                    throw new InvalidOperationException("Dense exit placement failed.");
            for (int x = 0; x < 8; x++)
            for (int y = 0; y < 4; y++)
                if (!model.Environment.AddFurniture(FurnitureKind.RoundTableSet, new Vector2(6 + 6 * x, 5 + 6 * y)))
                    throw new InvalidOperationException("Dense furniture placement failed.");
            List<Vector2> positions = model.Environment.GetPlannedAgentPositions().ToList();
            List<Vector2> standing = new List<Vector2>();
            Random layout = new Random(731); float gap = model.Config.Agent.Radius * 2.1f;
            for (int attempts = 0; positions.Count < 800 && attempts < 200000; attempts++)
            {
                Vector2 p = new Vector2(1 + (float)layout.NextDouble() * 54, 1 + (float)layout.NextDouble() * 32);
                if (model.Environment.IsPointBlocked(p, model.Config.Agent.Radius * 1.08f + .001f) || positions.Any(q => Vector2.DistanceSquared(p, q) < gap * gap)) continue;
                standing.Add(p); positions.Add(p);
            }
            if (positions.Count != 800) throw new InvalidOperationException("Dense population placement failed.");
            model.Environment.AgentSpawnGroups.Add(new AgentSpawnGroup(new Vector2(28, 17), standing));
            if (scenario == ScenarioType.ToxicFume) model.ToxicFumeSystem.RestoreSource(new Vector2(28, 17));
            model.BuildWallGrid(); return model;
        }
    }
}
