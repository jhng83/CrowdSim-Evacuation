using CrowdSim.OpenGL.UI;
using UnityEngine.UIElements;

namespace CrowdSim.Unity.Workspace
{
    public sealed partial class WorkspaceController
    {
        private FloatField _stageLength, _stageWidth;
        private Label _stageLimit;
        private void BuildStageControls()
        {
            _stageLength = new FloatField("Stage length (m)") { value = Session.StageLength, isDelayed = true, name = "stage-length" };
            _stageWidth = new FloatField("Stage depth / width (m)") { value = Session.StageWidth, isDelayed = true, name = "stage-width" };
            _stageLength.RegisterValueChangedCallback(evt => ResizeStage(evt.newValue, Session.StageWidth));
            _stageWidth.RegisterValueChangedCallback(evt => ResizeStage(Session.StageLength, evt.newValue));
            _assetControls.Add(_stageLength); _assetControls.Add(_stageWidth);
            _stageLimit = Label(_assetControls, "", "muted"); UpdateStageControls();
        }
        private void ResizeStage(float length, float width)
        {
            _operationStatus = ""; Session.SetStageSize(length, width);
            ClearPreview(); Draw(); Refresh(); Repreview();
        }
        private void UpdateStageControls()
        {
            if (_stageLength == null || Session.SelectedAsset != BuilderAsset.Stage) return;
            _stageLength.SetValueWithoutNotify(Session.StageLength); _stageWidth.SetValueWithoutNotify(Session.StageWidth);
            _stageLimit.text = "Length ≤ " + Session.MaximumStageLength.ToString("F1") + " m; depth ≤ " + Session.MaximumStageWidth.ToString("F1") +
                " m at this facing. Minimum 1 m each. Physical perimeter inset retained. Resize also checks neighbouring assets and people.";
        }
    }
}
