using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using UnityEngine;

public class RsaAgent : MARLAgent
{
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var actions = actionsOut.DiscreteActions;

        // Branch 0: 이동 uniform random (6 options: 0~5)
        actions[0] = Random.Range(0, 6);

        // Branch 1: alive enemy 중 uniform random 타겟 선택
        var aliveEnemies = EnemyLower.FindAll(e => e != null && e.IsActive);

        if (aliveEnemies.Count == 0)
        {
            actions[1] = 0;
        }
        else
        {
            int validRange = Mathf.Min(MaxObservedEnemies, aliveEnemies.Count);
            actions[1] = Random.Range(0, validRange);
        }
    }
}
