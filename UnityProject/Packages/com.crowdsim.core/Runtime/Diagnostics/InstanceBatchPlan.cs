using System;

namespace CrowdSim.Diagnostics
{
    /// <summary>Conservative instancing block size. Non-uniform body parts require both object and inverse matrices;
    /// 256 instances fit below Unity's default constant-buffer limit without assuming uniform scaling.</summary>
    public static class InstanceBatchPlan
    {
        public const int Capacity = 256;
        public static int BatchCount(int instances)
        {
            if (instances < 0) throw new ArgumentOutOfRangeException(nameof(instances));
            return instances / Capacity + (instances % Capacity == 0 ? 0 : 1);
        }
        public static int CountInBatch(int instances, int batch)
        {
            int count = BatchCount(instances);
            if (batch < 0 || batch >= count) throw new ArgumentOutOfRangeException(nameof(batch));
            return Math.Min(Capacity, instances - batch * Capacity);
        }
    }
}
