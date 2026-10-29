using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

// Season test buttons, in Play Mode: advance one step, or apply the level typed in the Level field (it raises On Season Changed).
[CustomEditor(typeof(Season))]
public class SeasonEditor : Editor
{
    public override VisualElement CreateInspectorGUI()
    {
        var root = new VisualElement();
        InspectorElement.FillDefaultInspector(root, serializedObject, this);

        var season = (Season)target;
        var buttons = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 6 } };
        var advance = new Button(season.AdvanceSeason) { text = "Advance Season", style = { flexGrow = 1 } };
        var apply = new Button(() => season.SetSeason(season.Level)) { text = "Apply Level", style = { flexGrow = 1 } };
        buttons.Add(advance);
        buttons.Add(apply);
        root.Add(buttons);
        var note = new Label("Play Mode only.");
        root.Add(note);
        root.schedule.Execute(() =>
        {
            buttons.SetEnabled(Application.isPlaying);
            note.style.display = Application.isPlaying ? DisplayStyle.None : DisplayStyle.Flex;
        }).Every(250);
        return root;
    }
}
