#nullable enable
// M2 import of reference 01b4355; see docs/unity-migration/COMPATIBILITY.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrowdSim.Core.Compatibility;
namespace CrowdSim.Core.Model
{
    /// <summary>
    /// Top-level emergency scenario selected before scene construction. The crowd engine, SFM,
    /// routing, recording and metrics remain shared; only the hazard subsystem differs.
    /// </summary>
    public enum ScenarioType
    {
        Explosion = 0,
        ToxicFume = 1,
    }

    /// <summary>Runtime exposure band for the abstract normalized toxic-fume model.</summary>
    public enum ToxicExposureStatus
    {
        Clear = 0,
        Exposed = 1,
        Impaired = 2,
        Incapacitated = 3,
    }
}
