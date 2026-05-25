using Unity.MLAgents.Actuators;
using UnityEngine;

/// <summary>
/// Heuristic Upper agent for BU Stage 1.
/// Assigns targets uniformly at random among alive enemies.
/// Used with Behavior Type = Heuristic Only.
/// </summary>
public class UpperAgentBU_forStage1 : UpperAgent
{
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActions = actionsOut.DiscreteActions;
        var aliveEnemies = EnemyLower.FindAll(e => e != null && e.IsActive);

        if (aliveEnemies.Count == 0)
        {
            for (int i = 0; i < discreteActions.Length; i++)
                discreteActions[i] = 0;
            return;
        }

        int branches = Mathf.Min(discreteActions.Length, MyLower.Count);
        int validRange = Mathf.Min(MaxObservedEnemies, aliveEnemies.Count);

        for (int i = 0; i < branches; i++)
        {
            discreteActions[i] = Random.Range(0, validRange);
        }
    }
}
