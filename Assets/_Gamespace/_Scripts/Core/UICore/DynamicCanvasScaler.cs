using UnityEngine;
using UnityEngine.UI;

namespace _Workspace._Scripts.Core.UICore
{
    [RequireComponent(typeof(CanvasScaler))]
    public class DynamicCanvasScaler : MonoBehaviour
    {
        void Start()
        {
            var scaler = GetComponent<CanvasScaler>();

            float screenRatio = (float)Screen.height / Screen.width;

            if (screenRatio >= 2.0f)
            {
                scaler.matchWidthOrHeight = 1f; 
            }
            else if (screenRatio <= 1.4f)
            {
                scaler.matchWidthOrHeight = 0.3f;
            }
            else
            {
                scaler.matchWidthOrHeight = 0.55f; 
            }
        }
    }
}