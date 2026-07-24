
// Raycast에 맞은 3D 오브젝트가 구현하는 입력 인터페이스

public interface IWorldInputTarget
{
    void OnWorldHover(WorldInputEventData eventData);
    void OnWorldUnHover(WorldInputEventData eventData);
    void OnWorldLeftClick(WorldInputEventData eventData);
    void OnWorldRightClick(WorldInputEventData eventData);
}
