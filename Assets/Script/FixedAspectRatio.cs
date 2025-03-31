using UnityEngine;

[RequireComponent(typeof(Camera))]
public class FixedAspectRatio : MonoBehaviour
{
    private Camera _camera;
    private float _targetAspect = 16f / 9f;

    void Awake()
    {
        _camera = GetComponent<Camera>();
    }

    void LateUpdate()
    {
        float windowAspect = (float)Screen.width / Screen.height;
        float scaleHeight = windowAspect / _targetAspect;

        if (scaleHeight < 1f)
        {
            // Letterbox (bandes noires horizontales)
            Rect rect = _camera.rect;
            rect.width = 1f;
            rect.height = scaleHeight;
            rect.x = 0f;
            rect.y = (1f - scaleHeight) / 2f;
            _camera.rect = rect;
        }
        else
        {
            // Pillarbox (bandes noires verticales)
            float scaleWidth = 1f / scaleHeight;
            Rect rect = _camera.rect;
            rect.width = scaleWidth;
            rect.height = 1f;
            rect.x = (1f - scaleWidth) / 2f;
            rect.y = 0f;
            _camera.rect = rect;
        }
    }
}
