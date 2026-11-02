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

        root.Add(new HelpBox("Areas are child objects with a TreeZone (Trees, Clear or Pines). Size each box with its BoxCollider's Edit Collider button.", HelpBoxMessageType.Info));
        var buttons = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 6 } };
        buttons.Add(new Button(() => ForEach(g => g.Generate())) { text = "Generate", style = { flexGrow = 1 } });
        buttons.Add(new Button(() => ForEach(g => g.Clear())) { text = "Clear", style = { flexGrow = 1 } });
        root.Add(buttons);

        var count = new Label();
        count.schedule.Execute(() =>
        {
            var generator = (TreeGenerator)target;
            count.text = $"Trees made by this generator: {generator.TreeCount} ({generator.PineCount} pines, {generator.CanLeafCount} can leaf out)";
        }).Every(250);
        root.Add(count);
        return root;
    }

    void ForEach(System.Action<TreeGenerator> action)
    {
        foreach (UnityEngine.Object t in targets)
            action((TreeGenerator)t);
    }
}
