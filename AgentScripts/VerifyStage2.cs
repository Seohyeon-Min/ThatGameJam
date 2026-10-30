using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEditor;

// Run each scenario in a fresh Play Mode session. Input devices and test speed are restored in finally.
public static class VerifyStage2
{
    static async Task Until(Func<bool> condition, float seconds, string failure)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (!condition() && Time.realtimeSinceStartup < end) await Task.Delay(40);
        if (!condition()) throw new Exception(failure);
    }
    static async Task Click(Mouse mouse, SnowBranch branch)
    {
        // Let the camera finish its one-second zone blend before positioning the virtual pointer.
        await Task.Delay(500);
        Vector3 world = branch.Perch.PerchPoint.position;
        Vector3 screen = Camera.main.WorldToScreenPoint(world);
        if (screen.z <= 0f || screen.x < 0f || screen.x > Screen.width || screen.y < 0f || screen.y > Screen.height)
            throw new Exception("Perch is off screen: " + branch.name + " " + screen);
        Physics.SyncTransforms();
        if (!Physics.Raycast(Camera.main.ScreenPointToRay(screen),out RaycastHit hit,100f,LayerMask.GetMask("Ground","Interactable","ClickPlane"))
            || hit.collider.GetComponentInParent<Perchable>() != branch.Perch)
            throw new Exception("Branch click is blocked: " + branch.name + " by " + hit.collider?.name);
        InputSystem.QueueStateEvent(mouse,new MouseState{position=screen});
        await Task.Delay(70);
        InputSystem.QueueStateEvent(mouse,new MouseState{position=screen}.WithButton(MouseButton.Left));
        await Task.Delay(70);
        InputSystem.QueueStateEvent(mouse,new MouseState{position=screen});
        try { await Until(()=>branch.IsOpen,5f,"Bird did not clear "+branch.name); }
        catch (Exception e)
        {
            var state=typeof(BirdController).GetField("state",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(BirdController.Instance);
            throw new Exception(e.Message+" bird="+BirdController.Instance.transform.position+" state="+state+" canPerch="+branch.Perch.CanPerch+" viewport="+Camera.main.WorldToViewportPoint(world));
        }
        if (BirdController.Instance.IsPerched || branch.Perch.CanPerch) throw new Exception("Branch did not release bird");
    }
    public static async Task<string> Run(string mode)
    {
        float oldScale=Time.timeScale;
        var oldFocus=InputSystem.settings.backgroundBehavior;
        Mouse mouse=InputSystem.AddDevice<Mouse>();
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        try
        {
            Time.timeScale=3f;
            while (GameFlow.Instance.CurrentStage.name != "Stage2") GameFlow.Instance.SkipToNextStage();
            Stage stage=GameFlow.Instance.CurrentStage;
            if(stage.name!="Stage2")throw new Exception("Not stage2");
            RabbitFamily family=stage.GetComponentInChildren<RabbitFamily>();
            SnowBranch[] branches=stage.GetComponentsInChildren<SnowBranch>().Where(b=>b.name.StartsWith("Bypass")).OrderBy(b=>b.name).ToArray();
            SnowBranch demo=stage.GetComponentsInChildren<SnowBranch>().First(b=>b.name=="DemoBranch");
            RabbitActor[] rabbits=stage.GetComponentsInChildren<RabbitActor>();
            if(rabbits.Length!=4 || branches.Length!=2)throw new Exception("Wrong stage population");
            if(rabbits.Any(r=>r.GetComponentInChildren<Perchable>()!=null))throw new Exception("Rabbits are perchable");
            BirdController.Instance.Teleport(new Vector3(0f,2.5f,30.7f));
            UnityEngine.Object.FindAnyObjectByType<BirdCamera>().Cut();
            await Task.Delay(200);
            int frame=Time.frameCount;
            await Task.Delay(200);
            if(Time.frameCount<=frame)throw new Exception("No advancing frames");
            if(family.Timer.IsRunning)throw new Exception("Timer starts before baby arrival");
            bool early=mode=="early";
            bool bird=mode=="bird"||early;
            if(!early)
            {
                await Until(()=>Baby.Instance.CurrentBlocker==family,18f,"Baby never waits at rabbits: "+Baby.Instance.State);
                if(family.Timer.Elapsed>2f)throw new Exception("Timer did not begin on arrival");
                if(!demo.IsOpen)throw new Exception("Demo branch did not clear before puzzle");
            }
            if(bird||mode=="partial")
            {
                await Click(mouse,branches[0]);
                if(family.IsOpen||family.Timer.IsResolved)throw new Exception("Single branch incorrectly opened family");
                if(bird)await Click(mouse,branches[1]);
            }
            if(bird)
            {
                await Until(()=>family.IsOpen,1f,"Family did not open after both branches");
                if(!family.IsOpen||!family.OpenedByBird)throw new Exception("Both branches did not open detour");
                if(rabbits.Any(r=>r.HasLeft))throw new Exception("Help woke rabbits");
            }
            else
            {
                await Until(()=>family.Timer.IsResolved,13f,"Natural timer never resolves");
                if(family.Timer.ResolvedByBird)throw new Exception("Natural incorrectly byBird");
                if(family.IsOpen)throw new Exception("Natural opens before rabbits leave");
                if(branches.Any(b=>b.Perch.CanPerch))throw new Exception("Natural resolution left interactive branches");
                // Late clicks cannot replace the natural result, even if they finish the branch animation.
                if(mode=="partial")branches[1].ShedSnow();
            }
            bool tookDetour=false;
            float end=Time.realtimeSinceStartup+26f;
            while(Baby.Instance.State!=BabyState.Resting&&Time.realtimeSinceStartup<end)
            {
                tookDetour|=Baby.Instance.transform.position.x < -2f;
                if(bird&&rabbits.Any(r=>r.HasLeft))throw new Exception("Detour woke sleeping rabbits");
                await Task.Delay(30);
            }
            if(Baby.Instance.State!=BabyState.Resting)throw new Exception("Baby stuck: "+Baby.Instance.State+" "+Baby.Instance.transform.position);
            if(tookDetour!=bird)throw new Exception("Wrong baby route, detour="+tookDetour);
            if(!bird&&rabbits.Any(r=>!r.HasLeft))throw new Exception("Natural did not move whole family");
            await Until(()=>UnityEngine.Object.FindAnyObjectByType<Season>().Level>=3,6f,"Stage2 did not finish season");
            await Task.Delay(1200);
            if(stage.GetComponentsInChildren<Stage2SnowMelt>()[0].GetComponentsInChildren<Transform>().Any(t=>t.name=="Divider"&&t.gameObject.activeInHierarchy))
                throw new Exception("Snowbanks did not melt");
            string report="PASS "+mode+": live frames; demo; 2 branches; correct resolution; correct baby route; family state; rest and season3 melt.";
            System.IO.File.WriteAllText("Temp/stage2-"+mode+"-test.txt",report);
            return report;
        }
        finally
        {
            Time.timeScale=oldScale;
            InputSystem.RemoveDevice(mouse);
            InputSystem.settings.backgroundBehavior=oldFocus;
        }
    }
}
