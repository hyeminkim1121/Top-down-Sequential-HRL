using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

/// <summary>
/// Heuristic Lower agent for TD Stage 1.
/// Follows Upper's target assignment: rotate toward target, move forward, shoot when in range.
/// </summary>
public class LowerAgentTD_forStage1 : LowerAgent
{
    public override void CollectObservations(VectorSensor sensor)
    {
        // Heuristic Only — no observations needed
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var actions = actionsOut.DiscreteActions;

        if (CurrentTarget == null || !IsActive)
        {
            actions[0] = 4; // idle
            return;
        }

        // Instantly face target
        Vector3 toTarget = CurrentTarget.transform.position - transform.position;
        toTarget.y = 0;
        if (toTarget != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(toTarget);
        }

        float distance = toTarget.magnitude;

        if (distance <= ShootingRange)
        {
            actions[0] = 5; // Shoot
            return;
        }

        actions[0] = 0; // MoveForward
    }
}
