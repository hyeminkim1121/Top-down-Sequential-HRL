// Red팀 LowerAgent — 교전 실험용
// EnemyLower를 LowerAgent로 받을 수 있도록 별도 리스트 추가
using System.Collections.Generic;
using UnityEngine;

public class RedLowerAgent : LowerAgent
{
    [Tooltip("적군 (Blue LowerAgent)")]
    public List<LowerAgent> EnemyBlue = new();
}
