using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CrowdSim.Core;
using CrowdSim.Core.Model;

namespace CrowdSim.Diagnostics
{
    /// <summary>Shared seeded cloud/Editor comparison layout. Independent authoring RNG does not consume model traits.</summary>
    public static class CrowdScaleLayout
    {
        public static Simulation Create(int count, bool furnished, bool hybrid, ScenarioType scenario = ScenarioType.Explosion)
        {
            if (count != 220 && count != 500 && count != 1000) throw new ArgumentOutOfRangeException(nameof(count));
            Simulation model = new Simulation(randomSeed: 731); model.Config.HybridEnabled = hybrid; model.SetScenarioType(scenario);
            foreach (float y in new[] { 10f, 24f })
                if (!model.Environment.AddExitFromClick(.3f, y, 5) || !model.Environment.AddExitFromClick(model.Environment.Width - .3f, y, 5))
                    throw new InvalidOperationException("Scale exit placement failed.");
            if (furnished)
                foreach (float x in new[] { 12f, 24f, 36f, 48f })
                foreach (float y in new[] { 12f, 24f })
                    if (!model.Environment.AddFurniture(FurnitureKind.RoundTableSet, new Vector2(x, y))) throw new InvalidOperationException("Scale furniture placement failed.");
            List<Vector2> positions = model.Environment.GetPlannedAgentPositions().ToList();
            int seats = positions.Count;
            Random layout = new Random(731); int attempts = 0;
            float separation = model.Config.Agent.Radius * 2.1f;
            List<Vector2> standing = new List<Vector2>();
            while (positions.Count < count && attempts++ < 200000)
            {
                Vector2 p = new Vector2(1 + (float)layout.NextDouble() * (model.Environment.Width - 2), 1 + (float)layout.NextDouble() * (model.Environment.Height - 2));
                if (model.Environment.IsPointBlocked(p, model.Config.Agent.Radius) || positions.Any(q => Vector2.DistanceSquared(p, q) < separation * separation)) continue;
                positions.Add(p); standing.Add(p);
            }
            if (positions.Count != count) throw new InvalidOperationException("Scale layout could not place the requested population.");
            model.Environment.AgentSpawnGroups.Add(new AgentSpawnGroup(new Vector2(30, 17), standing));
            if (model.Environment.PlannedAgentCount != count || seats + standing.Count != count) throw new InvalidOperationException("Scale population mismatch.");
            if (scenario == ScenarioType.ToxicFume) model.ToxicFumeSystem.RestoreSource(new Vector2(28, 17));
            model.BuildWallGrid(); return model;
        }
    }
}
