using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

[CustomEditor(typeof(CloudGenerator))]
public class CloudGeneratorEditor : Editor
{
    public override VisualElement CreateInspectorGUI()
    {
        var root = new VisualElement();
        InspectorElement.FillDefaultInspector(root, serializedObject, this);

        root.Add(new Button(() =>
        {
            foreach (UnityEngine.Object t in targets)
                ((CloudGenerator)t).Generate();
        }) { text = "Generate", style = { marginTop = 6 } });

        var count = new Label();
        count.schedule.Execute(() => count.text = $"Puffs: {((CloudGenerator)target).PuffCount}").Every(500);
        root.Add(count);
        return root;
    }
}
