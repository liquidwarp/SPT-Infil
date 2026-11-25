using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Liquidwarp.Infil.UI;

public class ClickTrigger : MonoBehaviour, IPointerClickHandler
{
    private Action<PointerEventData> _onClick;

    public void Init(Action<PointerEventData> onClick) => _onClick = onClick;

    public void OnPointerClick(PointerEventData eventData) => _onClick?.Invoke(eventData);
}
