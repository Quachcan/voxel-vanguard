using System;
using UnityEngine;

namespace _GameSpace._Scripts.Core.EventCore
{
    public abstract class Event<T> : ScriptableObject
    {
        public event Action<T> OnEventRaised;
        
        public void Raise(T data)
        {
            OnEventRaised?.Invoke(data);
        }
        
        public void Subscribe(Action<T> callback)
        {
            OnEventRaised += callback;
        }
        
        public void Unsubscribe(Action<T> callback)
        {
            OnEventRaised -= callback;
        }
    }
}