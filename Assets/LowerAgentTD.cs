using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

public class LowerAgentTD : LowerAgent
{
    public override void CollectObservations(VectorSensor sensor)
    {
        // Heuristic Only — observation 불필요
    }
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var actions = actionsOut.DiscreteActions;

        if (CurrentTarget == null || !IsActive)
        {
            actions[0] = 4; // 정지
            return;
        }

        // 매 스텝 타겟 방향으로 즉시 회전
        Vector3 toTarget = CurrentTarget.transform.position - transform.position;
        toTarget.y = 0;
        if (toTarget != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(toTarget);
        }

        float distance = toTarget.magnitude;

        // 사거리 내 → 사격
        if (distance <= ShootingRange)
        {
            actions[0] = 5; // Shoot
            return;
        }

        // 사거리 밖 → 전진 (이미 타겟 방향 바라보고 있음)
        actions[0] = 0; // MoveForward
    }
}
