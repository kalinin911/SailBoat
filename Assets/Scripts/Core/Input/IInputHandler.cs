using System;
using UnityEngine;

namespace Core.Input
{
    public interface IInputHandler
    {
        event Action<Vector3> OnMapClicked;
        void Initialize();
        void Dispose();
    }
}