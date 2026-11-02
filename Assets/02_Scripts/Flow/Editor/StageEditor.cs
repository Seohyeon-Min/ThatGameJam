using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

// Stage spacing tool: shift a stage (and everything parented under it) along +Z, optionally cascading the
// same shift to every stage after it in GameFlow's authored order (Stage.AdjustSpacing).
// In the Scene view, the selected stage's Area (the orange box the bird flies over) has handles on its faces to drag its sides,
// and one in the middle to move it whole.
[CustomEditor(typeof(Stage))]
public class StageEditor : Editor
{
    static readonly Color AreaColour = new(1f, 0.5f, 0f);
    readonly BoxBoundsHandle areaHandle = new() { axes = PrimitiveBoundsHandle.Axes.X | PrimitiveBoundsHandle.Axes.Z };

    public override VisualElement CreateInspectorGUI()
    {
        var root = new VisualElement();
        InspectorElement.FillDefaultInspector(root, serializedObject, this);

        var spacing = new VisualElement { style = { marginTop = 10 } };
        spacing.Add(new Label("Spacing") { style = { unityFontStyleAndWeight = FontStyle.Bold } });
        var delta = new FloatField("Shift Along Road (Z)") { value = 5f };
        var cascade = new Toggle("Also shift every later stage") { value = true, tooltip = "Keeps each later stage's own existing gaps. Off: the later stages stay, and the world closes the gap again where the next stage begins." };
        spacing.Add(new HelpBox("Opens the gap in the world too: the terrain grows at the join, and the streams, trees and flowers past it move along. Generate the trees, flowers and grass again to fill the gap.", HelpBoxMessageType.None));
        var apply = new Button(() => ((Stage)target).AdjustSpacing(delta.value, cascade.value)) { text = "Apply" };
        spacing.Add(delta);
        spacing.Add(cascade);
        spacing.Add(apply);
        root.Add(spacing);
        return root;
    }

    void OnSceneGUI()
    {
        var stage = (Stage)target;
        Bounds area = stage.area;

        areaHandle.center = area.center;
        areaHandle.size = area.size;
        areaHandle.SetColor(AreaColour);
        EditorGUI.BeginChangeCheck();
        areaHandle.DrawHandle();
        Vector3 centre = Handles.PositionHandle(area.center, Quaternion.identity);
        if (!EditorGUI.EndChangeCheck())
            return;

        Undo.RecordObject(stage, "Edit Stage Area");
        // The handle moved the sides, or the middle moved the whole box.
        Vector3 moved = centre - area.center;
        stage.area = moved != Vector3.zero
            ? new Bounds(area.center + moved, area.size)
            : new Bounds(areaHandle.center, new Vector3(areaHandle.size.x, area.size.y, areaHandle.size.z));
    }
}
