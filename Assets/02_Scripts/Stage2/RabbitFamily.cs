using System.Collections;
using UnityEngine;

// Branches open the side path without waking the family; otherwise the rabbits leave together.
public class RabbitFamily : PathBlocker
{
    [SerializeField] NaturalTimer timer;
    [SerializeField] SnowBranch[] branches;
    // Mother first, followed by the young rabbits.
    [SerializeField] RabbitActor[] rabbits;

    public override NaturalTimer Timer => timer;

    int lastSignal;

    void Awake()
    {
        foreach (SnowBranch branch in branches)
            branch.Cleared += CheckBranches;
        timer.Resolved += OnResolved;
    }

    void OnDestroy()
    {
        foreach (SnowBranch branch in branches)
            branch.Cleared -= CheckBranches;
        timer.Resolved -= OnResolved;
    }

    public override void BeginWaiting() => timer.Begin();

    void CheckBranches()
    {
        foreach (SnowBranch branch in branches)
            if (!branch.IsOpen)
                return;
        timer.ResolveByBird();
    }

    void OnResolved(bool byBird)
    {
        if (byBird)
        {
            StartCoroutine(OpenByBird());
            return;
        }

        foreach (SnowBranch branch in branches)
            branch.DisablePerch();
        StartCoroutine(LeaveTogether());
    }

    IEnumerator OpenByBird()
    {
        // The developer shortcut resolves the timer directly; it still has to clear the visible path.
        foreach (SnowBranch branch in branches)
            branch.ShedSnow();
        foreach (SnowBranch branch in branches)
            yield return new WaitUntil(() => branch.IsOpen);
        Open(true);
    }

    IEnumerator LeaveTogether()
    {
        foreach (RabbitActor rabbit in rabbits)
            StartCoroutine(rabbit.Leave());
        foreach (RabbitActor rabbit in rabbits)
            yield return new WaitUntil(() => rabbit.HasLeft);
        Open(false);
    }

    void Update()
    {
        if (timer.IsResolved)
            return;
        while (lastSignal < timer.SignalLevel)
        {
            lastSignal++;
            if (lastSignal == 1)
            {
                foreach (SnowBranch branch in branches)
                    branch.PulseSnow();
            }
            else
            {
                if (lastSignal == 2)
                {
                    for (int i = 1; i < rabbits.Length; i++)
                        rabbits[i].Stir();
                }
                else
                    rabbits[0].Stir();
            }
        }
    }
}
