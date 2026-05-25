using Unity.MLAgents.Actuators;
using UnityEngine;

/// <summary>
/// HRL Ablation 실험용 Upper Agent. UpperAgent의 모든 동작을 상속받고
/// Heuristic Only 모드에서 random target 할당만 추가로 구현.
/// Bottom-up Stage 1에서 Behavior Type = Heuristic Only로 사용.
/// Bottom-up Stage 2에서 Behavior Type = Default로 PPO 학습 가능.
/// </summary>
public class UpperAgentAblation : UpperAgent
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
