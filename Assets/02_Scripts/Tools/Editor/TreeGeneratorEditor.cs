using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

[CustomEditor(typeof(TreeGenerator))]
public class TreeGeneratorEditor : Editor
{
    public override VisualElement CreateInspectorGUI()
    {
        var root = new VisualElement();
        InspectorElement.FillDefaultInspector(root, serializedObject, this);

        var buttons = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 6 } };
        buttons.Add(new Button(() => ForEach(g => g.Generate())) { text = "Generate", style = { flexGrow = 1 } });
        buttons.Add(new Button(() => ForEach(g => g.Clear())) { text = "Clear", style = { flexGrow = 1 } });
        root.Add(buttons);

        var count = new Label();
        count.schedule.Execute(() => count.text = $"Trees made by this generator: {((TreeGenerator)target).TreeCount}").Every(250);
        root.Add(count);
        return root;
    }

    void ForEach(System.Action<TreeGenerator> action)
    {
        foreach (UnityEngine.Object t in targets)
            action((TreeGenerator)t);
    }
}
