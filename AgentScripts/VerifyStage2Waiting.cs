using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

public static class VerifyStage2Waiting
{
    static async Task Until(Func<bool> condition, float timeout, string error)
    {
        float end=Time.realtimeSinceStartup+timeout;
        while(!condition()&&Time.realtimeSinceStartup<end)await Task.Delay(30);
        if(!condition())throw new Exception(error);
    }
    public static async Task<string> Run()
    {
        float scale=Time.timeScale;
        try
        {
            Time.timeScale=2f;
            while(GameFlow.Instance.CurrentStage.name!="Stage2")GameFlow.Instance.SkipToNextStage();
            var stage=GameFlow.Instance.CurrentStage;
            var family=stage.GetComponentInChildren<RabbitFamily>();
            if(family.GetComponent<AudioSource>()!=null)throw new Exception("Family still has obsolete rustle source");
            var branches=stage.GetComponentsInChildren<SnowBranch>().Where(b=>b.name.StartsWith("Bypass")).ToArray();
            var sounds=branches.Select(b=>b.GetComponent<AudioSource>()).ToArray();
            var girl=Baby.Instance.GetComponentInChildren<Animator>();
            BirdController.Instance.Teleport(new Vector3(0f,2.5f,30.7f));
            UnityEngine.Object.FindAnyObjectByType<BirdCamera>().Cut();
            await Until(()=>Baby.Instance.CurrentBlocker==family,22f,"Never arrived at family");
            await Task.Delay(200);
            if(!girl.GetCurrentAnimatorStateInfo(0).IsName("QuietWait"))throw new Exception("Not QuietWait at rabbit stop");
            while(!family.Timer.IsResolved)
            {
                if(sounds.Any(s=>s.isPlaying))throw new Exception("Snow-clear sound during idle waiting");
                if(!girl.GetCurrentAnimatorStateInfo(0).IsName("QuietWait"))throw new Exception("Girl left calm breathing while waiting");
                if(branches.Any(b=>b.IsOpen))throw new Exception("Waiting cleared a branch");
                await Task.Delay(25);
            }
            if(family.Timer.SignalLevel!=3)throw new Exception("Did not cover all waiting signals");
            await Until(()=>family.IsOpen,15f,"Family never leaves naturally");
            await Until(()=>Baby.Instance.State==BabyState.Walking,3f,"Baby never resumes walking");
            await Task.Delay(200);
            if(girl.GetBool("QuietWait")||!girl.GetCurrentAnimatorStateInfo(0).IsName("Walk"))throw new Exception("QuietWait not cleared on walk");
            // The real clear effect still makes a sound and disables the snow/perch.
            branches[0].ShedSnow();
            if(!sounds[0].isPlaying)throw new Exception("Actual branch clear lost its sound");
            await Until(()=>branches[0].IsOpen,2f,"Actual branch did not clear");
            return "PASS: all 3 waiting signals silent; QuietWait breathing throughout; natural resumes Walk; actual ShedSnow still plays sound.";
        }
        finally{Time.timeScale=scale;}
    }

    public static async Task<string> Bird()
    {
        float scale=Time.timeScale;
        try
        {
            Time.timeScale=3f;
            while(GameFlow.Instance.CurrentStage.name!="Stage2")GameFlow.Instance.SkipToNextStage();
            var stage=GameFlow.Instance.CurrentStage;
            var family=stage.GetComponentInChildren<RabbitFamily>();
            var branches=stage.GetComponentsInChildren<SnowBranch>().Where(b=>b.name.StartsWith("Bypass")).ToArray();
            var girl=Baby.Instance.GetComponentInChildren<Animator>();
            await Until(()=>Baby.Instance.CurrentBlocker==family,18f,"Never waits at family");
            await Task.Delay(150);
            if(!girl.GetCurrentAnimatorStateInfo(0).IsName("QuietWait"))throw new Exception("Not calm while waiting");
            foreach(var branch in branches)branch.Perch.NotifyPerched();
            await Until(()=>family.IsOpen,3f,"Bird event did not open detour");
            if(!family.OpenedByBird)throw new Exception("Wrong solution");
            await Until(()=>Baby.Instance.State==BabyState.Reacting,2f,"Missing reaction");
            if(!girl.GetBool("QuietWait"))throw new Exception("Reaction lost quiet idle");
            await Until(()=>Baby.Instance.transform.position.x < -2f,5f,"No detour");
            if(girl.GetBool("QuietWait"))throw new Exception("Walking retained quiet idle");
            if(stage.GetComponentsInChildren<RabbitActor>().Any(r=>r.HasLeft))throw new Exception("Rabbits woke");
            return "PASS: perch events open both branches; calm reaction; detour resumes walking; rabbits remain asleep.";
        }
        finally{Time.timeScale=scale;}
    }
}
