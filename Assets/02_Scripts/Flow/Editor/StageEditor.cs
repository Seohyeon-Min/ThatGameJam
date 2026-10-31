using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

// Stage spacing tool: shift a stage (and everything parented under it) along +Z, optionally cascading the
// same shift to every stage after it in GameFlow's authored order (Stage.AdjustSpacing).
[CustomEditor(typeof(Stage))]
public class StageEditor : Editor
{
    public override VisualElement CreateInspectorGUI()
    {
        var root = new VisualElement();
        InspectorElement.FillDefaultInspector(root, serializedObject, this);

        var spacing = new VisualElement { style = { marginTop = 10 } };
        spacing.Add(new UnityEngine.UIElements.Label("Spacing") { style = { unityFontStyleAndWeight = UnityEngine.FontStyle.Bold } });
        var delta = new FloatField("Shift Along Road (Z)") { value = 5f };
        var cascade = new Toggle("Also shift every later stage") { value = true, tooltip = "Keeps each later stage's own existing gaps; off only widens or narrows the gap right before this stage." };
        var apply = new Button(() => ((Stage)target).AdjustSpacing(delta.value, cascade.value)) { text = "Apply" };
        spacing.Add(delta);
        spacing.Add(cascade);
        spacing.Add(apply);
        root.Add(spacing);
        return root;
    }
}
