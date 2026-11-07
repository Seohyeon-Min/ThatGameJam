using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// The player bird's controller: flying slowly it keeps beating its wings (Flap, looping), and only from its glide speed on does it
// glide (Glide, with its own wingbeat now and then); in place it hovers (Hover). A Slow bool (BirdController sets it) picks
// between the first two. Rerunning leaves it as it is.
// unity command script run --file AgentScripts/BirdSlowFlap.cs --entry BirdSlowFlap.Run
public static class BirdSlowFlap
{
    const string ControllerPath = "Assets/01_Arts/Models/Bird/Bird.controller";
    const string Slow = "Slow";

    public static string Run()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (!controller.parameters.Any(p => p.name == Slow))
            controller.AddParameter(Slow, AnimatorControllerParameterType.Bool);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState State(string name) => machine.states.First(s => s.state.name == name).state;
        AnimatorState hover = State("Hover"), flap = State("Flap"), glide = State("Glide"), glideFlap = State("GlideFlap");

        // Into Glide only when not slow.
        foreach (AnimatorState from in new[] { hover, flap, glideFlap })
            foreach (AnimatorStateTransition t in from.transitions.Where(t => t.destinationState == glide))
                Require(t, AnimatorConditionMode.IfNot);

        // Into Flap, moving and slow: from hovering at once, from gliding at once (eased), from the glide's own wingbeat at its end.
        Ensure(hover, flap, false, 0f, 0.25f);
        Ensure(glide, flap, false, 0f, 0.3f);
        Ensure(glideFlap, flap, true, 1f, 0.15f);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return "Bird.controller: " + string.Join(", ", new[] { hover, flap, glide, glideFlap }.Select(s =>
            s.name + " -> " + string.Join("/", s.transitions.Select(t => t.destinationState != null ? t.destinationState.name : "exit"))));
    }

    // The transition also needs Slow (If) or not slow (IfNot).
    static void Require(AnimatorStateTransition transition, AnimatorConditionMode mode)
    {
        if (transition.conditions.Any(c => c.parameter == Slow))
            transition.conditions = transition.conditions.Where(c => c.parameter != Slow).ToArray();
        transition.AddCondition(mode, 0f, Slow);
    }

    static void Ensure(AnimatorState from, AnimatorState to, bool exitTime, float exitAt, float duration)
    {
        AnimatorStateTransition transition = from.transitions.FirstOrDefault(t => t.destinationState == to);
        if (transition == null)
            transition = from.AddTransition(to);
        transition.hasExitTime = exitTime;
        transition.exitTime = exitAt;
        transition.duration = duration;
        transition.hasFixedDuration = true;
        transition.conditions = new AnimatorCondition[0];
        transition.AddCondition(AnimatorConditionMode.IfNot, 0f, "Perched");
        transition.AddCondition(AnimatorConditionMode.If, 0f, "Moving");
        transition.AddCondition(AnimatorConditionMode.If, 0f, Slow);
    }
}
