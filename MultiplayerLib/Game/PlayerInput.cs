using System.Numerics;

namespace MultiplayerLib.Game;

public struct PlayerInput
{
    public Vector2 MoveDirection;
    public bool IsShooting;
    public bool IsJumping;
    public bool IsCrouching;
    public float Timestamp;
}