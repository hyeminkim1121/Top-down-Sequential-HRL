using Unity.MLAgents.Actuators;
using UnityEngine;

/// <summary>
/// RSA with rule-based Heuristic movement, random target.
/// BU-2의 Red opponent (Lower가 학습됐을 때 대등한 baseline).
/// 움직임: rule-based (rotate/shoot/forward), 타겟: alive 중 random.
/// </summary>
public class RsaAgentHeuristicMove : MARLAgent
{
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var actions = actionsOut.DiscreteActions;
        var aliveEnemies = EnemyLower.FindAll(e => e != null && e.IsActive);

        if (aliveEnemies.Count == 0)
        {
            actions[0] = 0;
            actions[1] = 0;
            return;
        }

        int validRange = Mathf.Min(MaxObservedEnemies, aliveEnemies.Count);
        int targetIdx = Random.Range(0, validRange);
        actions[1] = targetIdx;

        var target = aliveEnemies[targetIdx];
        Vector3 toTarget = target.transform.position - transform.position;
        float distance = toTarget.magnitude;
        float angle = Vector3.SignedAngle(transform.forward, toTarget, Vector3.up);

        if (Mathf.Abs(angle) > 5f)
        {
            actions[0] = 4;  // Rotate towards target
        }
        else if (distance < ShootingRange)
        {
            actions[0] = 5;  // Shoot
        }
        else
        {
            actions[0] = 0;  // Move forward
        }
    }
}
