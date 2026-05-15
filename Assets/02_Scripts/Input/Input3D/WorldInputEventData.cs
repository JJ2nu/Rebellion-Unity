using UnityEngine;

// Raycast 결과 정보
public readonly struct WorldInputEventData
{
    public WorldInputEventData(Camera camera, Ray ray, RaycastHit hit, GameObject targetObject)
    {
        Camera = camera;
        Ray = ray;
        Hit = hit;
        TargetObject = targetObject;
    }

    public Camera Camera { get; }
    public Ray Ray { get; }
    public RaycastHit Hit { get; }
    public GameObject TargetObject { get; }

    public Vector3 WorldPosition => Hit.point;
    public Vector3 WorldNormal => Hit.normal;
}
