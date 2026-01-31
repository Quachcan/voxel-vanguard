using UnityEngine;
using UnityEngine.EventSystems;

namespace _Workspace._Scripts.Core.UICore
{
    public class UIHelper
    {
        public static bool IsPointerOverUI()
        {
            if (EventSystem.current == null)
                return false;

            if (Application.isMobilePlatform)
            {
                if (Input.touchCount > 0)
                {
                    return EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
                }
            }
            else
            {
                return EventSystem.current.IsPointerOverGameObject();
            }

            return false;
        }
    }
}