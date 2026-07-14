using UnityEngine;
using UnityEngine.Events;

namespace VolFx
{
    public class IfMovePresented : MonoBehaviour
    {
        public UnityEvent _onInvoke;

        private void OnEnable()
        {
            #pragma warning disable CS0618
            var hasMove = FindObjectOfType<VolFxTrans_Move>();

            if (hasMove)
                _onInvoke.Invoke();
        }
    }
}