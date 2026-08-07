namespace SHIN
{
    /// <summary>
    /// 스킬용 Virtual Camera Body 타입.
    /// Cinemachine Body 컴포넌트와 대응합니다.
    /// </summary>
    public enum SkillCameraType
    {
        DoNothing = 0,
        Follow = 1,
        FramingTransposer = 2,
        HardLock = 3,
        OrbitalTransposer = 4,
        TrackedDolly = 5,
    }
}
