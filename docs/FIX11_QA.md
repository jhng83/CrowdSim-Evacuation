# FIX11 QA Notes

- Baseline: FIX10, user-validated locally with build success and 23/23 tests passed.
- Added environment-aware exploration waypoint selection and exit-visibility reacquisition.
- Added short exploration replanning hysteresis/commitment.
- Added modest crowd penalty to exploration waypoint scoring.
- Changed congestion heat overlay from red/orange to purple only; panic ring unchanged.
- Added two exploration regression tests (expected suite total: 25).
- Static checks completed: modified C# files have balanced braces/parentheses; ZIP integrity is checked during packaging.
- .NET SDK unavailable in the assistant container; compile/test must be run on the user's machine.
