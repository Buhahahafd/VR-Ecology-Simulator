using UnityEngine;

namespace SpaceDebris
{
    /// <summary>
    /// Defines a single gameplay scenario: the briefing text, countdown duration,
    /// winning and losing conditions, and resource constraints.
    /// </summary>
    [CreateAssetMenu(fileName = "NewScenario", menuName = "Space Debris/Scenario")]
    public class ScenarioData : ScriptableObject
    {
        [Header("Identity")]
        public string scenarioTitle = "Сценарий";

        [TextArea(4, 10)]
        public string briefingText =
            "Два спутника движутся по пересекающимся орбитам. " +
            "У вас есть ограниченное время и топливо для одного манёвра. Действуйте.";

        [Header("Timing")]
        [Tooltip("Seconds before the collision event fires if the player does nothing.")]
        public float countdownSeconds = 90f;

        [Header("Constraints")]
        [Tooltip("Maximum number of maneuvers allowed. 0 = unlimited.")]
        public int maxManeuvers = 1;

        [Tooltip("Fuel available per satellite in this scenario (overrides SatelliteController default).")]
        public float fuelPerSatellite = 100f;

        [Header("Outcome Messages")]
        [TextArea(2, 6)]
        public string successMessage =
            "Манёвр выполнен успешно. Столкновение предотвращено.";

        [TextArea(2, 6)]
        public string failureMessage =
            "Время истекло. Столкновение произошло. Кессле-синдром начался.";
    }
}
