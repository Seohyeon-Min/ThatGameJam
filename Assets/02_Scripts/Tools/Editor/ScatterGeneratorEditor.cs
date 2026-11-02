using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

// Generate and Clear for every ScatterGenerator: FlowerGenerator and GrassGenerator.
[CustomEditor(typeof(ScatterGenerator), true)]
public class ScatterGeneratorEditor : Editor
{
    public override VisualElement CreateInspectorGUI()
    {
        var root = new VisualElement();
        InspectorElement.FillDefaultInspector(root, serializedObject, this);

        var buttons = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 6 } };
        buttons.Add(new Button(() => ForEach(g => g.Generate())) { text = "Generate", style = { flexGrow = 1 } });
        buttons.Add(new Button(() => ForEach(g => g.Clear())) { text = "Clear", style = { flexGrow = 1 } });
        buttons.Add(new Button(() => ForEach(g => g.SortIntoAreas()))
        {
            text = "Sort Into Areas",
            tooltip = "Puts what was made into one child per area by where it stands, without making anything again, so each area can be hidden by its season.",
            style = { flexGrow = 1 },
        });
        root.Add(buttons);

        var count = new Label();
        count.schedule.Execute(() => count.text = $"Made by this generator: {((ScatterGenerator)target).MadeCount}").Every(250);
        root.Add(count);
        return root;
    }

    void ForEach(System.Action<ScatterGenerator> action)
    {
        foreach (UnityEngine.Object t in targets)
            action((ScatterGenerator)t);
    }
}
