using UnityEngine;

/// <summary>
/// 全景背景与场景交互物共用的坐标基准。
/// 全景像素锚点来自正式背景图，避免门、楼梯和背景各自硬编码后逐渐漂移。
/// </summary>
public static class SceneLayoutMetrics
{
    public const string PanoramaAssetPath = "Assets/Art/Scene/YuyinPanorama.png";

    public const int PanoramaSourceWidth = 1670;
    public const int PanoramaSourceHeight = 540;
    public const int PanoramaPixelsPerUnit = 100;

    public const float PanoramaWorldWidth = 38.4f;
    public const float PanoramaWorldHeight = 12.96f;
    public const float PanoramaScaleX = PanoramaWorldWidth / (PanoramaSourceWidth / (float)PanoramaPixelsPerUnit);
    public const float PanoramaScaleY = PanoramaWorldHeight / (PanoramaSourceHeight / (float)PanoramaPixelsPerUnit);

    public const float WorldMinX = -9.6f;
    public const float WorldMaxX = 28.8f;
    public const float PixelToWorldX = PanoramaWorldWidth / PanoramaSourceWidth;
    public const float PixelToWorldY = 0.024f;

    public const float FirstFloorBackdropX = 9.6f;
    public const float FirstFloorBackdropY = 1.76f;
    public const float SecondFloorBackdropX = 9.6f;
    public const float SecondFloorBackdropY = -3.04f;
    public const float BackyardBackdropX = -10.69f;
    public const float BackyardBackdropY = 1.76f;

    public const float FirstFloorCameraMinY = -4.72f;
    public const float FirstFloorCameraMaxY = 6.08f;
    public const float SecondFloorCameraMinY = -7.36f;
    public const float SecondFloorCameraMaxY = 3.44f;
    public const float BackyardCameraMinY = -3.64f;
    public const float BackyardCameraMaxY = 7.16f;

    public const float GrandmaDoorX = -5.37f;
    public const float UncleDoorX = 3.32f;
    public const float MotherDoorX = -5.78f;
    public const float GirlDoorX = 6.75f;
    public const float FirstFloorDoorY = -1.36f;
    public const float SecondFloorDoorY = -1.17f;

    public const float StairsX = 8.45f;
    public const float StairsArrivalOffset = 1.2f;
    public const float GroundLineY = -3.4f;
    public const float PlayerY = -3.0f;
    public const float PlayerFootLineY = -3.5f;

    public const int BackdropSortingOrder = -100;

    public static Vector3 StitchedBackdropPosition
    {
        get { return new Vector3(FirstFloorBackdropX, FirstFloorBackdropY, 0f); }
    }

    public static Vector3 BackdropScale
    {
        get { return new Vector3(PanoramaScaleX, PanoramaScaleY, 1f); }
    }

    public static Vector3 NormalBackdropPosition(AreaId area)
    {
        switch (area)
        {
            case AreaId.SecondFloor:
                return new Vector3(SecondFloorBackdropX, SecondFloorBackdropY, 0f);
            case AreaId.Backyard:
                return new Vector3(BackyardBackdropX, BackyardBackdropY, 0f);
            default:
                return new Vector3(FirstFloorBackdropX, FirstFloorBackdropY, 0f);
        }
    }

    public static Vector2 CameraVerticalBounds(AreaId area)
    {
        switch (area)
        {
            case AreaId.SecondFloor:
                return new Vector2(SecondFloorCameraMinY, SecondFloorCameraMaxY);
            case AreaId.Backyard:
                return new Vector2(BackyardCameraMinY, BackyardCameraMaxY);
            default:
                return new Vector2(FirstFloorCameraMinY, FirstFloorCameraMaxY);
        }
    }

    public static float SourceXToWorld(float sourceX)
    {
        return WorldMinX + sourceX * PixelToWorldX;
    }
}
